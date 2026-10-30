using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 説明は ChoiceNode.Summary。
/// </summary>
public class ChoiceNode : BaseNode, ISerializationCallbackReceiver
{
    public const string Summary =
        "UI Prefab の中にあるボタンのうち、選択肢に取得した名前と同じものだけを分岐にします。クリックすると、その名前の出力へ進みます。\n" +
        "取得したボタンに付いているコンポーネントと、プレハブに設定したクリックは残します。\n" +
        "取得していないボタンは Choice では扱いません。プレハブに設定したクリックのまま動きます。\n" +
        "タイトル右のボタンでノード全体、設定ボタンで項目だけ、折りたたみと展開ができます。";

    [Header("表示するUIプレハブ")]
    public GameObject branchUIPrefab;

    [Header("選択肢（出力ポート名一覧）")]
    public List<string> choices = new List<string> { "Choice 1", "Choice 2" };

    [Header("オプション")]
    public bool autoDestroyUI = true;

    [Tooltip("オン: StoryPlayer の UI Parent 配下 / オフ: Canvas 直下。Parent 未指定時はどちらも Canvas 直下")]
    public bool parentToUiParent = true;

    [Tooltip("オンでノード全体を展開、オフで折りたたみ。")]
    public bool nodeExpanded = true;

    [Tooltip("オンで設定項目を展開、オフで折りたたみ。")]
    public bool settingsExpanded = true;

    [SerializeField]
    private int layoutVersion = 1;

    [NonSerialized]
    private GameObject currentUIInstance;

    /// <summary>
    /// グラフ上の説明文。Summary と同じです。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    public override void Execute(StoryPlayer player)
    {
        Debug.Log($"【Choice Node】入力待ちUIを表示します: {(branchUIPrefab != null ? branchUIPrefab.name : "None")}");

        if (branchUIPrefab == null)
        {
            Debug.LogError("【Choice Node】UIプレハブが設定されていません！");
            return;
        }

        Transform parent = player.GetUIParent(parentToUiParent);
        if (parent == null)
        {
            Debug.LogError("【Choice Node】UIを表示するための親 / Canvas がシーン内に見つかりません！");
            return;
        }

        currentUIInstance = player.SpawnPrefab(branchUIPrefab, parent);
        currentUIInstance.SetActive(true);
        BindAcquiredButtons(currentUIInstance, player);
    }

    private void BindAcquiredButtons(GameObject rootUI, StoryPlayer player)
    {
        Button[] buttons = rootUI.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
        {
            Debug.LogWarning("【Choice Node】プレハブ内にButtonコンポーネントが見つかりません。");
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

                string key = FindAcquiredChoice(item.choiceKey);
                if (key == null) continue;

                BindChoiceButton(item.button, key, player);
                bound.Add(item.button);
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            if (bound.Contains(btn)) continue;

            string key = FindAcquiredChoice(WithoutCloneSuffix(btn.gameObject.name));
            if (key == null) continue;

            BindChoiceButton(btn, key, player);
        }
    }

    private string FindAcquiredChoice(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || choices == null) return null;

        string trimmed = key.Trim();
        for (int i = 0; i < choices.Count; i++)
        {
            string choice = choices[i];
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

    private void BindChoiceButton(Button button, string choiceKey, StoryPlayer player)
    {
        string targetKey = choiceKey;
        button.onClick.AddListener(() => OnChoiceSelected(player, targetKey));
    }

    private void OnChoiceSelected(StoryPlayer player, string choiceKey)
    {
        Debug.Log($"【Choice Node】選択肢がクリックされました: '{choiceKey}'");

        if (autoDestroyUI && currentUIInstance != null)
        {
            player.UnregisterSpawned(currentUIInstance);
            UnityEngine.Object.Destroy(currentUIInstance);
            currentUIInstance = null;
        }

        player.ContinueTo(this, choiceKey);
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (layoutVersion >= 1) return;

        nodeExpanded = true;
        settingsExpanded = true;
        layoutVersion = 1;
    }
}