using UnityEngine;

public class AutoBranchController : MonoBehaviour, IMiniGame
{
    public string MiniGameComponentId => "AutoBranchController";

    // ★ ここを MediaPlaylist.MediaData から MediaNode に変更しました
    public void StartGame(VideoSelector selector, MediaNode data)
    {
        // 最初の分岐線（Choicesの0番目）が繋がっていればそちらへ、
        // 繋がっていなければ通常のNextの線へ進む
        if (data.choices != null && data.choices.Count > 0)
        {
            selector.ProceedToBranch(0);
        }
        else
        {
            selector.PlayNextNode();
        }
    }
}