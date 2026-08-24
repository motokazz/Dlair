using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChoicesController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "ChoicesController";

    public GameObject choicesPanel;
    public TypewriterEffect typewriter;
    public Transform buttonContainer;
    public GameObject buttonPrefab;

    private VideoSelector currentSelector;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    public void StartGame(VideoSelector selector, MediaNode data) // ★引数を変更
    {
        currentSelector = selector;
        if (choicesPanel != null) choicesPanel.SetActive(true);

        foreach (var btnObj in spawnedButtons) Destroy(btnObj);
        spawnedButtons.Clear();

        if (data.choices != null)
        {
            for (int i = 0; i < data.choices.Count; i++) // ★for文に変更
            {
                var choice = data.choices[i];
                GameObject newBtn = Instantiate(buttonPrefab, buttonContainer);
                newBtn.SetActive(false);
                spawnedButtons.Add(newBtn);

                TMP_Text tmpText = newBtn.GetComponentInChildren<TMP_Text>();
                if (tmpText != null) tmpText.text = choice.branchKey;

                Button btnComponent = newBtn.GetComponent<Button>();
                if (btnComponent != null)
                {
                    int choiceIndex = i; // ★クロージャ対策（何番目のボタンか記憶）
                    btnComponent.onClick.AddListener(() => OnChoiceSelected(choiceIndex));
                }
            }
        }

        if (typewriter != null)
        {
            bool hasText = !string.IsNullOrEmpty(data.eventParameter);
            typewriter.gameObject.SetActive(hasText);

            if (hasText) typewriter.Play(data.eventParameter, ShowAllButtons);
            else ShowAllButtons();
        }
        else ShowAllButtons();
    }

    private void ShowAllButtons()
    {
        foreach (var btn in spawnedButtons) { if (btn != null) btn.SetActive(true); }
    }

    private void OnChoiceSelected(int choiceIndex) // ★インデックスを受け取る
    {
        if (choicesPanel != null) choicesPanel.SetActive(false);
        currentSelector.ProceedToBranch(choiceIndex); // ★線に沿って進む！
    }
}