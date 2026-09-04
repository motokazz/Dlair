using UnityEngine;

public class SubGraphNode : BaseNode
{
    public StoryGraph subGraph;

    public override void Execute(StoryPlayer player)
    {
        player.EnterSubGraph(subGraph, this);
    }
}
