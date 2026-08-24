using UnityEngine;
using UnityEngine.UI; // ★追加：Buttonをスクリプトから操作するために必要

public class CustomBranchUIManager : MonoBehaviour
{
    private VideoSelector selector;
    private CustomUIBranchNode node;

    [Header("ボタンの登録")]
    [Tooltip("上から順番にノードの Choices (0, 1, 2...) に対応します。ここに登録したボタンは自動的にクリックイベントが設定されます。")]
    public GameObject[] choiceButtons;

    public void Setup(VideoSelector selector, CustomUIBranchNode node)
    {
        this.selector = selector;
        this.node = node;

        if (choiceButtons != null && choiceButtons.Length > 0 && node.choices != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] == null) continue;

                // 1. ノード側の選択肢数を超えている余分なボタンは隠す
                if (i >= node.choices.Count)
                {
                    choiceButtons[i].SetActive(false);
                    continue;
                }

                // 2. パラメーターの条件判定
                var choiceData = node.choices[i];
                bool isUnlocked = true;

                if (!string.IsNullOrEmpty(choiceData.requiredParam))
                {
                    if (GameManager.GetParameter(choiceData.requiredParam) < choiceData.requiredValue)
                    {
                        isUnlocked = false; // パラメーター不足
                    }
                }

                if (!isUnlocked)
                {
                    choiceButtons[i].SetActive(false); // 隠す
                    continue;
                }
                else
                {
                    choiceButtons[i].SetActive(true); // 表示する
                }

                // 3. ★大進化：スクリプトから自動でクリックイベントを登録！
                Button btn = choiceButtons[i].GetComponent<Button>();
                if (btn != null)
                {
                    int index = i; // （※C#の仕様上、ループ内の変数をそのまま渡すとバグるため、一旦別の変数にコピーします）

                    // プレハブに元々設定されていたかもしれない古いイベントを念のためリセット
                    btn.onClick.RemoveAllListeners();

                    // クリックされたら OnChoiceSelected(自身の番号) を呼ぶように自動登録
                    btn.onClick.AddListener(() => OnChoiceSelected(index));
                }
            }
        }
    }

    // 外部から呼ばれる必要がなくなったので private に変更しました
    private void OnChoiceSelected(int index)
    {
        if (selector != null && node != null)
        {
            selector.ExecuteNode(node.GetNextNode("choices " + index));
            Destroy(gameObject);
        }
    }
}