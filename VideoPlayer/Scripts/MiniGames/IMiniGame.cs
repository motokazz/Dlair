using UnityEngine;

public interface IMiniGame
{
    string MiniGameComponentId { get; }
    // ★ MediaPlaylist.MediaData から MediaNode に変更
    void StartGame(VideoSelector selector, MediaNode data);
}