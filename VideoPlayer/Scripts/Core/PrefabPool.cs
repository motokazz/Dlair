using System.Collections.Generic;
using UnityEngine;

public class PrefabPool : MonoBehaviour
{
    private static PrefabPool instance;

    private readonly Dictionary<GameObject, Stack<GameObject>> unused = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> originByInstance = new Dictionary<GameObject, GameObject>();
    private readonly HashSet<GameObject> resting = new HashSet<GameObject>();
    private Transform inactiveRoot;

    public static PrefabPool Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("[PrefabPool]");
                instance = host.AddComponent<PrefabPool>();
                DontDestroyOnLoad(host);
            }

            return instance;
        }
    }

    public static GameObject Spawn(GameObject prefab, Transform parent = null)
    {
        if (prefab == null) return null;
        return Instance.SpawnInternal(prefab, parent);
    }

    public static void Release(GameObject instance)
    {
        if (instance == null) return;
        Instance.ReleaseInternal(instance);
    }

    public static void Release(PooledObject pooled)
    {
        if (pooled == null) return;
        Release(pooled.gameObject);
    }

    private GameObject SpawnInternal(GameObject prefab, Transform parent)
    {
        EnsureInactiveRoot();

        GameObject spawned = null;
        if (unused.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            while (stack.Count > 0 && spawned == null)
            {
                GameObject candidate = stack.Pop();
                if (candidate != null) spawned = candidate;
            }
        }

        if (spawned == null)
        {
            spawned = parent != null ? Instantiate(prefab, parent, false) : Instantiate(prefab);
            BindOrigin(spawned, prefab);
            AlignSpawnedUi(spawned, parent);
            spawned.SetActive(true);
            return spawned;
        }

        BindOrigin(spawned, prefab);
        resting.Remove(spawned);
        if (parent != null) spawned.transform.SetParent(parent, false);
        AlignSpawnedUi(spawned, parent);
        spawned.SetActive(true);
        return spawned;
    }

    public static void AlignSpawnedUi(GameObject instance, Transform parent)
    {
        if (instance == null || parent is not RectTransform) return;

        if (instance.transform is not RectTransform)
        {
            Debug.LogWarning($"【UI生成】{instance.name} の RectTransform が失われていたため、付け直しました。");
            instance.AddComponent<RectTransform>();
        }

        // localPosition を代入するとアンカーごと動いて、ギズモが映像から外れる。
        // Z だけ anchoredPosition3D で戻し、画面上の位置は維持する。
        RectTransform[] rects = instance.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            Vector3 anchored = rect.anchoredPosition3D;
            if (Mathf.Abs(anchored.z) < 0.001f) continue;
            anchored.z = 0f;
            rect.anchoredPosition3D = anchored;
        }
    }

    private void ReleaseInternal(GameObject instance)
    {
        if (!originByInstance.TryGetValue(instance, out GameObject prefab) || prefab == null)
        {
            Destroy(instance);
            return;
        }

        if (!resting.Add(instance)) return;

        if (!unused.TryGetValue(prefab, out Stack<GameObject> stack))
        {
            stack = new Stack<GameObject>();
            unused[prefab] = stack;
        }

        instance.SetActive(false);
        instance.transform.SetParent(inactiveRoot, false);
        stack.Push(instance);
    }

    private void BindOrigin(GameObject instance, GameObject prefab)
    {
        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null) pooled = instance.AddComponent<PooledObject>();
        pooled.OriginPrefab = prefab;
        originByInstance[instance] = prefab;
    }

    private void EnsureInactiveRoot()
    {
        if (inactiveRoot != null) return;

        GameObject root = new GameObject("Inactive");
        root.transform.SetParent(transform, false);
        root.SetActive(false);
        inactiveRoot = root.transform;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
