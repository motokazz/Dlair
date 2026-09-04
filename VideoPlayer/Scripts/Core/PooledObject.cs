using UnityEngine;

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
