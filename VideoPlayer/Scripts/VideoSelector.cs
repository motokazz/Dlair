using UnityEngine;
using UnityEngine.Video;

public class VideoSelector : MonoBehaviour
{
    [Header("Player Reference")]
    [Tooltip("先ほど作成した DualVideoPlayer をセットしてください")]
    public DualVideoPlayer dualPlayer;

    [Header("Playlist")]
    [Tooltip("選択可能にしたい動画をすべてここに登録します")]
    public VideoClip[] videoClips;

    /// <summary>
    /// UIボタンから呼び出すためのメソッド
    /// 引数(index)には、再生したい動画の配列番号(0, 1, 2...)を指定します
    /// </summary>
    public void OnVideoSelectButtonClicked(int index)
    {
        // 配列の範囲外エラーを防ぐ
        if (index < 0 || index >= videoClips.Length)
        {
            Debug.LogWarning("指定されたインデックスの動画が存在しません。");
            return;
        }

        // 動画がセットされていない場合は無視
        VideoClip selectedClip = videoClips[index];
        if (selectedClip == null) return;

        Debug.Log($"動画[{index}]の再生がリクエストされました。");

        // DualVideoPlayer に再生とトランジションを指示する
        dualPlayer.RequestPlayNextVideo(selectedClip);
    }
}