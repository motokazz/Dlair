using System.Collections;
using UnityEngine;

/// <summary>
/// 指定した秒数（Sec）が経過するまで Next に進まないフローノード。
/// 経過時間は毎フレーム Time.deltaTime を積算して測る。フレームレートや PC の処理速度には依存せず、
/// Time.timeScale が 0 のときは待機も止まる。
/// </summary>
public class WaitNode : BaseNode
{
    [Tooltip("Next に進むまでの待ち時間（秒）。Time.deltaTime の積算で計測する。")]
    public float sec = 1f;

    [System.NonSerialized]
    private Coroutine routine;

    [System.NonSerialized]
    private int runId;

    /// <summary>
    /// Sec 秒だけ待ってから Next へ進む。0 以下なら待たずに Next へ進む。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        if (player == null) return;

        StopWait(player);

        float duration = Mathf.Max(0f, sec);
        if (duration <= 0f)
        {
            player.ContinueTo(this, "Next");
            return;
        }

        int id = ++runId;
        routine = player.StartCoroutine(WaitThenContinue(player, duration, id));
    }

    /// <summary>
    /// 進行中の待機を打ち切る。Goto などでこのノードを離れたあと、古い待機が Next を発火しないようにする。
    /// </summary>
    public void StopWait(StoryPlayer player)
    {
        runId++;
        if (routine == null) return;

        if (player != null)
        {
            player.StopCoroutine(routine);
        }

        routine = null;
    }

    private IEnumerator WaitThenContinue(StoryPlayer player, float duration, int id)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsCurrentRun(player, id))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                break;
            }

            yield return null;
        }

        if (routine != null && id == runId)
        {
            routine = null;
        }

        if (!IsCurrentRun(player, id))
        {
            yield break;
        }

        player.ContinueTo(this, "Next");
    }

    private bool IsCurrentRun(StoryPlayer player, int id)
    {
        return id == runId && player != null && player.CurrentNode == this;
    }

    /// <summary>
    /// グラフ上のタイトルに出す待ち時間。
    /// </summary>
    public string GetDisplayTitle()
    {
        return Mathf.Max(0f, sec).ToString("0.###") + "s";
    }
}
