using UnityEngine;
using UnityEngine.UI;

public class MessageController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "MessageController";

    [Header("UI Elements")]
    public GameObject messagePanel;
    public TypewriterEffect typewriter;

    [Tooltip("画面全体を覆う透明なボタン（クリック判定用）")]
    public Button tapButton;

    private VideoSelector currentSelector;
    private MediaNode currentData; // ★重複しないように一箇所だけに修正
    private bool isTyping = false;

    public void StartGame(VideoSelector selector, MediaNode data)
    {
        currentSelector = selector;
        currentData = data;

        if (messagePanel != null) messagePanel.SetActive(true);

        if (tapButton != null)
        {
            tapButton.onClick.RemoveAllListeners();
            tapButton.onClick.AddListener(OnScreenTapped);
        }

        if (typewriter != null && !string.IsNullOrEmpty(data.eventParameter))
        {
            isTyping = true;
            typewriter.Play(data.eventParameter, () => isTyping = false);
        }
        else
        {
            isTyping = false;
        }
    }

    private void OnScreenTapped()
    {
        if (isTyping)
        {
            typewriter.Skip();
        }
        else
        {
            if (messagePanel != null) messagePanel.SetActive(false);

            if (currentData.choices != null && currentData.choices.Count > 0)
                currentSelector.ProceedToBranch(0); // ★ノードの線に沿って進む
            else
                currentSelector.PlayNextNode(); // ★通常遷移
        }
    }
}