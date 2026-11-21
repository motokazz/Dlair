using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 属性名と、そのカーソル画像。スプライトがあればテクスチャより優先する。
/// </summary>
[System.Serializable]
public class UiCursorAttributeEntry
{
    [Tooltip("ボタンのカーソル属性と同じ名前")]
    public string attributeName;
    [Tooltip("スプライトがあればこちらを優先します。両方空ならボタンオーバーの画像です")]
    public Sprite sprite;
    public Texture2D texture;
    public Vector2 hotspot = new Vector2(8f, 4f);
}

/// <summary>
/// ボタンの上でカーソルを変える。属性があればその画像、無ければ共通のボタンオーバー画像。
/// UI 描画と、OS カーソルの置き換えを選べる。
/// </summary>
public class UiCursorController : MonoBehaviour
{
    [Header("通常")]
    [Tooltip("スプライトがあればこちらを優先します。両方空ならOSの矢印です")]
    public Sprite defaultCursorSprite;
    public Texture2D defaultCursor;
    public Vector2 defaultHotspot;

    [Header("ボタンオーバー")]
    [Tooltip("スプライトがあればこちらを優先します。両方空なら内蔵ポインターです")]
    public Sprite hoverCursorSprite;
    public Texture2D hoverCursor;
    public Vector2 hoverHotspot = new Vector2(8f, 4f);
    public Color hoverTint = Color.white;
    [Tooltip("ソフトウェア表示のサイズ（px）。ハードウェアカーソルには影響しません")]
    public float hoverDisplaySize = 48f;

    [Header("属性")]
    [Tooltip("属性名ごとのカーソル画像。ボタンの UiCursorAttribute と同じ名前を書きます")]
    public List<UiCursorAttributeEntry> attributeCursors = new List<UiCursorAttributeEntry>();

    [Header("判定")]
    [Tooltip("Button 以外の Selectable（Toggle など）でも変える")]
    public bool includeOtherSelectables;

    [Header("表示方式")]
    [Tooltip("ON: UIで描画（Gameビュー向き） / OFF: OSカーソルを置き換え")]
    public bool useSoftwareCursor = true;
    [Tooltip("ONなら Hotspot を使う。OFFならスプライトの Pivot（テクスチャは左上）")]
    public bool overrideHotspot;

    private readonly List<RaycastResult> raycastHits = new List<RaycastResult>();
    private readonly List<Object> runtimeOwned = new List<Object>();
    private Texture2D builtInHover;
    private Sprite builtInHoverSprite;
    private Sprite defaultTextureSprite;
    private Sprite hoverTextureSprite;
    private Texture2D defaultHardwareTexture;
    private Texture2D hoverHardwareTexture;
    private readonly Dictionary<UiCursorAttributeEntry, Sprite> attributeTextureSprites = new Dictionary<UiCursorAttributeEntry, Sprite>();
    private readonly Dictionary<UiCursorAttributeEntry, Texture2D> attributeHardwareTextures = new Dictionary<UiCursorAttributeEntry, Texture2D>();
    private bool hovering;
    private string hoveringAttribute;
    private Canvas canvas;
    private RectTransform canvasRect;
    private Camera eventCamera;
    private RectTransform softwareRect;
    private Image softwareImage;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas != null) canvasRect = canvas.transform as RectTransform;
        RefreshEventCamera();
        EnsureCursorAssets();
        if (useSoftwareCursor) EnsureSoftwareCursor();
    }

    private void OnEnable()
    {
        RefreshEventCamera();
        ApplyCursor(false);
    }

    private void OnDisable()
    {
        RestoreOsCursor();
        if (softwareRect != null) softwareRect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        RestoreOsCursor();
        for (int i = 0; i < runtimeOwned.Count; i++)
        {
            if (runtimeOwned[i] != null) Destroy(runtimeOwned[i]);
        }

        runtimeOwned.Clear();
    }

    private void LateUpdate()
    {
        bool nextHover = TryGetPointerTarget(out string nextAttribute);
        if (!nextHover) nextAttribute = null;
        if (nextHover != hovering || nextAttribute != hoveringAttribute)
        {
            hovering = nextHover;
            hoveringAttribute = nextAttribute;
            ApplyCursor(hovering, hoveringAttribute);
        }
        else if (useSoftwareCursor && (hovering || HasDefaultGraphic()))
        {
            PositionSoftwareCursor();
        }
    }

    private bool TryGetPointerTarget(out string attributeName)
    {
        attributeName = null;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = GetPointerScreenPosition()
        };

        raycastHits.Clear();
        eventSystem.RaycastAll(pointer, raycastHits);

        for (int i = 0; i < raycastHits.Count; i++)
        {
            GameObject hit = raycastHits[i].gameObject;
            if (hit == null) continue;
            if (softwareRect != null && (hit == softwareRect.gameObject || hit.transform.IsChildOf(softwareRect))) continue;

            Selectable selectable = hit.GetComponentInParent<Selectable>();
            if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable()) continue;
            if (!includeOtherSelectables && selectable is not Button) continue;

            UiCursorAttribute attribute = selectable.GetComponent<UiCursorAttribute>();
            if (attribute != null && !string.IsNullOrEmpty(attribute.attributeName))
            {
                attributeName = attribute.attributeName;
            }

            return true;
        }

        return false;
    }

    private void ApplyCursor(bool hover, string attributeName = null)
    {
        if (useSoftwareCursor)
        {
            EnsureSoftwareCursor();
            bool showSoftware = hover || HasDefaultGraphic();
            Cursor.visible = !showSoftware;
            if (!showSoftware)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                Cursor.visible = false;
            }

            if (softwareRect != null)
            {
                softwareRect.gameObject.SetActive(showSoftware);
                if (showSoftware)
                {
                    ApplySoftwareVisual(hover, attributeName);
                    PositionSoftwareCursor();
                }
            }

            return;
        }

        if (softwareRect != null) softwareRect.gameObject.SetActive(false);
        Cursor.visible = true;
        if (hover)
        {
            ResolveHover(attributeName, out Sprite sourceSprite, out Texture2D sourceTexture, out Vector2 sourceHotspot, out UiCursorAttributeEntry entry);
            Cursor.SetCursor(
                GetHoverHardwareTexture(sourceSprite, sourceTexture, entry),
                GetHotspot(true, sourceSprite, sourceTexture, sourceHotspot),
                CursorMode.Auto);
        }
        else if (HasDefaultGraphic())
        {
            Cursor.SetCursor(GetDefaultHardwareTexture(), GetHotspot(false, defaultCursorSprite, defaultCursor, defaultHotspot), CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void ApplySoftwareVisual(bool hover, string attributeName)
    {
        Sprite sourceSprite;
        Texture2D sourceTexture;
        Vector2 sourceHotspot;
        Sprite sprite;
        if (hover)
        {
            ResolveHover(attributeName, out sourceSprite, out sourceTexture, out sourceHotspot, out UiCursorAttributeEntry entry);
            sprite = GetHoverSoftwareSprite(sourceSprite, sourceTexture, sourceHotspot, entry);
        }
        else
        {
            sourceSprite = defaultCursorSprite;
            sourceTexture = defaultCursor;
            sourceHotspot = defaultHotspot;
            sprite = GetDefaultSoftwareSprite();
        }

        softwareImage.sprite = sprite;
        softwareImage.preserveAspect = true;
        softwareImage.color = hover ? hoverTint : Color.white;
        softwareRect.sizeDelta = FitSize(sprite, hoverDisplaySize);
        softwareRect.pivot = GetSoftwarePivot(hover, sprite, sourceSprite, sourceTexture, sourceHotspot);
    }

    private void ResolveHover(string attributeName, out Sprite sprite, out Texture2D texture, out Vector2 hotspot, out UiCursorAttributeEntry entry)
    {
        entry = FindAttribute(attributeName);
        if (entry != null && (entry.sprite != null || entry.texture != null))
        {
            sprite = entry.sprite;
            texture = entry.texture;
            hotspot = entry.hotspot;
            return;
        }

        entry = null;
        sprite = hoverCursorSprite;
        texture = hoverCursor;
        hotspot = hoverHotspot;
    }

    private UiCursorAttributeEntry FindAttribute(string attributeName)
    {
        if (string.IsNullOrEmpty(attributeName) || attributeCursors == null) return null;

        for (int i = 0; i < attributeCursors.Count; i++)
        {
            UiCursorAttributeEntry entry = attributeCursors[i];
            if (entry == null || string.IsNullOrEmpty(entry.attributeName)) continue;
            if (entry.attributeName == attributeName) return entry;
        }

        return null;
    }

    private void EnsureCursorAssets()
    {
        if (hoverCursorSprite == null && hoverCursor == null && builtInHover == null)
        {
            builtInHover = CreateBuiltInPointer();
            Own(builtInHover);
            builtInHoverSprite = CreateSprite(builtInHover, new Vector2(8f, 4f));
            Own(builtInHoverSprite);
        }

        if (defaultCursorSprite == null && defaultCursor != null)
        {
            defaultTextureSprite = CreateSprite(defaultCursor, defaultHotspot);
            Own(defaultTextureSprite);
        }

        if (hoverCursorSprite == null && hoverCursor != null)
        {
            hoverTextureSprite = CreateSprite(hoverCursor, hoverHotspot);
            Own(hoverTextureSprite);
        }
    }

    private bool HasDefaultGraphic()
    {
        return defaultCursorSprite != null || defaultCursor != null;
    }

    private Sprite GetHoverSoftwareSprite(Sprite sourceSprite, Texture2D sourceTexture, Vector2 sourceHotspot, UiCursorAttributeEntry entry)
    {
        if (sourceSprite != null) return sourceSprite;
        if (sourceTexture != null) return GetTextureSprite(sourceTexture, sourceHotspot, entry);
        return builtInHoverSprite;
    }

    private Sprite GetDefaultSoftwareSprite()
    {
        if (defaultCursorSprite != null) return defaultCursorSprite;
        return defaultTextureSprite;
    }

    private Texture2D GetHoverHardwareTexture(Sprite sourceSprite, Texture2D sourceTexture, UiCursorAttributeEntry entry)
    {
        if (sourceSprite != null) return GetSpriteHardwareTexture(sourceSprite, entry);
        if (sourceTexture != null) return sourceTexture;
        return builtInHover;
    }

    private Texture2D GetDefaultHardwareTexture()
    {
        if (defaultCursorSprite != null)
        {
            if (defaultHardwareTexture == null)
            {
                defaultHardwareTexture = CreateTextureFromSprite(defaultCursorSprite);
                Own(defaultHardwareTexture);
            }

            return defaultHardwareTexture;
        }

        return defaultCursor;
    }

    private Sprite GetTextureSprite(Texture2D sourceTexture, Vector2 sourceHotspot, UiCursorAttributeEntry entry)
    {
        if (entry != null)
        {
            if (!attributeTextureSprites.TryGetValue(entry, out Sprite cached) || cached == null)
            {
                cached = CreateSprite(sourceTexture, sourceHotspot);
                Own(cached);
                attributeTextureSprites[entry] = cached;
            }

            return cached;
        }

        if (hoverTextureSprite == null)
        {
            hoverTextureSprite = CreateSprite(sourceTexture, sourceHotspot);
            Own(hoverTextureSprite);
        }

        return hoverTextureSprite;
    }

    private Texture2D GetSpriteHardwareTexture(Sprite sourceSprite, UiCursorAttributeEntry entry)
    {
        if (entry != null)
        {
            if (!attributeHardwareTextures.TryGetValue(entry, out Texture2D cached) || cached == null)
            {
                cached = CreateTextureFromSprite(sourceSprite);
                Own(cached);
                attributeHardwareTextures[entry] = cached;
            }

            return cached;
        }

        if (hoverHardwareTexture == null)
        {
            hoverHardwareTexture = CreateTextureFromSprite(sourceSprite);
            Own(hoverHardwareTexture);
        }

        return hoverHardwareTexture;
    }

    private Vector2 GetHotspot(bool hover, Sprite sourceSprite, Texture2D sourceTexture, Vector2 custom)
    {
        if (sourceSprite != null && !overrideHotspot)
        {
            return SpritePivotToCursorHotspot(sourceSprite);
        }

        if (hover && sourceSprite == null && sourceTexture == null)
        {
            return new Vector2(8f, 4f);
        }

        return custom;
    }

    private Vector2 GetSoftwarePivot(bool hover, Sprite sprite, Sprite sourceSprite, Texture2D sourceTexture, Vector2 sourceHotspot)
    {
        if (sprite == null) return new Vector2(0f, 1f);
        if (!overrideHotspot && sourceSprite != null)
        {
            Rect rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f) return new Vector2(0f, 1f);
            return new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
        }

        Vector2 hotspot = GetHotspot(hover, sourceSprite, sourceTexture, sourceHotspot);
        float width = sprite.rect.width;
        float height = sprite.rect.height;
        if (width <= 0f || height <= 0f) return new Vector2(0f, 1f);
        return new Vector2(hotspot.x / width, 1f - hotspot.y / height);
    }

    private void EnsureSoftwareCursor()
    {
        if (softwareRect != null || canvasRect == null) return;

        GameObject go = new GameObject("UiHoverCursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(canvasRect, false);
        softwareRect = go.GetComponent<RectTransform>();
        softwareRect.anchorMin = new Vector2(0.5f, 0.5f);
        softwareRect.anchorMax = new Vector2(0.5f, 0.5f);
        softwareRect.sizeDelta = Vector2.one * hoverDisplaySize;
        softwareImage = go.GetComponent<Image>();
        softwareImage.raycastTarget = false;
        softwareImage.preserveAspect = true;
        go.SetActive(false);
        go.transform.SetAsLastSibling();
    }

    private void PositionSoftwareCursor()
    {
        if (softwareRect == null || canvasRect == null) return;

        softwareRect.SetAsLastSibling();
        RefreshEventCamera();
        Vector2 screen = GetPointerScreenPosition();
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCamera, out Vector2 local))
        {
            softwareRect.anchoredPosition = local;
        }
    }

    private void RefreshEventCamera()
    {
        if (canvas == null)
        {
            eventCamera = null;
            return;
        }

        eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void RestoreOsCursor()
    {
        Cursor.visible = true;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        hovering = false;
        hoveringAttribute = null;
    }

    private void Own(Object asset)
    {
        if (asset != null) runtimeOwned.Add(asset);
    }

    private static Vector2 FitSize(Sprite sprite, float target)
    {
        if (sprite == null) return Vector2.one * target;
        float width = Mathf.Max(1f, sprite.rect.width);
        float height = Mathf.Max(1f, sprite.rect.height);
        if (width >= height)
        {
            return new Vector2(target, target * (height / width));
        }

        return new Vector2(target * (width / height), target);
    }

    private static Vector2 SpritePivotToCursorHotspot(Sprite sprite)
    {
        return new Vector2(sprite.pivot.x, sprite.rect.height - sprite.pivot.y);
    }

    private static Sprite CreateSprite(Texture2D tex, Vector2 hotspotPixels)
    {
        if (tex == null) return null;
        Vector2 pivot = new Vector2(
            tex.width > 0 ? hotspotPixels.x / tex.width : 0f,
            tex.height > 0 ? 1f - hotspotPixels.y / tex.height : 1f);
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, 100f);
    }

    private static Texture2D CreateTextureFromSprite(Sprite sprite)
    {
        if (sprite == null) return null;

        Texture source = sprite.texture;
        Rect texRect = sprite.textureRect;
        int width = Mathf.Max(1, Mathf.RoundToInt(texRect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(texRect.height));

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = sprite.name + "_CursorTex"
        };
        copy.ReadPixels(new Rect(texRect.x, texRect.y, width, height), 0, 0);
        copy.Apply(false, false);

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private static Texture2D CreateBuiltInPointer()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "UiHoverCursor_BuiltIn"
        };

        Color clear = Color.clear;
        Color outline = new Color(0.08f, 0.08f, 0.08f, 1f);
        Color fill = Color.white;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        int[] rows =
        {
            0b00000000000000000000000000000000,
            0b01000000000000000000000000000000,
            0b01100000000000000000000000000000,
            0b01110000000000000000000000000000,
            0b01111000000000000000000000000000,
            0b01111100000000000000000000000000,
            0b01111110000000000000000000000000,
            0b01111111000000000000000000000000,
            0b01111111100000000000000000000000,
            0b01111111110000000000000000000000,
            0b01111111111000000000000000000000,
            0b01111111111100000000000000000000,
            0b01111111000000000000000000000000,
            0b01111111000000000000000000000000,
            0b01101111100000000000000000000000,
            0b01001111100000000000000000000000,
            0b00000111110000000000000000000000,
            0b00000111110000000000000000000000,
            0b00000011111000000000000000000000,
            0b00000011111000000000000000000000,
            0b00000001111100000000000000000000,
            0b00000000111100000000000000000000,
            0b00000000111000000000000000000000,
            0b00000000010000000000000000000000,
        };

        for (int row = 0; row < rows.Length; row++)
        {
            int bits = rows[row];
            int y = size - 1 - row;
            for (int x = 0; x < size; x++)
            {
                if (((bits >> (31 - x)) & 1) == 0) continue;
                pixels[y * size + x] = fill;
            }
        }

        Outline(pixels, size, outline);
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static void Outline(Color[] pixels, int size, Color outline)
    {
        bool[] filled = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++) filled[i] = pixels[i].a > 0.5f;

        int[] ox = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] oy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (filled[i]) continue;
                bool neighbor = false;
                for (int k = 0; k < ox.Length; k++)
                {
                    int nx = x + ox[k];
                    int ny = y + oy[k];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    if (!filled[ny * size + nx]) continue;
                    neighbor = true;
                    break;
                }

                if (neighbor) pixels[i] = outline;
            }
        }
    }

    private static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            return UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        }

        if (UnityEngine.InputSystem.Touchscreen.current != null)
        {
            return UnityEngine.InputSystem.Touchscreen.current.primaryTouch.position.ReadValue();
        }

        if (UnityEngine.InputSystem.Pointer.current != null)
        {
            return UnityEngine.InputSystem.Pointer.current.position.ReadValue();
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }
}
