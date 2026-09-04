using UnityEngine;

public class StartNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ContinueTo(this, "Next");
    }
}