using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 行先ボタンの出し方。表示、ロック、非表示。
/// </summary>
public enum GotoDisplayState
{
    Visible,
    Locked,
    Hidden
}

/// <summary>
/// 行先一覧の上書き。各行の設定、すべて表示、解放済みだけ。
/// </summary>
public enum GotoPanelOverride
{
    EntrySettings,
    ShowAll,
    UnlockedOnly
}

/// <summary>
/// Label への移動ボタンを並べる。再生記録を見て、クリックでそのラベルへ飛ぶ。
/// </summary>
public class StoryGotoPanel : MonoBehaviour
{
    /// <summary>
    /// 行先ボタン1件。ラベル名、ボタン、表示、初期解放、移動先グラフ。
    /// </summary>
    [Serializable]
    public class Entry
    {
        [Tooltip("Label（行先）ノードの名前。空ならボタン名を使います")]
        public string labelName;
        public Button button;
        public GotoDisplayState displayState = GotoDisplayState.Visible;
        [Tooltip("最初から解放されているか")]
        public bool unlockedByDefault;
        [Tooltip("空なら StoryPlayer のグラフから探します")]
        public StoryGraph targetGraph;
    }

    [Header("参照")]
    public StoryPlayer storyPlayer;
    [Tooltip("行先の検索先。空なら StoryPlayer のグラフを使います")]
    public StoryGraph searchGraph;

    [Header("表示")]
    [Tooltip("EntrySettings: 各行の表示状態 / ShowAll: デバッグですべて押せる / UnlockedOnly: 解放済みだけ")]
    public GotoPanelOverride displayOverride = GotoPanelOverride.EntrySettings;
    [Tooltip("エントリ未設定時、子ボタンへ一括適用する表示状態")]
    public GotoDisplayState defaultDisplayState = GotoDisplayState.Visible;

    [Header("行先一覧（空なら子ボタン名＝ラベル名で自動割り当て）")]
    public List<Entry> entries = new List<Entry>();

    private readonly List<BoundEntry> bound = new List<BoundEntry>();

    private struct BoundEntry
    {
        public string key;
        public Entry source;
        public Button button;
        public bool unlockedByDefault;
        public StoryGraph targetGraph;
        public GotoDisplayState displayState;
    }

    private void OnEnable()
    {
        StoryVisitLog.OnChanged -= RefreshDisplay;
        StoryVisitLog.OnChanged += RefreshDisplay;
        BindButtons();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        StoryVisitLog.OnChanged -= RefreshDisplay;
    }

    public static bool IsUnlocked(StoryGraph graph, string labelName)
    {
        if (graph == null || string.IsNullOrWhiteSpace(labelName)) return false;
        if (!graph.TryFindLabelByName(labelName.Trim(), out StoryGraph owner, out LabelNode label))
            return graph.IsLabelVisited(labelName);

        StoryGraph visitedGraph = owner != null ? owner : graph;
        string visitedLabel = label != null ? label.GetDisplayName() : labelName.Trim();
        return visitedGraph.IsLabelVisited(visitedLabel);
    }

    public void BindButtons()
    {
        bound.Clear();
        EnsurePlayer();

        List<Entry> resolved = ResolveEntries();
        for (int i = 0; i < resolved.Count; i++)
        {
            Entry entry = resolved[i];
            if (entry == null || entry.button == null) continue;

            string key = string.IsNullOrWhiteSpace(entry.labelName)
                ? entry.button.gameObject.name
                : entry.labelName.Trim();

            Button capturedButton = entry.button;
            string capturedKey = key;
            StoryGraph capturedGraph = entry.targetGraph;
            capturedButton.onClick.RemoveAllListeners();
            capturedButton.onClick.AddListener(() => JumpTo(capturedKey, capturedGraph));

            bound.Add(new BoundEntry
            {
                key = key,
                source = entry,
                button = capturedButton,
                unlockedByDefault = entry.unlockedByDefault,
                targetGraph = capturedGraph,
                displayState = entry.displayState
            });
        }
    }

    public void RefreshDisplay()
    {
        EnsurePlayer();
        for (int i = 0; i < bound.Count; i++)
        {
            BoundEntry item = bound[i];
            if (item.button == null) continue;
            ApplyDisplay(item);
        }
    }

    public void JumpTo(string labelName)
    {
        JumpTo(labelName, null);
    }

    public void JumpTo(string labelName, StoryGraph destGraph)
    {
        if (string.IsNullOrWhiteSpace(labelName)) return;
        EnsurePlayer();
        if (storyPlayer == null)
        {
            Debug.LogError("【GotoPanel】StoryPlayer が見つかりません。");
            return;
        }

        StoryGraph graph = destGraph != null ? destGraph : searchGraph;
        if (!storyPlayer.JumpFromOutside(labelName.Trim(), graph))
        {
            Debug.LogWarning($"【GotoPanel】行先 '{labelName}' へジャンプできませんでした。");
        }
    }

    private List<Entry> ResolveEntries()
    {
        if (entries != null && entries.Count > 0)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.button != null) continue;
                string name = string.IsNullOrWhiteSpace(entry.labelName) ? null : entry.labelName.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                entry.button = FindChildButton(name);
            }

            return entries;
        }

        List<Entry> auto = new List<Entry>();
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            auto.Add(new Entry
            {
                button = buttons[i],
                labelName = buttons[i].gameObject.name,
                displayState = defaultDisplayState
            });
        }

        return auto;
    }

    private Button FindChildButton(string objectName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (string.Equals(buttons[i].gameObject.name, objectName, StringComparison.Ordinal))
            {
                return buttons[i];
            }
        }

        return null;
    }

    private void ApplyDisplay(BoundEntry item)
    {
        bool unlocked = item.unlockedByDefault || IsEntryUnlocked(item);
        bool visible;
        bool interactable;

        if (displayOverride == GotoPanelOverride.ShowAll)
        {
            visible = true;
            interactable = true;
        }
        else if (displayOverride == GotoPanelOverride.UnlockedOnly)
        {
            visible = item.displayState != GotoDisplayState.Hidden && (item.displayState == GotoDisplayState.Visible || unlocked);
            interactable = visible;
        }
        else
        {
            switch (item.displayState)
            {
                case GotoDisplayState.Hidden:
                    visible = false;
                    interactable = false;
                    break;
                case GotoDisplayState.Locked:
                    visible = true;
                    interactable = unlocked;
                    break;
                default:
                    visible = true;
                    interactable = true;
                    break;
            }
        }

        if (item.button.gameObject.activeSelf != visible)
        {
            item.button.gameObject.SetActive(visible);
        }

        item.button.interactable = interactable;
    }

    private bool IsEntryUnlocked(BoundEntry item)
    {
        StoryGraph search = item.targetGraph != null ? item.targetGraph : searchGraph;
        if (search == null && storyPlayer != null)
            search = storyPlayer.graph;

        return IsUnlocked(search, item.key);
    }

    private void EnsurePlayer()
    {
        if (storyPlayer != null) return;
#if UNITY_2023_1_OR_NEWER
        storyPlayer = FindFirstObjectByType<StoryPlayer>();
#else
        storyPlayer = FindObjectOfType<StoryPlayer>();
#endif
    }
}
