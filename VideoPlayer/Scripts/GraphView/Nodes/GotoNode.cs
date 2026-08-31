using UnityEngine;

public class GotoNode : BaseNode
{
    public string targetLabelGuid;
    public StoryGraph targetGraph;

    public override void Execute(StoryPlayer player)
    {
        player.JumpToLabel(targetGraph, targetLabelGuid, this);
    }

    public string GetDisplayTitle()
    {
        LabelNode label = targetGraph != null ? targetGraph.FindLabel(targetLabelGuid) : null;
        if (label != null)
        {
            return "GOTO  " + label.GetDisplayName();
        }

        return string.IsNullOrEmpty(targetLabelGuid) ? "GOTO  未選択" : "GOTO  ?";
    }
}
