using UnityEngine;

/// <summary>
/// ストーリーの開始点。到達すると再生記録に Start を足し、Next へ進む。
/// Start ノードの GUID は記録しない。
/// </summary>
public class StartNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        StoryVisitLog.Record(player != null ? player.ActiveGraph : null, StoryVisitButtonEntry.StartTarget);
        player.ContinueTo(this, "Next");
    }
}