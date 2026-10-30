using UnityEngine;

/// <summary>
/// 処理をせず Next へつなぐ中継ノード。
/// </summary>
public class RedirectNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ContinueTo(this, "Next");
    }
}
