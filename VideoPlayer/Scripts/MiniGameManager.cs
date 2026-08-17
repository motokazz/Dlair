using UnityEngine;

public class MiniGameManager : MonoBehaviour
{
    public VideoSelector videoSelector;
    private IMiniGame[] miniGames;

    private void Start()
    {
        miniGames = GetComponentsInChildren<IMiniGame>(true);
    }

    public void ReceiveCustomEvent(MediaPlaylist.MediaData data)
    {
        string targetComponentId = data.eventId;

        // ★ AutoBranchの除外を削除
        if (string.IsNullOrEmpty(targetComponentId) || targetComponentId == "None") return;

        foreach (var game in miniGames)
        {
            if (game.MiniGameComponentId == targetComponentId)
            {
                // AutoBranchの時はログが出るとうるさいので非表示にする配慮
                if (targetComponentId != "AutoBranchController")
                {
                    Debug.Log($"<color=cyan>[Manager]</color> '{targetComponentId}' を開始します！");
                }
                game.StartGame(videoSelector, data);
                return;
            }
        }

        Debug.LogWarning($"<color=red>[Manager]</color> 対応するスクリプト '{targetComponentId}' がシーン上に見つかりません！");
    }
}