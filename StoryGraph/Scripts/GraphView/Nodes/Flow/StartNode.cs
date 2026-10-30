using UnityEngine;

/// <summary>
/// ストーリーの開始点。Next へ進む。
/// </summary>
public class StartNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ContinueTo(this, "Next");
    }
}