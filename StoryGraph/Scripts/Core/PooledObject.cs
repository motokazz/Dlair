using UnityEngine;

/// <summary>
/// プールから出した実体。有効化で子のパーティクルを再生し、Release でプールへ戻す。
/// </summary>
public class PooledObject : MonoBehaviour
{
    public GameObject OriginPrefab { get; set; }

    public void Release()
    {
        PrefabPool.Release(gameObject);
    }

    private void OnEnable()
    {
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Clear(true);
            systems[i].Play(true);
        }
    }
}
