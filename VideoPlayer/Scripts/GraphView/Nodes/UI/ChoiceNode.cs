using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoiceNode : BaseNode
{
    [Header("表示するUIプレハブ")]
    public GameObject branchUIPrefab;

    [Header("選択肢（出力ポート名一覧）")]
    public List<string> choices = new List<string> { "Choice 1", "Choice 2" };

    [Header("オプション")]
    public bool autoDestroyUI = true;

    [NonSerialized]
    private GameObject currentUIInstance;

    public override void Execute(StoryPlayer player)
    {
        Debug.Log($"【Choice Node】入力待ちUIを表示します: {(branchUIPrefab != null ? branchUIPrefab.name : "None")}");

        if (branchUIPrefab == null)
        {
            Debug.LogError("【Choice Node】UIプレハブが設定されていません！");
            return;
        }

        Canvas parentCanvas = player.GetUICanvas();
        if (parentCanvas == null)
        {
            Debug.LogError("【Choice Node】UIを表示するためのCanvasがシーン内に見つかりません！");
            return;
        }

        currentUIInstance = player.SpawnPrefab(branchUIPrefab, parentCanvas.transform);
        currentUIInstance.SetActive(true);

        // BranchPanelUIコンポーネントがある場合
        BranchPanelUI panelUI = currentUIInstance.GetComponent<BranchPanelUI>();
        if (panelUI != null)
        {
            panelUI.SetupCallbacks(selectedKey => OnChoiceSelected(player, selectedKey));
        }
        else
        {
            // 汎用ボタンバインド（子要素のすべてのButtonを取得して紐付け）
            BindButtonsAutomatically(currentUIInstance, player);
        }
    }

    private void BindButtonsAutomatically(GameObject rootUI, StoryPlayer player)
    {
        Button[] buttons = rootUI.GetComponentsInChildren<Button>(true);
        if (buttons.Length == 0)
        {
            Debug.LogWarning("【Choice Node】プレハブ内にButtonコンポーネントが見つかりません。");
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            Button btn = buttons[i];
            string btnName = btn.gameObject.name;

            // 選択肢リストに完全一致する名前があるか確認
            string matchingChoice = choices.Find(c => string.Equals(c, btnName, StringComparison.OrdinalIgnoreCase));

            // 一致しない場合はボタン内テキストも確認
            if (string.IsNullOrEmpty(matchingChoice))
            {
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    matchingChoice = choices.Find(c => string.Equals(c, tmp.text.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            // それでも見つからない場合はインデックスで割り当て
            if (string.IsNullOrEmpty(matchingChoice) && i < choices.Count)
            {
                matchingChoice = choices[i];
            }

            // 何らかのキーが決まった場合
            if (!string.IsNullOrEmpty(matchingChoice))
            {
                string targetKey = matchingChoice;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnChoiceSelected(player, targetKey));
            }
            else
            {
                // リストにない余分なボタンは、ボタン名そのものをキーにしてバインド
                string fallbackKey = btnName;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnChoiceSelected(player, fallbackKey));
            }
        }
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
}