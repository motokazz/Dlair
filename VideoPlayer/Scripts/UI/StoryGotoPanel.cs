using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public enum GotoDisplayState
{
    Visible,
    Locked,
    Hidden
}

public enum GotoPanelOverride
{
    EntrySettings,
    ShowAll,
    UnlockedOnly
}

public class StoryGotoPanel : MonoBehaviour
{
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

    [Header("永続化")]
    public bool persistUnlocks = true;
    public string saveKey = "StoryGotoUnlocks";

    [Header("行先一覧（空なら子ボタン名＝ラベル名で自動割り当て）")]
    public List<Entry> entries = new List<Entry>();

    private static readonly List<StoryGotoPanel> instances = new List<StoryGotoPanel>();
    private static HashSet<string> unlockedIds = new HashSet<string>();
    private static bool unlocksLoaded;

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
        if (!instances.Contains(this)) instances.Add(this);
        LoadUnlocksIfNeeded();
        BindButtons();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        instances.Remove(this);
    }

    public static void NotifyVisited(string labelName)
    {
        if (string.IsNullOrWhiteSpace(labelName)) return;
        Unlock(labelName.Trim());
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null) instances[i].RefreshDisplay();
        }

        StoryLabelTreePanel.RefreshAll();
    }

    public static bool IsUnlocked(string labelName)
    {
        LoadUnlocksIfNeeded();
        return !string.IsNullOrEmpty(labelName) && unlockedIds.Contains(labelName);
    }

    public static void Unlock(string labelName)
    {
        if (string.IsNullOrWhiteSpace(labelName)) return;
        LoadUnlocksIfNeeded();
        if (!unlockedIds.Add(labelName.Trim())) return;
        SaveUnlocks();
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
        LoadUnlocksIfNeeded();
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
        bool unlocked = item.unlockedByDefault || IsUnlocked(item.key);
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

    private void EnsurePlayer()
    {
        if (storyPlayer != null) return;
#if UNITY_2023_1_OR_NEWER
        storyPlayer = FindFirstObjectByType<StoryPlayer>();
#else
        storyPlayer = FindObjectOfType<StoryPlayer>();
#endif
    }

    private static void LoadUnlocksIfNeeded()
    {
        if (unlocksLoaded) return;
        unlocksLoaded = true;
        unlockedIds.Clear();

        string path = GetSavePath();
        if (!File.Exists(path)) return;

        try
        {
            UnlockSaveData data = JsonUtility.FromJson<UnlockSaveData>(File.ReadAllText(path));
            if (data?.ids == null) return;
            for (int i = 0; i < data.ids.Count; i++)
            {
                if (!string.IsNullOrEmpty(data.ids[i])) unlockedIds.Add(data.ids[i]);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【GotoPanel】解放データの読み込みに失敗しました: {e.Message}");
        }
    }

    private static void SaveUnlocks()
    {
        bool anyPersist = false;
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null && instances[i].persistUnlocks)
            {
                anyPersist = true;
                break;
            }
        }

        if (!anyPersist && instances.Count > 0) return;

        UnlockSaveData data = new UnlockSaveData { ids = new List<string>(unlockedIds) };
        try
        {
            File.WriteAllText(GetSavePath(), JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"【GotoPanel】解放データの保存に失敗しました: {e.Message}");
        }
    }

    private static string GetSavePath()
    {
        string key = "StoryGotoUnlocks";
        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i] != null && !string.IsNullOrEmpty(instances[i].saveKey))
            {
                key = instances[i].saveKey;
                break;
            }
        }

        return Path.Combine(Application.persistentDataPath, key + ".json");
    }

    [Serializable]
    private class UnlockSaveData
    {
        public List<string> ids = new List<string>();
    }
}
