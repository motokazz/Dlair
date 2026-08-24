using UnityEngine;
using XNode;

[CreateNodeMenu("Story/4. Sub Graph (グループ化)")]
[NodeTint("#5c2a7a")] // 目立つように紫色にします
public class SubGraphNode : BaseStoryNode
{
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    [Header("呼び出す別のグラフ")]
    public StoryGraph subGraph;

    public override void Execute(VideoSelector selector)
    {
        // 本体に「このサブグラフを実行して、終わったらNextに戻ってきてね」と伝えます
        selector.EnterSubGraph(subGraph, GetNextNode("next"));
    }
}