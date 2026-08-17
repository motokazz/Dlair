using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Video; // ★追加：VideoPlayerを操作するために必要

public class TapGameController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "TapGameController";

    [Header("UI")]
    public GameObject tapGamePanel;
    public TextMeshProUGUI countText;

    [Tooltip("画面全体を覆う透明な連打ボタン")]
    public Button tapButton;

    // ==========================================
    // ★追加：スピード変化の設定
    // ==========================================
    [Header("Speed Settings")]
    [Tooltip("1回タップするごとに増える再生スピードの量（例: 0.1 ならタップごとに 1.0 -> 1.1 -> 1.2 と加速）")]
    public float speedIncreasePerTap = 0.1f;

    private VideoSelector currentSelector;
    private MediaPlaylist.MediaData currentData;
    private int currentTapCount;
    private int currentTargetCount;

    public void StartGame(VideoSelector selector, MediaPlaylist.MediaData data)
    {
        currentSelector = selector;
        currentData = data;

        currentTapCount = 0;
        currentTargetCount = 10; // デフォルト目標回数

        if (!string.IsNullOrEmpty(data.eventParameter) && int.TryParse(data.eventParameter, out int parsedCount))
        {
            currentTargetCount = parsedCount;
        }

        // コールバックの自動登録
        if (tapButton != null)
        {
            tapButton.onClick.RemoveAllListeners();
            tapButton.onClick.AddListener(OnScreenTapped);
        }

        // ★ゲーム開始時に動画のスピードを標準(1.0)にリセットしておく
        ResetPlaybackSpeed();

        UpdateCountText();
        if (tapGamePanel != null) tapGamePanel.SetActive(true);
    }

    private void OnScreenTapped()
    {
        currentTapCount++;
        UpdateCountText();

        // ==========================================
        // ★追加：タップするたびに再生スピードを変える！
        // ==========================================
        VideoPlayer activePlayer = currentSelector.dualPlayer.GetActivePlayer();
        if (activePlayer != null)
        {
            // 基本スピード(1.0) ＋ (現在のタップ数 × 1タップあたりの増加量)
            activePlayer.playbackSpeed = 1.0f + (currentTapCount * speedIncreasePerTap);
        }

        // 目標回数に到達した時の処理
        if (currentTapCount >= currentTargetCount)
        {
            if (tapGamePanel != null) tapGamePanel.SetActive(false);

            // ★次の動画に進む前に、スピードを元の1.0に戻す（これをしないと次の動画も早回しになってしまいます）
            ResetPlaybackSpeed();

            if (currentData.choices != null && currentData.choices.Length > 0)
            {
                currentSelector.ProceedToNextTarget(currentData.choices[0].targetId);
            }
            else
            {
                currentSelector.PlayNextInPlaylist();
            }
        }
    }

    private void UpdateCountText()
    {
        if (countText != null) countText.text = $"{currentTapCount} / {currentTargetCount}";
    }

    // スピードを1.0x（等倍）に戻す安全機能
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