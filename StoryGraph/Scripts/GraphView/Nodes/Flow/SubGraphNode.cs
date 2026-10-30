using UnityEngine;

/// <summary>
/// 別の StoryGraph に入り、その Exit から戻ってくる。
/// </summary>
public class SubGraphNode : BaseNode
{
    public StoryGraph subGraph;

    public override void Execute(StoryPlayer player)
    {
        player.EnterSubGraph(subGraph, this);
    }
}
