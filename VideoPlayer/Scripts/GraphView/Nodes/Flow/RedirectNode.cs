using UnityEngine;

public class RedirectNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ContinueTo(this, "Next");
    }
}
