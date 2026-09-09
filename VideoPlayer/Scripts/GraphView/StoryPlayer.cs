using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

public class StoryPlayer : MonoBehaviour
{
    [Header("新システムの設定")]
    [FormerlySerializedAs("flowGraph")]
    public StoryGraph graph;
    public DualVideoPlayer dualPlayer;
    public StoryVariableDatabase variableDatabase;
    public Canvas uiCanvas;

    [Header("Debug / Collection")]
    [Tooltip("指定した行先ラベル名から開始します。空なら Start ノードから開始します。")]
    public string startFromLabel;

    private BaseNode currentNode;
    private Coroutine monitorCoroutine;
    private bool hasStartedMedia;
    private int gotoDepth;
    private readonly Stack<GraphFrame> graphStack = new Stack<GraphFrame>();
    private readonly List<GameObject> runtimeSpawned = new List<GameObject>();
    private readonly HashSet<GameObject> protectedSpawned = new HashSet<GameObject>();

    private struct GraphFrame
    {
        public StoryGraph graph;
        public BaseNode returnNode;
    }

    public StoryGraph ActiveGraph => graphStack.Count > 0 ? graphStack.Peek().graph : graph;

    public bool IsInSubGraph => graphStack.Count > 0;

    public BaseNode CurrentNode => currentNode;

    public void ExecuteNode(BaseNode node)
    {
        if (node == null) return;
        currentNode = node;
        node.Execute(this);
    }

    public BaseNode GetNextNode(string guid, string portName)
    {
        StoryGraph active = ActiveGraph;
        return active != null ? active.GetNextNode(guid, portName) : null;
    }

    public void ContinueTo(BaseNode from, string portName)
    {
        if (from == null) return;

        BaseNode nextNode = GetNextNode(from.guid, portName);
        if (nextNode != null)
        {
            ExecuteNode(nextNode);
            return;
        }

        if (graphStack.Count > 0)
        {
            ExitSubGraph();
            return;
        }

        Debug.Log($"【進行終了】'{portName}' の先にノードがありません。");
    }

    public void EnterSubGraph(StoryGraph subGraph, BaseNode returnNode)
    {
        if (subGraph == null)
        {
            ContinueTo(returnNode, "Next");
            return;
        }

        if (IsGraphInCallStack(subGraph))
        {
            Debug.LogError($"【SubGraph】循環参照のため入れません: {subGraph.name}");
            ContinueTo(returnNode, "Next");
            return;
        }

        if (graphStack.Count >= 16)
        {
            Debug.LogError("【SubGraph】ネストが深すぎます。");
            ContinueTo(returnNode, "Next");
            return;
        }

        graphStack.Push(new GraphFrame { graph = subGraph, returnNode = returnNode });
        BaseNode startNode = subGraph.nodes != null ? subGraph.nodes.Find(n => n is StartNode) : null;
        if (startNode != null)
        {
            ExecuteNode(startNode);
        }
        else
        {
            Debug.LogWarning($"【SubGraph】'{subGraph.name}' に Start ノードがありません。");
            ExitSubGraph();
        }
    }

    public void ExitSubGraph()
    {
        if (graphStack.Count == 0) return;

        GraphFrame frame = graphStack.Pop();
        currentNode = frame.returnNode;

        if (TryContinueFrom(frame.returnNode))
        {
            return;
        }

        if (TryResumeOwnerFlow(frame.graph))
        {
            return;
        }

        if (graphStack.Count > 0)
        {
            ExitSubGraph();
            return;
        }

        Debug.Log("【進行終了】Exit ノードに到達しました。");
    }

    public void ExitSubGraphOrEnd()
    {
        if (graphStack.Count > 0)
        {
            ExitSubGraph();
            return;
        }

        Debug.Log("【進行終了】Exit ノードに到達しました。");
    }

    public void JumpToLabel(StoryGraph destGraph, string labelGuid, BaseNode fromGoto)
    {
        if (gotoDepth >= 32)
        {
            Debug.LogError("【Goto】ジャンプが多すぎます。ループしていないか確認してください。");
            return;
        }

        StoryGraph owner = null;
        LabelNode label = null;

        if (destGraph != null)
        {
            destGraph.TryFindLabel(labelGuid, out owner, out label);
        }

        if (label == null && ActiveGraph != null)
        {
            ActiveGraph.TryFindLabel(labelGuid, out owner, out label);
        }

        if (label == null && graph != null)
        {
            graph.TryFindLabel(labelGuid, out owner, out label);
        }

        if (label == null)
        {
            Debug.LogError("【Goto】行先ラベルが見つかりません。先に Label（行先）ノードを作成し、Goto から選択してください。");
            return;
        }

        Debug.Log($"【Goto】'{label.GetDisplayName()}' へジャンプ ({(owner != null ? owner.name : "?")})");
        gotoDepth++;
        try
        {
            StopMediaMonitor();
            ClearRuntimePrefabs();
            EnterGraphAtNode(owner, label, fromGoto);
        }
        finally
        {
            gotoDepth--;
        }
    }

    public bool PlayFromLabel(string labelName)
    {
        return JumpFromOutside(labelName, null, false);
    }

    public bool JumpFromOutside(string labelName, StoryGraph searchRoot = null)
    {
        return JumpFromOutside(labelName, searchRoot, true);
    }

    private bool JumpFromOutside(string labelName, StoryGraph searchRoot, bool resetPlayback)
    {
        if (string.IsNullOrWhiteSpace(labelName)) return false;

        StoryGraph search = searchRoot != null ? searchRoot : graph;
        if (search == null) return false;
        if (!search.TryFindLabelByName(labelName.Trim(), out StoryGraph owner, out LabelNode label))
        {
            return false;
        }

        if (resetPlayback)
        {
            gotoDepth = 0;
            graphStack.Clear();
        }

        StopMediaMonitor();
        ClearRuntimePrefabs();
        EnterGraphAtNode(owner, label, null);
        return true;
    }

    public GameObject SpawnPrefab(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, parent);
        RegisterSpawned(instance);
        return instance;
    }

    public void RegisterSpawned(GameObject instance)
    {
        if (instance == null) return;
        if (!runtimeSpawned.Contains(instance))
        {
            runtimeSpawned.Add(instance);
        }
    }

    public void UnregisterSpawned(GameObject instance)
    {
        if (instance == null) return;
        runtimeSpawned.Remove(instance);
        protectedSpawned.Remove(instance);
    }

    public void ProtectSpawned(GameObject instance)
    {
        if (instance == null) return;
        protectedSpawned.Add(instance);
    }

    public void UnprotectSpawned(GameObject instance)
    {
        if (instance == null) return;
        protectedSpawned.Remove(instance);
    }

    public void ClearRuntimePrefabs()
    {
        for (int i = runtimeSpawned.Count - 1; i >= 0; i--)
        {
            GameObject instance = runtimeSpawned[i];
            if (instance == null)
            {
                runtimeSpawned.RemoveAt(i);
                continue;
            }

            if (protectedSpawned.Contains(instance)) continue;

            Destroy(instance);
            runtimeSpawned.RemoveAt(i);
        }

        protectedSpawned.RemoveWhere(instance => instance == null);
    }

    private void EnterGraphAtNode(StoryGraph destGraph, BaseNode destNode, BaseNode returnNode)
    {
        if (destGraph == null || destNode == null) return;

        if (destGraph == ActiveGraph)
        {
            ExecuteNode(destNode);
            return;
        }

        if (destGraph == graph)
        {
            graphStack.Clear();
            ExecuteNode(destNode);
            return;
        }

        // 0600 → 0630 → 0600 のように、すでに積んであるグラフへ戻るときはネストせず巻き戻す
        if (TryUnwindToGraph(destGraph))
        {
            ExecuteNode(destNode);
            return;
        }

        if (TryFindOwnerSubGraphNode(destGraph, out BaseNode ownerNode, out int framesToPop))
        {
            for (int i = 0; i < framesToPop; i++)
            {
                if (graphStack.Count == 0) break;
                graphStack.Pop();
            }

            returnNode = ownerNode;
        }
        else if (graphStack.Count > 0)
        {
            // SubGraph 関係のない章どうしの Goto は呼び出しではなくジャンプとして差し替える
            GraphFrame current = graphStack.Pop();
            returnNode = current.returnNode;
        }

        if (IsGraphInCallStack(destGraph))
        {
            Debug.LogError($"【Goto】すでに '{destGraph.name}' の実行中です。");
            return;
        }

        if (graphStack.Count >= 16)
        {
            Debug.LogError("【Goto】ネストが深すぎます。");
            return;
        }

        graphStack.Push(new GraphFrame { graph = destGraph, returnNode = returnNode });
        ExecuteNode(destNode);
    }

    private bool TryUnwindToGraph(StoryGraph destGraph)
    {
        if (destGraph == null || graphStack.Count == 0) return false;

        int framesToPop = -1;
        GraphFrame[] frames = graphStack.ToArray();
        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i].graph != destGraph) continue;
            framesToPop = i;
            break;
        }

        if (framesToPop < 0) return false;

        for (int i = 0; i < framesToPop; i++)
        {
            if (graphStack.Count == 0) break;
            graphStack.Pop();
        }

        return ActiveGraph == destGraph;
    }

    private bool TryContinueFrom(BaseNode from)
    {
        if (from == null) return false;

        BaseNode nextNode = GetNextNode(from.guid, "Next");
        if (nextNode == null) return false;

        ExecuteNode(nextNode);
        return true;
    }

    private bool TryResumeOwnerFlow(StoryGraph exitedGraph)
    {
        if (exitedGraph == null) return false;

        while (true)
        {
            BaseNode ownerNode = FindDirectSubGraphNode(ActiveGraph, exitedGraph);
            if (ownerNode != null)
            {
                return TryContinueFrom(ownerNode);
            }

            if (graphStack.Count == 0)
            {
                return false;
            }

            graphStack.Pop();
        }
    }

    private bool TryFindOwnerSubGraphNode(StoryGraph destGraph, out BaseNode ownerNode, out int framesToPop)
    {
        ownerNode = null;
        framesToPop = 0;
        if (destGraph == null) return false;

        ownerNode = FindDirectSubGraphNode(ActiveGraph, destGraph);
        if (ownerNode != null)
        {
            return true;
        }

        GraphFrame[] frames = graphStack.ToArray();
        for (int i = 0; i < frames.Length; i++)
        {
            StoryGraph parent = (i + 1 < frames.Length) ? frames[i + 1].graph : graph;
            ownerNode = FindDirectSubGraphNode(parent, destGraph);
            if (ownerNode == null) continue;

            framesToPop = i + 1;
            return true;
        }

        return false;
    }

    private static BaseNode FindDirectSubGraphNode(StoryGraph owner, StoryGraph nested)
    {
        if (owner == null || nested == null || owner.nodes == null) return null;

        for (int i = 0; i < owner.nodes.Count; i++)
        {
            if (owner.nodes[i] is SubGraphNode subGraphNode && subGraphNode.subGraph == nested)
            {
                return subGraphNode;
            }
        }

        return null;
    }

    private void StopMediaMonitor()
    {
        if (monitorCoroutine == null) return;
        StopCoroutine(monitorCoroutine);
        monitorCoroutine = null;
    }

    private bool IsGraphInCallStack(StoryGraph candidate)
    {
        if (candidate == null) return false;
        if (candidate == graph) return true;
        foreach (GraphFrame frame in graphStack)
        {
            if (frame.graph == candidate) return true;
        }
        return false;
    }

    public Canvas GetUICanvas()
    {
        if (uiCanvas != null) return uiCanvas;
#if UNITY_2023_1_OR_NEWER
        uiCanvas = Object.FindFirstObjectByType<Canvas>();
#else
        uiCanvas = Object.FindObjectOfType<Canvas>();
#endif
        return uiCanvas;
    }

    private void Start()
    {
        if (variableDatabase != null)
        {
            GameManager.InitializeFromDatabase(variableDatabase);
        }

        StartCoroutine(InitAndPlay());
    }

    private IEnumerator InitAndPlay()
    {
        if (graph == null || dualPlayer == null) yield break;
        yield return null;
        gotoDepth = 0;

        BaseNode startNode = graph.nodes.Find(n => n is StartNode);
        if (!string.IsNullOrWhiteSpace(startFromLabel) && PlayFromLabel(startFromLabel.Trim()))
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(startFromLabel))
        {
            Debug.LogWarning($"【StoryPlayer】startFromLabel '{startFromLabel}' が見つからないため Start から開始します。");
        }

        if (startNode != null) ExecuteNode(startNode);
    }

    public void PlayVideo(VideoClip clip, BaseNode node, bool isLooping = false, float portOutputTime = -1f, AudioClip audioClip = null, float crossFadeDuration = 1.0f)
    {
        if (clip == null) return;
        currentNode = node;
        float fade = Mathf.Max(0f, crossFadeDuration);
        StartOrTransitionMedia(false, clip, null, isLooping, audioClip, fade);
        BeginPortOutput(portOutputTime, (float)clip.length, fade, true);
    }

    public void PlayImage(Texture2D image, float duration, AudioClip audioClip, bool isLooping, float crossFadeDuration, BaseNode node, float portOutputTime)
    {
        if (image == null) return;
        currentNode = node;
        float fade = Mathf.Max(0f, crossFadeDuration);
        StartOrTransitionMedia(true, null, image, isLooping, audioClip, fade);
        BeginPortOutput(portOutputTime, duration, fade, false);
    }

    private void StartOrTransitionMedia(bool isStaticImage, VideoClip clip, Texture2D image, bool isLooping, AudioClip audioClip, float fadeDuration)
    {
        if (!hasStartedMedia)
        {
            dualPlayer.PlayFirstMedia(isStaticImage, clip, image, isLooping, audioClip);
            hasStartedMedia = true;
        }
        else
        {
            dualPlayer.RequestPlayNextMedia(isStaticImage, clip, image, isLooping, audioClip, fadeDuration);
        }
    }

    private void BeginPortOutput(float portOutputTime, float mediaDuration, float fadeOverlap, bool isVideo)
    {
        if (monitorCoroutine != null) StopCoroutine(monitorCoroutine);
        monitorCoroutine = null;

        bool fireAtStart = portOutputTime >= 0f && Mathf.Approximately(portOutputTime, 0f);
        if (fireAtStart)
        {
            PlayNextNode();
            return;
        }

        bool fireAtEnd = portOutputTime < 0f || (mediaDuration > 0f && portOutputTime >= mediaDuration);
        if (isVideo)
        {
            monitorCoroutine = StartCoroutine(VideoPortOutputRoutine(portOutputTime, fireAtEnd, fadeOverlap));
        }
        else
        {
            monitorCoroutine = StartCoroutine(ImagePortOutputRoutine(portOutputTime, mediaDuration, fadeOverlap, fireAtEnd));
        }
    }

    private IEnumerator VideoPortOutputRoutine(float portOutputTime, bool fireAtEnd, float fadeOverlap)
    {
        while (dualPlayer.IsTransitioning) yield return null;

        VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
        if (activePlayer == null) yield break;

        while (activePlayer.length <= 0) yield return null;

        double totalTime = activePlayer.length;
        if (!fireAtEnd && portOutputTime >= totalTime)
        {
            fireAtEnd = true;
        }

        if (fireAtEnd)
        {
            yield return WaitUntilClipEnd(activePlayer, totalTime, fadeOverlap);
        }
        else
        {
            yield return WaitUntilPlaybackTime(activePlayer, portOutputTime, totalTime);
        }
    }

    private IEnumerator WaitUntilClipEnd(VideoPlayer activePlayer, double totalTime, float fadeOverlap)
    {
        float triggerOverlap = Mathf.Max(0.1f, fadeOverlap);
        double previousTime = 0;

        while (true)
        {
            if (dualPlayer.IsTransitioning) yield break;

            double currentTime = activePlayer.time;
            bool reachedEndWindow = currentTime > 0.1f && (totalTime - currentTime) <= triggerOverlap;
            bool loopedBack = previousTime > 1.0 && currentTime + 0.5 < previousTime;
            if (reachedEndWindow || loopedBack)
            {
                PlayNextNode();
                yield break;
            }

            if (!activePlayer.isLooping && currentTime > (totalTime / 2.0) && !activePlayer.isPlaying)
            {
                PlayNextNode();
                yield break;
            }

            previousTime = currentTime;
            yield return null;
        }
    }

    private IEnumerator ImagePortOutputRoutine(float portOutputTime, float duration, float fadeOverlap, bool fireAtEnd)
    {
        while (dualPlayer.IsTransitioning) yield return null;

        if (!fireAtEnd && duration > 0f && portOutputTime >= duration)
        {
            fireAtEnd = true;
        }

        float waitTime;
        if (fireAtEnd)
        {
            waitTime = Mathf.Max(0f, duration - Mathf.Max(0.1f, fadeOverlap));
        }
        else
        {
            waitTime = Mathf.Max(0f, portOutputTime);
        }

        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            if (dualPlayer.IsTransitioning) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }

        PlayNextNode();
    }

    private IEnumerator WaitUntilPlaybackTime(VideoPlayer activePlayer, float portOutputTime, double totalTime)
    {
        while (true)
        {
            if (dualPlayer.IsTransitioning) yield break;

            if (activePlayer.time >= portOutputTime)
            {
                PlayNextNode();
                yield break;
            }

            if (activePlayer.time > (totalTime / 2.0) && !activePlayer.isPlaying)
            {
                PlayNextNode();
                yield break;
            }

            yield return null;
        }
    }

    private void PlayNextNode()
    {
        ContinueTo(currentNode, "Next");
    }
}