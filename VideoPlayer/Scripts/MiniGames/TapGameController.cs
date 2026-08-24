using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Video;

public class TapGameController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "TapGameController";

    [Header("UI")]
    public GameObject tapGamePanel;
    public TextMeshProUGUI countText;
    public Button tapButton;

    [Header("Speed Settings")]
    public float speedIncreasePerTap = 0.1f;

    private VideoSelector currentSelector;
    private MediaNode currentData;
    private int currentTapCount;
    private int currentTargetCount;

    public void StartGame(VideoSelector selector, MediaNode data)
    {
        currentSelector = selector;
        currentData = data;

        currentTapCount = 0;
        currentTargetCount = 10;

        if (!string.IsNullOrEmpty(data.eventParameter) && int.TryParse(data.eventParameter, out int parsedCount))
        {
            currentTargetCount = parsedCount;
        }

        if (tapButton != null)
        {
            tapButton.onClick.RemoveAllListeners();
            tapButton.onClick.AddListener(OnScreenTapped);
        }

        ResetPlaybackSpeed();
        UpdateCountText();
        if (tapGamePanel != null) tapGamePanel.SetActive(true);
    }

    private void OnScreenTapped()
    {
        currentTapCount++;
        UpdateCountText();

        VideoPlayer activePlayer = currentSelector.dualPlayer.GetActivePlayer();
        if (activePlayer != null)
        {
            activePlayer.playbackSpeed = 1.0f + (currentTapCount * speedIncreasePerTap);
        }

        if (currentTapCount >= currentTargetCount)
        {
            if (tapGamePanel != null) tapGamePanel.SetActive(false);
            ResetPlaybackSpeed();

            if (currentData.choices != null && currentData.choices.Count > 0)
            {
                currentSelector.ProceedToBranch(0);
            }
            else
            {
                currentSelector.PlayNextNode();
            }
        }
    }

    private void UpdateCountText()
    {
        if (countText != null) countText.text = $"{currentTapCount} / {currentTargetCount}";
    }

    private void ResetPlaybackSpeed()
    {
        if (currentSelector != null && currentSelector.dualPlayer != null)
        {
            VideoPlayer activePlayer = currentSelector.dualPlayer.GetActivePlayer();
            if (activePlayer != null)
            {
                activePlayer.playbackSpeed = 1.0f;
            }
        }
    }
}