using UnityEngine;
using UnityEngine.Video;
using XNode;
using System.Collections.Generic;

// これを付けると、ノードを作るメニューに表示されます
[CreateNodeMenu("Media/Video Node")]
public class MediaNode : Node
{
    // ★ノードを繋ぐ「線」のためのダミー型（見た目用）
    [System.Serializable] public struct Flow { }

    // ==========================================
    // 入出力ポート（ここから線を引っ張る！）
    // ==========================================
    [Input(ShowBackingValue.Never, ConnectionType.Multiple)]
    public Flow enter; // 前の動画から入ってくる線

    [Output(ShowBackingValue.Never, ConnectionType.Override)]
    public Flow next;  // 次の動画へ向かう線（通常遷移）

    // ==========================================
    // 今まで通りの動画データ
    // ==========================================
    public string title = "New Media";
    public Sprite thumbnail;
    public bool isStaticImage;
    public VideoClip clip;
    public Texture2D image;
    public float imageDuration = 5.0f;
    public AudioClip audioClip;
    public bool isLooping = true;

    [Header("Crossfade")]
    public bool overrideCrossfade = false;
    public float customCrossfadeDuration = 1.0f;

    [Header("Interactive Event")]
    public string eventId = "None";
    public float eventTriggerTime = 0f;
    public string eventParameter;

    // ==========================================
    // ★大進化：Choices（分岐ボタン）
    // targetId（文字）を廃止し、ノードの線を直接出せるようにする設定！
    // ==========================================
    [System.Serializable]
    public class BranchChoice
    {
        public string branchKey = "Button Text";
    }

    [Output(dynamicPortList = true)] // リストの数だけ「出力ポート（丸ポッチ）」が自動で増える魔法の設定！
    public List<BranchChoice> choices = new List<BranchChoice>();

    // ==========================================
    // xNode必須のメソッド（値を渡すグラフではないので null を返すだけでOK）
    // ==========================================
    public override object GetValue(NodePort port)
    {
        return null;
    }

    // 次のノード（通常再生）を取得する便利メソッド
    public MediaNode GetNextNode()
    {
        NodePort port = GetOutputPort("next");
        if (port != null && port.IsConnected) return port.Connection.node as MediaNode;
        return null;
    }

    // 指定した分岐先のノードを取得する便利メソッド
    public MediaNode GetBranchTarget(int index)
    {
        NodePort port = GetOutputPort("choices " + index);
        if (port != null && port.IsConnected) return port.Connection.node as MediaNode;
        return null;
    }
}