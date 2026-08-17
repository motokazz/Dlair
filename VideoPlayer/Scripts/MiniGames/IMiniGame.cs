using UnityEngine;

public interface IMiniGame
{
    // ★ Enumをやめ、自分のクラス名（ID）を名乗るだけにする
    string MiniGameComponentId { get; }
    void StartGame(VideoSelector selector, MediaPlaylist.MediaData data);
}