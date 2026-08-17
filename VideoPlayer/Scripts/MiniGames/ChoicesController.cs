using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoicesController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "ChoicesController";

    [Header("UI Elements")]
    public GameObject choicesPanel;

    [Tooltip("汎用化されたタイプライターエフェクト")]
    public TypewriterEffect typewriter;

    public Transform buttonContainer;
    public GameObject buttonPrefab;

    private VideoSelector currentSelector;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    public void StartGame(VideoSelector selector, MediaPlaylist.MediaData data)
    {
        currentSelector = selector;

        // ==========================================
        // ★大修正：一番最初にパネルをActiveにする！
        // これをしないと、子供のオブジェクトでコルーチンが動かせません。
        // ==========================================
        if (choicesPanel != null) choicesPanel.SetActive(true);

        // 古いボタンの消去
        foreach (var btnObj in spawnedButtons) Destroy(btnObj);
        spawnedButtons.Clear();

        // ボタンの生成（最初は非表示）
        if (data.choices != null)
        {
            foreach (var choice in data.choices)
            {
                GameObject newBtn = Instantiate(buttonPrefab, buttonContainer);
                newBtn.SetActive(false); // ボタンは隠しておく
                spawnedButtons.Add(newBtn);

                TMP_Text tmpText = newBtn.GetComponentInChildren<TMP_Text>();
                if (tmpText != null) tmpText.text = choice.branchKey;
                else
                {
                    Text fallbackText = newBtn.GetComponentInChildren<Text>();
                    if (fallbackText != null) fallbackText.text = choice.branchKey;
                }

                Button btnComponent = newBtn.GetComponent<Button>();
                if (btnComponent != null)
                {
                    string target = choice.targetId;
                    btnComponent.onClick.AddListener(() => OnChoiceSelected(target));
                }
            }
        }

        // ==========================================
        // 文字の表示開始（親パネルがActiveになったので、安全にコルーチンが動きます）
        // ==========================================
        if (typewriter != null)
        {
            bool hasText = !string.IsNullOrEmpty(data.eventParameter);
            typewriter.gameObject.SetActive(hasText);

            if (hasText)
            {
                typewriter.Play(data.eventParameter, ShowAllButtons);
            }
            else ShowAllButtons();
        }
        else ShowAllButtons();
    }

    private void ShowAllButtons()
    {
        foreach (var btn in spawnedButtons) { if (btn != null) btn.SetActive(true); }
    }

    private void OnChoiceSelected(string targetId)
    {
        if (choicesPanel != null) choicesPanel.SetActive(false);
        currentSelector.ProceedToNextTarget(targetId);
    }
}