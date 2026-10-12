using UnityEngine;

public class GotoNode : BaseNode
{
    public string targetLabelGuid;
    public StoryGraph targetGraph;

    public override void Execute(StoryPlayer player)
    {
        player.JumpToLabel(targetGraph != null ? targetGraph : player.ActiveGraph, targetLabelGuid, this);
    }

    public string GetDisplayTitle()
    {
        LabelNode label = ResolveLabel();
        if (label != null)
        {
            return "GOTO  " + label.GetDisplayName();
        }

        return string.IsNullOrEmpty(targetLabelGuid) ? "GOTO  未選択" : "GOTO  ?";
    }

    public LabelNode ResolveLabel()
    {
        if (string.IsNullOrEmpty(targetLabelGuid)) return null;

        if (targetGraph != null)
        {
            LabelNode label = targetGraph.FindLabel(targetLabelGuid);
            if (label != null) return label;
        }

        return FindLabelOnSameAsset();
    }

    private LabelNode FindLabelOnSameAsset()
    {
#if UNITY_EDITOR
        string path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(path)) return null;

        StoryGraph owner = UnityEditor.AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
        if (owner != null && owner != targetGraph)
        {
            LabelNode label = owner.FindLabel(targetLabelGuid);
            if (label != null) return label;
        }

        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets == null) return null;

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is LabelNode label && label.guid == targetLabelGuid)
            {
                return label;
            }
        }
#endif
        return null;
    }
}
