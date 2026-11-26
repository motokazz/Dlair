using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Choice Node Multi の 1 つの UI。プレハブ、親、選択肢を持つ。
/// </summary>
[Serializable]
public class ChoiceMultiUiEntry
{
    public int uiId = 1;
    public GameObject prefab;

    [Tooltip("-1: Canvas直下 / 0: UI Parent / 1以降: UIParent1...")]
    public int parentSlot;

    public List<string> choices = new List<string> { "Choice 1" };
}

/// <summary>
/// 説明は ChoiceNodeMulti.Summary。
/// </summary>
public class ChoiceNodeMulti : BaseNode, ISerializationCallbackReceiver
{
    public const string Summary =
        "複数の UI を同時に表示し、UI ごとに選択肢へ分岐します。\n" +
        "各 UI はプレハブ、親、選択肢を持ちます。親は Canvas 直下、UI Parent、UIParent1 以降から選びます。未設定の親は Canvas 直下です。\n" +
        "取得したボタンのうち、その UI の選択肢と同じ名前だけを分岐にします。出力の名前は UI番号:選択肢名 です。\n" +
        "取得したボタンに付いているコンポーネントと、プレハブに設定したクリックは残します。\n" +
        "取得していないボタンは扱いません。プレハブに設定したクリックのまま動きます。\n" +
        "選択後に UI を消すがオンのときは、どれかを選ぶと、このノードが出した UI をまとめて消してからその出力へ進みます。\n" +
        "タイトル右のボタンでノード全体、設定ボタンで項目だけ、折りたたみと展開ができます。";

    public List<ChoiceMultiUiEntry> uis = new List<ChoiceMultiUiEntry>
    {
        new ChoiceMultiUiEntry()
    };

    [Header("オプション")]
    public bool autoDestroyUI = true;

    [Tooltip("オンでノード全体を展開、オフで折りたたみ。")]
    public bool nodeExpanded = true;

    [Tooltip("オンで設定項目を展開、オフで折りたたみ。")]
    public bool settingsExpanded = true;

    [SerializeField]
    private int nextUiId = 2;

    [NonSerialized]
    private List<GameObject> currentUIInstances = new List<GameObject>();

    /// <summary>
    /// グラフ上の説明文。Summary と同じです。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    /// <summary>
    /// ノードタイトル用の UI 番号と選択肢。
    /// </summary>
    public string GetDisplayTitle()
    {
        if (uis == null || uis.Count == 0) return "ChoiceMulti";

        List<string> parts = new List<string>();
        for (int i = 0; i < uis.Count; i++)
        {
            ChoiceMultiUiEntry entry = uis[i];
            if (entry?.choices == null) continue;
            for (int c = 0; c < entry.choices.Count; c++)
            {
                if (string.IsNullOrWhiteSpace(entry.choices[c])) continue;
                parts.Add(PortName(entry.uiId, entry.choices[c].Trim()));
            }
        }

        return parts.Count == 0 ? "ChoiceMulti" : "ChoiceMulti  " + string.Join(" / ", parts);
    }

    /// <summary>
    /// 出力ポート名。番号はその UI に固定します。
    /// </summary>
    public static string PortName(int uiId, string choice)
    {
        return "UI" + uiId + ":" + (choice ?? "");
    }

    /// <summary>
    /// 新しい UI 行用の番号。消した番号は再利用しません。
    /// </summary>
    public int AllocateUiId()
    {
        EnsureEntries();
        int id = nextUiId < 1 ? 1 : nextUiId;
        nextUiId = id + 1;
        return id;
    }

    /// <summary>
    /// 欠けている番号と選択肢リストを補います。
    /// </summary>
    public void EnsureEntries()
    {
        if (uis == null) uis = new List<ChoiceMultiUiEntry>();

        int maxId = 0;
        for (int i = 0; i < uis.Count; i++)
        {
            if (uis[i] == null) uis[i] = new ChoiceMultiUiEntry();
            if (uis[i].choices == null) uis[i].choices = new List<string> { "Choice 1" };
            if (uis[i].uiId > maxId) maxId = uis[i].uiId;
        }

        if (nextUiId <= maxId) nextUiId = maxId + 1;
        if (nextUiId < 1) nextUiId = 1;

        HashSet<int> used = new HashSet<int>();
        for (int i = 0; i < uis.Count; i++)
        {
            ChoiceMultiUiEntry entry = uis[i];
            if (entry.uiId >= 1 && used.Add(entry.uiId)) continue;

            entry.uiId = nextUiId;
            nextUiId++;
            used.Add(entry.uiId);
        }
    }

    public override void Execute(StoryPlayer player)
    {
        if (currentUIInstances == null) currentUIInstances = new List<GameObject>();
        currentUIInstances.Clear();

        EnsureEntries();
        if (player == null || uis.Count == 0)
        {
            Debug.LogError("【Choice Node Multi】UIが設定されていません！");
            return;
        }

        int spawned = 0;
        for (int i = 0; i < uis.Count; i++)
        {
            ChoiceMultiUiEntry entry = uis[i];
            if (entry == null || entry.prefab == null)
            {
                Debug.LogError($"【Choice Node Multi】UI{entry?.uiId} のプレハブが設定されていません！");
                continue;
            }

            Transform parent = player.GetUIParentSlot(entry.parentSlot);
            if (parent == null)
            {
                Debug.LogError($"【Choice Node Multi】UI{entry.uiId} を表示するための親 / Canvas がシーン内に見つかりません！");
                continue;
            }

            GameObject instance = player.SpawnPrefab(entry.prefab, parent);
            if (instance == null) continue;

            instance.SetActive(true);
            currentUIInstances.Add(instance);
            BindAcquiredButtons(instance, entry, player);
            spawned++;
            Debug.Log($"【Choice Node Multi】UI{entry.uiId} を表示します: {entry.prefab.name}");
        }

        if (spawned == 0)
            Debug.LogError("【Choice Node Multi】表示できたUIがありません。");
    }

    private void BindAcquiredButtons(GameObject rootUI, ChoiceMultiUiEntry entry, StoryPlayer player)
    {
        Button[] buttons = rootUI.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
        {
            Debug.LogWarning($"【Choice Node Multi】UI{entry.uiId} のプレハブ内にButtonコンポーネントが見つかりません。");
            return;
        }

        HashSet<Button> bound = new HashSet<Button>();
        BranchPanelUI panelUI = rootUI.GetComponent<BranchPanelUI>();
        if (panelUI != null && panelUI.namedButtons != null)
        {
            for (int i = 0; i < panelUI.namedButtons.Count; i++)
            {
                BranchPanelUI.NamedButton item = panelUI.namedButtons[i];
                if (item.button == null || bound.Contains(item.button)) continue;

                string key = FindAcquiredChoice(entry, item.choiceKey);
                if (key == null) continue;

                BindChoiceButton(item.button, entry.uiId, key, player);
                bound.Add(item.button);
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (bound.Contains(btn)) continue;

            string key = FindAcquiredChoice(entry, WithoutCloneSuffix(btn.gameObject.name));
            if (key == null) continue;

            BindChoiceButton(btn, entry.uiId, key, player);
        }
    }

    private static string FindAcquiredChoice(ChoiceMultiUiEntry entry, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || entry == null || entry.choices == null) return null;

        string trimmed = key.Trim();
        for (int i = 0; i < entry.choices.Count; i++)
        {
            string choice = entry.choices[i];
            if (string.IsNullOrWhiteSpace(choice)) continue;
            if (string.Equals(choice.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                return choice;
        }

        return null;
    }

    private static string WithoutCloneSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        const string suffix = "(Clone)";
        while (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - suffix.Length).TrimEnd();
        }

        return name;
    }

    private void BindChoiceButton(Button button, int uiId, string choiceKey, StoryPlayer player)
    {
        string portName = PortName(uiId, choiceKey);
        button.onClick.AddListener(() => OnChoiceSelected(player, portName));
    }

    private void OnChoiceSelected(StoryPlayer player, string portName)
    {
        Debug.Log($"【Choice Node Multi】選択肢がクリックされました: '{portName}'");

        if (autoDestroyUI && currentUIInstances != null)
        {
            for (int i = 0; i < currentUIInstances.Count; i++)
            {
                GameObject instance = currentUIInstances[i];
                if (instance == null) continue;
                player.UnregisterSpawned(instance);
                UnityEngine.Object.Destroy(instance);
            }

            currentUIInstances.Clear();
        }

        player.ContinueTo(this, portName);
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        EnsureEntries();
    }
}
