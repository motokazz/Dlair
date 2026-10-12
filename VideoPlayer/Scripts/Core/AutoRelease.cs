using System.Collections;
using UnityEngine;

public class AutoRelease : MonoBehaviour
{
    [Tooltip("秒後にプールへ戻す。パーティクル待ちがオンなら使わない")]
    public float lifetime = 1.5f;

    [Tooltip("オン: 子の ParticleSystem が全て消えてから戻す")]
    public bool waitForParticles;

    private Coroutine routine;
    private bool useOverride;
    private float overrideLifetime;

    /// <summary>
    /// この表示だけ、パーティクル待ちを使わず指定秒で破棄する。プレハブに書いた lifetime は変えない。
    /// </summary>
    public void BeginLifetime(float seconds)
    {
        useOverride = true;
        overrideLifetime = Mathf.Max(0f, seconds);
        if (!isActiveAndEnabled) return;
        RestartRoutine();
    }

    private void OnEnable()
    {
        RestartRoutine();
    }

    private void OnDisable()
    {
        useOverride = false;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void RestartRoutine()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReleaseRoutine());
    }

    private IEnumerator ReleaseRoutine()
    {
        bool timed = useOverride || !waitForParticles;
        float seconds = useOverride ? overrideLifetime : lifetime;
        useOverride = false;

        if (!timed)
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
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
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
