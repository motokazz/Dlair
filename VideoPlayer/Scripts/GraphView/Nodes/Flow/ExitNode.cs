using UnityEngine;

public class ExitNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ExitSubGraphOrEnd();
    }
}
