using UnityEngine;

public class SpawnPrefabNode : BaseNode
{
    [Header("生成するプレハブ")]
    public GameObject prefab;

    [Header("配置")]
    [Tooltip("オン: いまのポインタ位置に出す（Choice のクリック位置など）")]
    public bool spawnAtPointer = true;

    [Tooltip("オン: Canvas 配下の UI として出す / オフ: ワールド座標")]
    public bool parentToCanvas = true;

    [Tooltip("ワールド生成時、カメラからこの距離の位置に出す")]
    public float worldDistance = 10f;

    public override void Execute(StoryPlayer player)
    {
        if (prefab == null)
        {
            Debug.LogError("【Spawn Prefab】プレハブが設定されていません！");
            player.ContinueTo(this, "Next");
            return;
        }

        Transform parent = null;
        if (parentToCanvas)
        {
            Canvas canvas = player != null ? player.GetUICanvas() : null;
            if (canvas == null)
            {
                Debug.LogError("【Spawn Prefab】Canvas が見つかりません！");
                player.ContinueTo(this, "Next");
                return;
            }
            parent = canvas.transform;
        }

        GameObject instance = PrefabPool.Spawn(prefab, parent);
        if (instance != null && spawnAtPointer)
        {
            PlaceAtPointer(instance, parentToCanvas ? parent as RectTransform : null);
        }

        player.ContinueTo(this, "Next");
    }

    public string GetDisplayTitle()
    {
        return prefab != null ? prefab.name : "Spawn";
    }

    private void PlaceAtPointer(GameObject instance, RectTransform canvasRect)
    {
        Vector2 screen = GetPointerScreenPosition();

        if (canvasRect != null)
        {
            RectTransform rt = instance.transform as RectTransform;
            if (rt == null) return;

            Canvas canvas = canvasRect.GetComponent<Canvas>();
            Camera eventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, eventCam, out Vector2 local))
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = local;
            }
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, worldDistance));
        instance.transform.position = world;
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
