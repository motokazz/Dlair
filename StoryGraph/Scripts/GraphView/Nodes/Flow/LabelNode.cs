using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移動先の名前。到達すると訪問済みにし、Next へ進む。
/// </summary>
public class LabelNode : BaseNode
{
    public string labelName = "Label 1";

    public override void Execute(StoryPlayer player)
    {
        Debug.Log($"【Label】'{GetDisplayName()}'");
        StoryGotoPanel.NotifyVisited(GetDisplayName());
        player.ContinueTo(this, "Next");
    }

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(labelName) ? "Label" : labelName.Trim();
    }

    public string GetDisplayTitle()
    {
        return GetDisplayName();
    }

    public static string NextDefaultName(HashSet<string> usedNames)
    {
        int i = 1;
        while (usedNames != null && usedNames.Contains("Label " + i))
        {
            i++;
        }

        return "Label " + i;
    }
}
