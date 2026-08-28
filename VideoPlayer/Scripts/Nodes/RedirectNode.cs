using UnityEngine;
using XNode;

// ★usingは必ず一番上で宣言します
#if UNITY_EDITOR
using UnityEditor;
using XNodeEditor;
#endif

[CreateNodeMenu("Story/99. Redirect (中継)")]
[NodeWidth(60)] // ★ノードの横幅を限界まで小さくする
[NodeTint("#555555")] // 目立たないダークグレー
public class RedirectNode : BaseStoryNode
{
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    public override void Execute(VideoSelector selector)
    {
        // 自分は何もしない。ただ繋がっている次のノードへパスを回すだけ！
        selector.ExecuteNode(GetNextNode("next"));
    }
}

// ==========================================
// 極小サイズにするための専用エディタ（見た目カスタム）
// ==========================================
#if UNITY_EDITOR
[CustomNodeEditor(typeof(RedirectNode))]
public class RedirectNodeEditor : NodeEditor 
{
    // ★タイトルバー（上部の名前部分）を完全に非表示にする魔法
    public override void OnHeaderGUI() 
    {
        // 何も書かないことでヘッダーが消滅します
    }

    public override void OnBodyGUI() 
    {
        // 上下の余白を極限まで詰める
        GUILayout.Space(-5);

        GUILayout.BeginHorizontal();
        
        // 左側：入力ポート（文字なし）
        NodePort inputPort = target.GetInputPort("enter");
        if (inputPort != null) NodeEditorGUILayout.PortField(new GUIContent(""), inputPort, GUILayout.Width(16));
        
        // 真ん中の空間
        GUILayout.FlexibleSpace();
        
        // 右側：出力ポート（文字なし）
        NodePort outputPort = target.GetOutputPort("next");
        if (outputPort != null) NodeEditorGUILayout.PortField(new GUIContent(""), outputPort, GUILayout.Width(16));
        
        GUILayout.EndHorizontal();

        GUILayout.Space(-5);
    }
}
#endif