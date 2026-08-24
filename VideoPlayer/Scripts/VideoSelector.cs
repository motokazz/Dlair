using UnityEngine;
using UnityEngine.Video;
using UnityEngine.Events;
using System.Collections;

public class VideoSelector : MonoBehaviour
{
    public enum PlaybackMode { Normal, Interactive, Manual }

    [Header("Node Graph")]
    public MediaGraph mediaGraph; // ★プレイリストではなくグラフ！

    [Header("References")]
    public DualVideoPlayer dualPlayer;
    public VideoClip dummyClip;

    [Header("Events")]
    public UnityEvent<MediaNode> onCustomEventTriggered; // ★MediaNodeに変更

    private MediaNode currentNode; // ★現在のノードを記憶
    private PlaybackMode currentMode = PlaybackMode.Normal;
    private Coroutine monitorCoroutine;

    private void Start()
    {
        // ==========================================
        // ★修正：エラーを分かりやすくし、Start Nodeを自動で探す親切設計に
        // ==========================================
        if (mediaGraph == null)
        {
            Debug.LogError("【エラー】VideoSelectorに『Media Graph』がセットされていません！インスペクターでグラフをアタッチしてください。");
            return;
        }

        // Start Nodeが設定されていない場合の自動修復
        if (mediaGraph.startNode == null)
        {
            foreach (var node in mediaGraph.nodes)
            {
                if (node is MediaNode mediaNode)
                {
                    mediaGraph.startNode = mediaNode;
                    Debug.LogWarning("【お知らせ】Start Nodeが未設定だったため、自動的に最初のノードを開始地点にしました。");
                    break;
                }
            }

            // それでも無ければエラー
            if (mediaGraph.startNode == null)
            {
                Debug.LogError("【エラー】グラフの中にMedia Nodeが一つもありません！ノード画面を開いて動画ノードを作成してください。");
                return;
            }
        }

        currentNode = mediaGraph.startNode;
        currentMode = (currentNode.eventId != "None") ? PlaybackMode.Interactive : PlaybackMode.Normal;

        if (dummyClip != null) StartCoroutine(StartupSequence());
        else
        {
            dualPlayer.PlayFirstMedia(currentNode.isStaticImage, currentNode.clip, currentNode.image, currentNode.isLooping, currentNode.audioClip);
            StartPlaybackMonitor();
        }
    }

    private IEnumerator StartupSequence()
    {
        yield return dualPlayer.WarmUpDecoder(dummyClip);
        float fadeDuration = mediaGraph.defaultCrossfadeDuration;

        dualPlayer.RequestPlayNextMedia(currentNode.isStaticImage, currentNode.clip, currentNode.image, currentNode.isLooping, currentNode.audioClip, fadeDuration);
        StartPlaybackMonitor();
    }

    // ==========================================
    // ★ノードの「Next」の線に沿って進む処理
    // ==========================================
    public void PlayNextNode()
    {
        if (currentNode == null) return;

        MediaNode nextNode = currentNode.GetNextNode();
        if (nextNode != null) ExecutePlayNode(nextNode);
    }

    // ==========================================
    // ★選択肢などから特定の線を辿って進む処理
    // ==========================================
    public void ProceedToBranch(int choiceIndex)
    {
        if (currentNode == null) return;

        MediaNode targetNode = currentNode.GetBranchTarget(choiceIndex);
        if (targetNode != null) ExecutePlayNode(targetNode);
        else PlayNextNode(); // 線が繋がっていなければ通常遷移にフォールバック
    }

    private void ExecutePlayNode(MediaNode node)
    {
        StopPlaybackMonitor();

        float fadeDuration = mediaGraph.defaultCrossfadeDuration;
        if (currentNode != null && currentNode.overrideCrossfade)
        {
            fadeDuration = currentNode.customCrossfadeDuration;
        }

        currentNode = node;
        currentMode = (currentNode.eventId != "None") ? PlaybackMode.Interactive : PlaybackMode.Normal;

        dualPlayer.RequestPlayNextMedia(currentNode.isStaticImage, currentNode.clip, currentNode.image, currentNode.isLooping, currentNode.audioClip, fadeDuration);

        StartPlaybackMonitor();
    }

    private void StartPlaybackMonitor()
    {
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = StartCoroutine(PlaybackMonitorRoutine());
    }

    private void StopPlaybackMonitor()
    {
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = null;
    }

    private IEnumerator PlaybackMonitorRoutine()
    {
        while (dualPlayer.IsTransitioning) yield return null;

        float currentOverlap = currentNode.overrideCrossfade ? currentNode.customCrossfadeDuration : mediaGraph.defaultCrossfadeDuration;
        float triggerTime = Mathf.Max(currentOverlap, 0.1f);
        bool useCustomTriggerTime = currentNode.eventTriggerTime > 0f;

        if (currentNode.isStaticImage)
        {
            float waitTime = useCustomTriggerTime ? currentNode.eventTriggerTime : Mathf.Max(0, currentNode.imageDuration - triggerTime);
            yield return new WaitForSeconds(waitTime);
            CheckBranchesOrPlayNext(currentNode);
        }
        else
        {
            VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
            if (activePlayer == null) yield break;
            while (activePlayer.length <= 0) yield return null;

            double totalTime = activePlayer.length;
            bool hasTriggered = false;
            float customTimer = 0f;

            while (!hasTriggered)
            {
                if (dualPlayer.IsTransitioning) yield break;
                double currentTime = activePlayer.time;

                if (useCustomTriggerTime)
                {
                    customTimer += Time.deltaTime;
                    if (customTimer >= currentNode.eventTriggerTime)
                    {
                        hasTriggered = true;
                        CheckBranchesOrPlayNext(currentNode);
                        yield break;
                    }
                }
                else
                {
                    if (currentTime > 0.1f && (totalTime - currentTime) <= triggerTime)
                    {
                        hasTriggered = true;
                        CheckBranchesOrPlayNext(currentNode);
                        yield break;
                    }

                    if (currentTime > (totalTime / 2.0) && !activePlayer.isPlaying)
                    {
                        hasTriggered = true;
                        CheckBranchesOrPlayNext(currentNode);
                        yield break;
                    }
                }
                yield return null;
            }
        }
    }

    private void CheckBranchesOrPlayNext(MediaNode node)
    {
        if (currentMode == PlaybackMode.Manual) return;

        if (currentMode == PlaybackMode.Interactive)
        {
            if (node.eventId != "None")
            {
                if (node.eventId == "AutoBranchController")
                {
                    ProceedToBranch(0); // 最初の分岐線を辿る
                    return;
                }

                if (onCustomEventTriggered != null)
                {
                    onCustomEventTriggered.Invoke(node);
                }
                return;
            }
        }
        PlayNextNode();
    }
}