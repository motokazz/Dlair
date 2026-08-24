using UnityEngine;
using XNode;

// ★エディタ用のusingも、一番上で宣言します
#if UNITY_EDITOR
using UnityEditor;
using XNodeEditor;
#endif

[CreateNodeMenu("Story/0. Start Node")]
[NodeTint("#2e7d32")] // 目立つようにノードの色を深い緑色にします
public class StartNode : BaseStoryNode
{
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    public override void Execute(VideoSelector selector)
    {
        // 自分自身は何もせず、繋がっている次のノードへ即座にパスを回す
        selector.ExecuteNode(GetNextNode("next"));
    }
}

// ==========================================
// 見た目を綺麗にする専用エディタ
// ==========================================
#if UNITY_EDITOR
[CustomNodeEditor(typeof(StartNode))]
public class StartNodeEditor : NodeEditor 
{
    public override void OnBodyGUI() 
    {
        // 本来親クラスにある「Enter（入力）」の丸ポッチをあえて隠し、
        // Next（出力）だけを描画することで、「ここが完全なスタート地点」であることを強調します。
        NodeEditorGUILayout.PortField(target.GetOutputPort("next"));
        
        GUILayout.Space(5);
        GUILayout.Label("ここからストーリーが始まります", EditorStyles.boldLabel);
    }
}
#endif