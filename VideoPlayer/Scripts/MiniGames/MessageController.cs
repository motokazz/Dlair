using UnityEngine;
using UnityEngine.UI; // ★追加：Buttonを使うために必要

public class MessageController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "MessageController";

    [Header("UI Elements")]
    public GameObject messagePanel;
    public TypewriterEffect typewriter;

    // ==========================================
    // ★追加：画面全体を覆う透明なボタン
    // ==========================================
    [Tooltip("画面全体を覆う透明なボタン（クリック判定用）")]
    public Button tapButton;

    private VideoSelector currentSelector;
    private MediaPlaylist.MediaData currentData;
    private bool isTyping = false;

    public void StartGame(VideoSelector selector, MediaPlaylist.MediaData data)
    {
        currentSelector = selector;
        currentData = data;

        // パネルを一番最初に表示（コルーチン対策）
        if (messagePanel != null) messagePanel.SetActive(true);

        // ==========================================
        // ★大修正：コードから直接コールバック（リスナー）を登録！
        // ==========================================
        if (tapButton != null)
        {
            tapButton.onClick.RemoveAllListeners(); // 古いコールバックをお掃除
            tapButton.onClick.AddListener(OnScreenTapped); // コールバックを登録
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

    // コールバックとして自動的に呼ばれるメソッド（publicである必要もなくなりました！）
    private void OnScreenTapped()
    {
        if (isTyping)
        {
            // まだ文字が表示中なら、スキップして全表示する
            typewriter.Skip();
        }
        else
        {
            // 全表示されているなら、次の動画へ進む
            if (messagePanel != null) messagePanel.SetActive(false);

            if (currentData.choices != null && currentData.choices.Length > 0)
                currentSelector.ProceedToNextTarget(currentData.choices[0].targetId);
            else
                currentSelector.PlayNextInPlaylist();
        }
    }
}