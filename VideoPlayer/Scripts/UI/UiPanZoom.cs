using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public enum UiPanLimitMode
{
    Cover,
    Contain
}

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("UI/Pan Zoom")]
public class UiPanZoom : MonoBehaviour, IPointerDownHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
    [Header("対象")]
    [Tooltip("動かしたい RectTransform。空ならこのオブジェクト自身")]
    public List<RectTransform> targets = new List<RectTransform>();
    [Tooltip("パン制限の基準。空なら先頭のターゲット")]
    public RectTransform panLimitTarget;

    [Header("操作")]
    public bool enablePan = true;
    public bool enableZoom = true;
    [Tooltip("Button などの Selectable 上ではパンしない（クリックを優先）")]
    public bool ignoreWhenOverSelectable = true;

    [Header("ズーム")]
    [Min(0.1f)] public float minZoom = 1f;
    [Min(0.1f)] public float maxZoom = 4f;
    [Tooltip("ホイール1ノッチあたりの倍率")]
    public float zoomStep = 0.1f;
    [Tooltip("有効化時に位置と倍率を初期状態へ戻す")]
    public bool resetOnEnable = true;
    [Tooltip("ダブルクリックで初期状態へ戻す")]
    public bool resetOnDoubleClick = true;

    [Header("パン制限")]
    [FormerlySerializedAs("clampToParent")]
    [Tooltip("パンできる範囲を制限する")]
    public bool limitPan = true;
    [Tooltip("Cover: 範囲を常に覆う（動画向け） / Contain: 範囲の内側に収める")]
    public UiPanLimitMode panLimitMode = UiPanLimitMode.Cover;
    [Tooltip("空なら制限基準ターゲットの親を範囲にします")]
    public RectTransform panBounds;
    [Tooltip("制限の遊び（px）。0 なら端ぴったり")]
    public float panLimitPadding = 0f;

    [SerializeField, HideInInspector, FormerlySerializedAs("target")]
    private RectTransform legacyTarget;

    private readonly List<TrackedTarget> tracked = new List<TrackedTarget>();
    private float currentZoom = 1f;
    private bool dragging;
    private bool pinching;
    private float lastPinchDistance;
    private Camera eventCamera;
    private Canvas canvas;

    public float CurrentZoom => currentZoom;

    private class TrackedTarget
    {
        public RectTransform rect;
        public Vector2 homeAnchoredPosition;
        public Vector3 homeLocalScale;
    }

    private void Awake()
    {
        MigrateLegacyTarget();
        CacheTargets();
        CaptureHome();
        RefreshCanvas();
    }

    private void OnEnable()
    {
        MigrateLegacyTarget();
        CacheTargets();
        EnsureHomes();
        RefreshCanvas();
        if (resetOnEnable && Application.isPlaying) ResetView();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        zoomStep = Mathf.Max(0.01f, zoomStep);
        MigrateLegacyTarget();
    }

    public void ResetView()
    {
        dragging = false;
        pinching = false;
        currentZoom = 1f;
        if (tracked.Count == 0) CacheTargets();

        for (int i = 0; i < tracked.Count; i++)
        {
            TrackedTarget item = tracked[i];
            if (item.rect == null) continue;
            if (IsNearZeroScale(item.homeLocalScale)) continue;
            item.rect.localScale = item.homeLocalScale;
            item.rect.anchoredPosition = item.homeAnchoredPosition;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null) return;
        eventCamera = eventData.pressEventCamera;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData == null) return;
        eventData.useDragThreshold = true;
        if (ShouldIgnore(eventData))
        {
            eventData.pointerDrag = null;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = enablePan && eventData != null && eventData.button == PointerEventData.InputButton.Left && !ShouldIgnore(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || eventData == null || tracked.Count == 0) return;
        PanByScreenDelta(eventData.delta, eventData.pressEventCamera);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (!enableZoom || eventData == null) return;
        float delta = eventData.scrollDelta.y;
        if (Mathf.Approximately(delta, 0f)) delta = eventData.scrollDelta.x;
        if (Mathf.Approximately(delta, 0f)) return;

        float factor = 1f + Mathf.Sign(delta) * zoomStep;
        ApplyZoom(currentZoom * factor, eventData.position, eventData.pressEventCamera);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!resetOnDoubleClick || eventData == null || eventData.clickCount < 2) return;
        if (ShouldIgnore(eventData)) return;
        ResetView();
    }

    private void Update()
    {
        if (!enableZoom || !isActiveAndEnabled) return;
        HandlePinchZoom();
    }

    private void HandlePinchZoom()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen == null)
        {
            pinching = false;
            return;
        }

        Camera cam = EventCamera();
        Vector2 first = default;
        Vector2 second = default;
        int count = 0;

        for (int i = 0; i < touchscreen.touches.Count; i++)
        {
            var touch = touchscreen.touches[i];
            if (!touch.isInProgress) continue;
            Vector2 pos = touch.position.ReadValue();
            if (!ContainsScreenPoint(pos, cam)) continue;
            if (count == 0) first = pos;
            else if (count == 1) second = pos;
            count++;
            if (count >= 2) break;
        }

        if (count < 2)
        {
            pinching = false;
            return;
        }

        dragging = false;
        float distance = Vector2.Distance(first, second);
        Vector2 midpoint = (first + second) * 0.5f;
        if (!pinching)
        {
            pinching = true;
            lastPinchDistance = distance;
            return;
        }

        if (lastPinchDistance > 1f && distance > 1f)
        {
            ApplyZoom(currentZoom * (distance / lastPinchDistance), midpoint, cam);
        }

        lastPinchDistance = distance;
    }

    private void PanByScreenDelta(Vector2 screenDelta, Camera cam)
    {
        if (tracked.Count == 0 || screenDelta.sqrMagnitude < 0.0001f) return;

        for (int i = 0; i < tracked.Count; i++)
        {
            RectTransform rect = tracked[i].rect;
            if (rect == null) continue;
            RectTransform parent = rect.parent as RectTransform;
            if (parent == null) continue;

            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos, cam, out Vector2 localBefore)) continue;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPos + screenDelta, cam, out Vector2 localAfter)) continue;
            TranslateLocalXy(rect, localAfter - localBefore);
        }

        ClampPan();
    }

    private void ApplyZoom(float nextZoom, Vector2 screenPosition, Camera cam)
    {
        if (tracked.Count == 0) return;

        float zoom = Mathf.Clamp(nextZoom, minZoom, maxZoom);
        if (Mathf.Approximately(zoom, currentZoom)) return;

        Camera eventCam = cam != null ? cam : EventCamera();
        currentZoom = zoom;

        for (int i = 0; i < tracked.Count; i++)
        {
            TrackedTarget item = tracked[i];
            if (item.rect == null) continue;

            bool hasPoint = RectTransformUtility.ScreenPointToWorldPointInRectangle(item.rect, screenPosition, eventCam, out Vector3 worldBefore);
            Vector3 localPoint = hasPoint ? item.rect.InverseTransformPoint(worldBefore) : Vector3.zero;

            Vector3 nextScale = new Vector3(
                item.homeLocalScale.x * currentZoom,
                item.homeLocalScale.y * currentZoom,
                item.homeLocalScale.z == 0f ? 1f : item.homeLocalScale.z);
            item.rect.localScale = nextScale;

            if (hasPoint)
            {
                Vector3 worldAfter = item.rect.TransformPoint(localPoint);
                TranslateWorldXy(item.rect, worldBefore - worldAfter);
            }
        }

        ClampPan();
    }

    private void ClampPan()
    {
        if (!limitPan) return;

        RectTransform limitSource = ResolveLimitSource();
        if (limitSource == null) return;

        RectTransform bounds = panBounds != null ? panBounds : limitSource.parent as RectTransform;
        if (bounds == null || !IsUsableBounds(bounds)) return;

        GetLocalMinMax(limitSource, bounds, out Vector2 targetMin, out Vector2 targetMax);
        GetLocalMinMax(bounds, bounds, out Vector2 boundsMin, out Vector2 boundsMax);

        float pad = panLimitPadding;
        boundsMin += new Vector2(pad, pad);
        boundsMax -= new Vector2(pad, pad);

        Vector2 delta = Vector2.zero;
        float targetWidth = targetMax.x - targetMin.x;
        float targetHeight = targetMax.y - targetMin.y;
        float boundsWidth = boundsMax.x - boundsMin.x;
        float boundsHeight = boundsMax.y - boundsMin.y;

        if (panLimitMode == UiPanLimitMode.Cover)
        {
            delta.x = CoverAxis(targetMin.x, targetMax.x, targetWidth, boundsMin.x, boundsMax.x, boundsWidth);
            delta.y = CoverAxis(targetMin.y, targetMax.y, targetHeight, boundsMin.y, boundsMax.y, boundsHeight);
        }
        else
        {
            delta.x = ContainAxis(targetMin.x, targetMax.x, targetWidth, boundsMin.x, boundsMax.x, boundsWidth);
            delta.y = ContainAxis(targetMin.y, targetMax.y, targetHeight, boundsMin.y, boundsMax.y, boundsHeight);
        }

        if (delta.sqrMagnitude < 0.0001f) return;

        Vector3 worldDelta = bounds.TransformVector(new Vector3(delta.x, delta.y, 0f));
        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i].rect != null) TranslateWorldXy(tracked[i].rect, worldDelta);
        }
    }

    private static float CoverAxis(float targetMin, float targetMax, float targetSize, float boundsMin, float boundsMax, float boundsSize)
    {
        if (targetSize <= boundsSize)
        {
            return ((boundsMin + boundsMax) - (targetMin + targetMax)) * 0.5f;
        }

        float delta = 0f;
        if (targetMin > boundsMin) delta += boundsMin - targetMin;
        if (targetMax < boundsMax) delta += boundsMax - targetMax;
        return delta;
    }

    private static float ContainAxis(float targetMin, float targetMax, float targetSize, float boundsMin, float boundsMax, float boundsSize)
    {
        if (targetSize >= boundsSize)
        {
            return ((boundsMin + boundsMax) - (targetMin + targetMax)) * 0.5f;
        }

        float delta = 0f;
        if (targetMin < boundsMin) delta += boundsMin - targetMin;
        if (targetMax > boundsMax) delta += boundsMax - targetMax;
        return delta;
    }

    private static void GetLocalMinMax(RectTransform source, RectTransform space, out Vector2 min, out Vector2 max)
    {
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            Vector3 local = space.InverseTransformPoint(corners[i]);
            min.x = Mathf.Min(min.x, local.x);
            min.y = Mathf.Min(min.y, local.y);
            max.x = Mathf.Max(max.x, local.x);
            max.y = Mathf.Max(max.y, local.y);
        }
    }

    private void MigrateLegacyTarget()
    {
        if (legacyTarget == null) return;
        if (targets == null) targets = new List<RectTransform>();
        if (!targets.Contains(legacyTarget)) targets.Insert(0, legacyTarget);
        legacyTarget = null;
    }

    private void CacheTargets()
    {
        Dictionary<RectTransform, TrackedTarget> previous = new Dictionary<RectTransform, TrackedTarget>(tracked.Count);
        for (int i = 0; i < tracked.Count; i++)
        {
            TrackedTarget item = tracked[i];
            if (item.rect != null) previous[item.rect] = item;
        }

        tracked.Clear();
        HashSet<RectTransform> seen = new HashSet<RectTransform>();

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                AddTracked(targets[i], seen, previous);
            }
        }

        if (tracked.Count == 0)
        {
            AddTracked(transform as RectTransform, seen, previous);
        }

        EnsureHomes();
    }

    private void AddTracked(RectTransform rect, HashSet<RectTransform> seen, Dictionary<RectTransform, TrackedTarget> previous)
    {
        if (rect == null || !seen.Add(rect)) return;
        if (rect.GetComponent<CanvasScaler>() != null) return;

        TrackedTarget item;
        if (previous != null && previous.TryGetValue(rect, out TrackedTarget existing))
        {
            item = existing;
        }
        else
        {
            item = new TrackedTarget { rect = rect };
        }

        item.rect = rect;
        tracked.Add(item);
    }

    private void CaptureHome()
    {
        currentZoom = 1f;
        for (int i = 0; i < tracked.Count; i++)
        {
            CaptureHome(tracked[i], true);
        }
    }

    private void EnsureHomes()
    {
        for (int i = 0; i < tracked.Count; i++)
        {
            CaptureHome(tracked[i], false);
        }
    }

    private static void CaptureHome(TrackedTarget item, bool overwrite)
    {
        if (item == null || item.rect == null) return;
        if (!overwrite && !IsNearZeroScale(item.homeLocalScale)) return;

        item.homeAnchoredPosition = item.rect.anchoredPosition;
        Vector3 scale = item.rect.localScale;
        item.homeLocalScale = IsNearZeroScale(scale) ? Vector3.one : scale;
    }

    private static void TranslateWorldXy(RectTransform rect, Vector3 worldDelta)
    {
        if (rect == null) return;
        RectTransform parent = rect.parent as RectTransform;
        if (parent == null) return;
        Vector3 localDelta = parent.InverseTransformVector(worldDelta);
        TranslateLocalXy(rect, new Vector2(localDelta.x, localDelta.y));
    }

    private static void TranslateLocalXy(RectTransform rect, Vector2 parentLocalDelta)
    {
        if (rect == null) return;
        Vector3 localPosition = rect.localPosition;
        localPosition.x += parentLocalDelta.x;
        localPosition.y += parentLocalDelta.y;
        rect.localPosition = localPosition;
    }

    private static bool IsUsableBounds(RectTransform bounds)
    {
        if (bounds == null) return false;
        Vector3 scale = bounds.lossyScale;
        if (Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f) return false;
        Rect rect = bounds.rect;
        return rect.width > 0.5f && rect.height > 0.5f;
    }

    private static bool IsNearZeroScale(Vector3 scale)
    {
        return Mathf.Abs(scale.x) < 0.0001f || Mathf.Abs(scale.y) < 0.0001f;
    }

    private RectTransform ResolveLimitSource()
    {
        if (panLimitTarget != null) return panLimitTarget;
        for (int i = 0; i < tracked.Count; i++)
        {
            if (tracked[i].rect != null) return tracked[i].rect;
        }

        return null;
    }

    private bool ShouldIgnore(PointerEventData eventData)
    {
        if (!ignoreWhenOverSelectable || eventData == null) return false;

        GameObject go = eventData.pointerCurrentRaycast.gameObject;
        if (go == null) go = eventData.pointerPress;
        if (go == null) go = eventData.pointerEnter;
        if (go == null) return false;

        Selectable selectable = go.GetComponentInParent<Selectable>();
        if (selectable == null) return false;
        return selectable.gameObject != gameObject;
    }

    private bool ContainsScreenPoint(Vector2 screenPosition, Camera cam)
    {
        RectTransform self = transform as RectTransform;
        if (self != null && RectTransformUtility.RectangleContainsScreenPoint(self, screenPosition, cam))
        {
            return true;
        }

        for (int i = 0; i < tracked.Count; i++)
        {
            RectTransform rect = tracked[i].rect;
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, cam))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshCanvas()
    {
        canvas = GetComponentInParent<Canvas>();
        eventCamera = null;
    }

    private Camera EventCamera()
    {
        if (eventCamera != null) return eventCamera;
        if (canvas == null) RefreshCanvas();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera;
    }
}
