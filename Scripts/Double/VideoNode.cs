using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;  // Input System用
using System.Collections.Generic;

[CreateAssetMenu(fileName = "VideoNode", menuName = "FMV/VideoNode")]
public class VideoNode : ScriptableObject
{
    [Header("再生設定")]
    public VideoClip videoClip;                     // このノードで再生する動画

    public List<SuccessBranch> branches = new List<SuccessBranch>();

    [Header("失敗分岐")]
    public VideoNode failNode;                 // 失敗/タイムアウトで次へ（死亡動画など）

    [Header("ノードフラグ")]
    public bool isStartNode = false;           // 開始ノードか
    public bool isDeathNode = false;           // 死亡ノードか（ここでライフ減らす）
    public bool isEndNode = false;             // クリアノードか
    public bool isLoop;
    public bool playContinue = false;
}
[System.Serializable]
public class SuccessBranch
{
    [Header("成功分岐")]
    public VideoNode successNode;              // 正しい入力で次へ
    public float inputStart = 1.0f;
    public float inputWindow = 1.0f;           // 入力受付時間（秒）
    public string actionName = "RightArrow";  // Action名（文字列）
    public bool skipVideo = false;
    public SuccessCondition condition;

    [Header("失敗分岐")]
    public VideoNode failNode;                 // 失敗/タイムアウトで次へ（死亡動画など）

    public enum SuccessCondition
    {
        AnyInput,
        QTE,
        Custom
    }
}