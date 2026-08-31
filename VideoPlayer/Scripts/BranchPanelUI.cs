using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BranchPanelUI : MonoBehaviour
{
    [Serializable]
    public struct NamedButton
    {
        public string choiceKey;
        public Button button;
    }

    [Header("明示的なボタン割り当て（任意）")]
    public List<NamedButton> namedButtons = new List<NamedButton>();

    // ボタンとクリック時のコールバックを登録
    public void SetupCallbacks(Action<string> onChoiceSelected)
    {
        // 1. 明示的に設定されている場合
        if (namedButtons != null && namedButtons.Count > 0)
        {
            foreach (var item in namedButtons)
            {
                if (item.button != null)
                {
                    string key = item.choiceKey;
                    item.button.onClick.RemoveAllListeners();
                    item.button.onClick.AddListener(() => onChoiceSelected?.Invoke(key));
                }
            }
        }
        else
        {
            // 2. 自動検索（子要素のすべてのButtonを取得）
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string key = btn.gameObject.name;

                // ボタン内のテキスト（TMP or UI.Text）があればそれも参照可能
                var tmp = btn.GetComponentInChildren<TMP_Text>();
                if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                {
                    // 名前はGameObjectの名前を優先しつつ登録
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => onChoiceSelected?.Invoke(key));
            }
        }
    }
}