using UnityEngine;

public class AutoBranchController : MonoBehaviour, IMiniGame
{
    // ★ 私は「AutoBranchController」です！
    public string MiniGameComponentId => "AutoBranchController";

    public void StartGame(VideoSelector selector, MediaPlaylist.MediaData data)
    {
        // リストの1番目に設定された動画へ自動で飛ぶ
        if (data.choices != null && data.choices.Length > 0)
        {
            selector.ProceedToNextTarget(data.choices[0].targetId);
        }
        else
        {
            // 設定忘れの場合は次へ進む
            selector.PlayNextInPlaylist();
        }
    }
}