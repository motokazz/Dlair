using UnityEngine;

/// <summary>
/// プールへ戻した瞬間に StoryPlayer の台帳から外す。
/// 戻した実体を次の生成が再利用しても、前の Goto 対象のまま残さない。
/// </summary>
public class StorySpawnTracker : MonoBehaviour
{
    private StoryPlayer player;

    public void Bind(StoryPlayer owner)
    {
        player = owner;
    }

    private void OnDisable()
    {
        if (player == null) return;
        if (GetComponent<PooledObject>() == null) return;
        player.UnregisterSpawned(gameObject);
    }
}
