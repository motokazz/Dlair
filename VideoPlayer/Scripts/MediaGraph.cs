using UnityEngine;
using XNode; // ★xNodeを使うための宣言

[CreateAssetMenu(fileName = "NewMediaGraph", menuName = "VideoPlayer/Media Graph (xNode)")]
public class MediaGraph : NodeGraph
{
    [Header("Playlist Global Settings")]
    [Tooltip("このプレイリスト全体の基本クロスフェード時間（秒）")]
    public float defaultCrossfadeDuration = 1.0f;

    // ★ グラフの中で「最初に再生する動画」を指定するための枠
    public MediaNode startNode;
}