using System.Collections;
using UnityEngine;

public class AutoRelease : MonoBehaviour
{
    [Tooltip("秒後にプールへ戻す。パーティクル待ちがオンなら使わない")]
    public float lifetime = 1.5f;

    [Tooltip("オン: 子の ParticleSystem が全て消えてから戻す")]
    public bool waitForParticles;

    private Coroutine routine;

    private void OnEnable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReleaseRoutine());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private IEnumerator ReleaseRoutine()
    {
        if (waitForParticles)
        {
            yield return null;
            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            while (AnyAlive(systems))
            {
                yield return null;
            }
        }
        else
        {
            if (lifetime > 0f) yield return new WaitForSeconds(lifetime);
            else yield return null;
        }

        routine = null;
        PooledObject pooled = GetComponent<PooledObject>();
        if (pooled != null) pooled.Release();
        else Destroy(gameObject);
    }

    private static bool AnyAlive(ParticleSystem[] systems)
    {
        if (systems == null) return false;
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null && systems[i].IsAlive(true)) return true;
        }
        return false;
    }
}
