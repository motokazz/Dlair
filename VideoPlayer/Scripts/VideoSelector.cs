using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class VideoSelector : MonoBehaviour
{
    [Header("Graph")]
    public StoryGraph storyGraph;

    [Header("References")]
    public DualVideoPlayer dualPlayer;
    public VideoClip dummyClip;

    [Header("UI (Branch Node)")]
    public GameObject choicesPanel;
    public Transform buttonContainer;
    public GameObject buttonPrefab;
    public TypewriterEffect typewriter;

    private BaseStoryNode currentNode;
    private Coroutine monitorCoroutine;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    private bool isFirstPlay = true;

    // ★追加：帰り道を覚えておくための「スタック（しおりのようなもの）」
    private Stack<BaseStoryNode> returnStack = new Stack<BaseStoryNode>();

    private void Start()
    {
        if (storyGraph == null)
        {
            Debug.LogError("【エラー】Story Graphがセットされていません！");
            return;
        }

        // ==========================================
        // ★変更：グラフ内から「StartNode」を自動で探し出す！
        // ==========================================
        BaseStoryNode firstNode = null;
        foreach (var node in storyGraph.nodes)
        {
            if (node is StartNode startNode)
            {
                firstNode = startNode;
                break;
            }
        }

        if (firstNode == null)
        {
            Debug.LogError("【エラー】グラフの中に『Start Node』が見つかりません！右クリックから作成してください。");
            return;
        }

        if (choicesPanel != null) choicesPanel.SetActive(false);

        if (dummyClip != null)
        {
            isFirstPlay = false;
            // 起動シーケンスへStartNodeを渡す
            StartCoroutine(StartupSequence(firstNode));
        }
        else
        {
            isFirstPlay = true;
            ExecuteNode(firstNode);
        }
    }

    // StartupSequence も引数を受け取るように少し修正します
    private IEnumerator StartupSequence(BaseStoryNode firstNode)
    {
        yield return dualPlayer.WarmUpDecoder(dummyClip);
        ExecuteNode(firstNode);
    }


    // ==========================================
    // ★大進化：ノードの実行（サブグラフから戻る機能を搭載）
    // ==========================================
    public void ExecuteNode(BaseStoryNode node)
    {
        if (node == null)
        {
            // ★変更：次に進むノードが無い場合、しおり（帰り道）が挟まっていればそこに戻る！
            if (returnStack.Count > 0)
            {
                BaseStoryNode returnNode = returnStack.Pop();
                if (returnNode != null)
                {
                    ExecuteNode(returnNode);
                    return;
                }
            }

            // 帰り道も無ければ、本当にストーリーが終了
            Debug.Log("【Story】ストーリーの完全な終端に到達しました。");
            return;
        }

        currentNode = node;
        node.Execute(this);
    }

    // ==========================================
    // ★追加：サブグラフに入るための専用メソッド
    // ==========================================
    public void EnterSubGraph(StoryGraph sub, BaseStoryNode returnNode)
    {
        if (sub == null)
        {
            Debug.LogWarning("サブグラフがセットされていません。スキップします。");
            ExecuteNode(returnNode);
            return;
        }

        // サブグラフの中から緑色のStartNodeを探す
        BaseStoryNode firstNode = null;
        foreach (var n in sub.nodes)
        {
            if (n is StartNode startNode)
            {
                firstNode = startNode;
                break;
            }
        }

        if (firstNode != null)
        {
            // 今の場所（帰り道）をしおりとして挟んでから、サブグラフのStartNodeへ飛ぶ！
            returnStack.Push(returnNode);
            ExecuteNode(firstNode);
        }
        else
        {
            Debug.LogError($"{sub.name} の中に Start Node がありません！スキップします。");
            ExecuteNode(returnNode);
        }
    }

    // ==========================================
    // メディア再生処理（一番最初か、トランジションかで分岐）
    // ==========================================
    public void PlayMedia(bool isStaticImage, VideoClip clip, Texture2D image, float duration, bool isLooping, AudioClip audioClip, float fade, BaseStoryNode node)
    {
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);

        if (isFirstPlay)
        {
            // ★追加：一番最初の再生の時だけ、専用の立ち上げメソッドを使う！
            isFirstPlay = false;
            dualPlayer.PlayFirstMedia(isStaticImage, clip, image, isLooping, audioClip);
        }
        else
        {
            // 2回目以降はクロスフェードで滑らかに繋ぐ
            dualPlayer.RequestPlayNextMedia(isStaticImage, clip, image, isLooping, audioClip, fade);
        }

        monitorCoroutine = StartCoroutine(PlaybackMonitorRoutine(isStaticImage, duration, fade, node));
    }

    private IEnumerator PlaybackMonitorRoutine(bool isStaticImage, float duration, float fade, BaseStoryNode node)
    {
        while (dualPlayer.IsTransitioning) yield return null;

        float triggerTime = Mathf.Max(fade, 0.1f);

        if (isStaticImage)
        {
            float waitTime = Mathf.Max(0, duration - triggerTime);
            yield return new WaitForSeconds(waitTime);
            ExecuteNode(node.GetNextNode("next"));
        }
        else
        {
            VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
            if (activePlayer == null) yield break;
            while (activePlayer.length <= 0) yield return null;

            bool useCustomTimer = duration > 0f;
            double targetTotalTime = useCustomTimer ? duration : activePlayer.length;
            float customTimer = 0f;

            while (true)
            {
                if (dualPlayer.IsTransitioning) yield break;

                if (useCustomTimer)
                {
                    customTimer += Time.deltaTime;
                    if (customTimer >= (targetTotalTime - triggerTime))
                    {
                        ExecuteNode(node.GetNextNode("next"));
                        yield break;
                    }
                }
                else
                {
                    double currentTime = activePlayer.time;
                    if (currentTime > 0.1f && (targetTotalTime - currentTime) <= triggerTime)
                    {
                        ExecuteNode(node.GetNextNode("next"));
                        yield break;
                    }
                    if (currentTime > (targetTotalTime / 2.0) && !activePlayer.isPlaying)
                    {
                        ExecuteNode(node.GetNextNode("next"));
                        yield break;
                    }
                }
                yield return null;
            }
        }
    }

    public void ShowChoices(BranchNode node)
    {
        if (choicesPanel != null) choicesPanel.SetActive(true);
        CleanupButtons();

        for (int i = 0; i < node.choices.Count; i++)
        {
            var choice = node.choices[i];

            if (!string.IsNullOrEmpty(choice.requiredParam))
            {
                if (GameManager.GetParameter(choice.requiredParam) < choice.requiredValue) continue;
            }

            GameObject newBtn = Instantiate(buttonPrefab, buttonContainer);
            newBtn.SetActive(true);
            spawnedButtons.Add(newBtn);

            TMP_Text tmpText = newBtn.GetComponentInChildren<TMP_Text>();
            if (tmpText != null) tmpText.text = choice.buttonText;

            Button btnComponent = newBtn.GetComponent<Button>();
            if (btnComponent != null)
            {
                int choiceIndex = i;
                btnComponent.onClick.AddListener(() =>
                {
                    CleanupButtons();
                    if (choicesPanel != null) choicesPanel.SetActive(false);
                    ExecuteNode(node.GetNextNode("choices " + choiceIndex));
                });
            }
        }

        if (typewriter != null && !string.IsNullOrEmpty(node.messageText))
        {
            typewriter.gameObject.SetActive(true);
            typewriter.Play(node.messageText, null);
        }

        if (spawnedButtons.Count == 0)
        {
            if (choicesPanel != null) choicesPanel.SetActive(false);
            ExecuteNode(node.GetNextNode("choices 0"));
        }
    }

    private void CleanupButtons()
    {
        foreach (var b in spawnedButtons) { if (b != null) Destroy(b); }
        spawnedButtons.Clear();
    }
    // ==========================================
    // ★追加：自作UI分岐ノードから呼ばれる処理
    // ==========================================
    public void ShowCustomChoices(CustomUIBranchNode node)
    {
        if (node.customUIPrefab == null)
        {
            Debug.LogError("【エラー】Custom UI Branch ノードにプレハブがセットされていません！");
            ExecuteNode(node.GetNextNode("choices 0")); // エラー回避のため強制的に0番に進む
            return;
        }

        // 自作UIのプレハブを生成する（普段使っているchoicesPanelの親＝Canvasの下に出す）
        Transform parentTransform = choicesPanel != null ? choicesPanel.transform.parent : this.transform;
        GameObject uiInstance = Instantiate(node.customUIPrefab, parentTransform);

        // プレハブに付いているマネージャーを初期化
        CustomBranchUIManager uiManager = uiInstance.GetComponent<CustomBranchUIManager>();
        if (uiManager != null)
        {
            uiManager.Setup(this, node);
        }
        else
        {
            Debug.LogError("【エラー】生成したプレハブに CustomBranchUIManager スクリプトがアタッチされていません！");
        }
    }
}