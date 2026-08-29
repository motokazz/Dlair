using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class StoryFlowPlayer : MonoBehaviour
{
    [Header("新システムの設定")]
    public StoryFlowGraph flowGraph;
    public DualVideoPlayer dualPlayer;

    private BaseFlowNode currentNode; // 現在実行しているノード
    private Coroutine monitorCoroutine; // 監視用のコルーチン

    private void Start()
    {
        StartCoroutine(InitAndPlay());
    }

    private IEnumerator InitAndPlay()
    {
        if (flowGraph == null || dualPlayer == null) yield break;
        yield return null;

        BaseFlowNode startNode = flowGraph.nodes.Find(n => n is StartFlowNode);
        if (startNode != null) startNode.Execute(this);
    }

    // ★引数に node が追加されています
    public void PlayVideo(VideoClip clip, BaseFlowNode node)
    {
        if (clip == null) return;
        currentNode = node; // 現在のノードを記憶しておく

        VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
        if (activePlayer == null || !activePlayer.isPlaying)
        {
            dualPlayer.PlayFirstMedia(false, clip, null, false, null);
        }
        else
        {
            // クロスフェード時間（とりあえず1.0秒に設定）
            dualPlayer.RequestPlayNextMedia(false, clip, null, false, null, 1.0f);
        }

        // 前の監視を止めて、新しい動画の監視をスタート！
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = StartCoroutine(PlaybackMonitorRoutine());
    }

    // ==========================================
    // ★追加：動画の終わりを監視する処理
    // ==========================================
    private IEnumerator PlaybackMonitorRoutine()
    {
        // フェード切り替え中が終わるまで待つ
        while (dualPlayer.IsTransitioning) yield return null;

        VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
        if (activePlayer == null) yield break;

        // 動画の長さが取得できるまで少し待つ
        while (activePlayer.length <= 0) yield return null;

        double totalTime = activePlayer.length;
        float triggerOverlap = 1.0f; // ★動画が終わる1.0秒前に次へ進む（クロスフェード用）

        bool hasTriggered = false;

        while (!hasTriggered)
        {
            if (dualPlayer.IsTransitioning) yield break; // すでに次へ行っていたら監視終了

            double currentTime = activePlayer.time;

            // 動画が終わりに近づいたかチェック！
            if (currentTime > 0.1f && (totalTime - currentTime) <= triggerOverlap)
            {
                hasTriggered = true;
                PlayNextNode(); // 次のノードへ！
                yield break;
            }

            // 保険：動画が最後までいって停止してしまった場合
            if (currentTime > (totalTime / 2.0) && !activePlayer.isPlaying)
            {
                hasTriggered = true;
                PlayNextNode();
                yield break;
            }

            yield return null;
        }
    }

    // 現在のノードから線（Next）を辿って、次のノードを実行する処理
    private void PlayNextNode()
    {
        if (currentNode == null) return;

        // グラフの機能を使って、現在のノードの "Next" に繋がっているノードを取得
        BaseFlowNode nextNode = flowGraph.GetNextNode(currentNode.guid, "Next");

        if (nextNode != null)
        {
            Debug.Log($"【進行】動画終了！次のノードへ進みます");
            nextNode.Execute(this); // ★バトンリレー！
        }
        else
        {
            Debug.Log("【進行終了】次に繋がっているノードがありません。");
        }
    }
}