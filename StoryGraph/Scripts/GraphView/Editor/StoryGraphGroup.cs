using UnityEditor.Experimental.GraphView;

/// <summary>
/// グラフ編集画面のグループ。ノードと付箋を入れ、グループの入れ子は受け付けない。
/// </summary>
public class StoryGraphGroup : Group
{
    public string guid;

    public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
    {
        if (element is StoryGraphGroup)
        {
            reasonWhyNotAccepted = "グループを入れ子にはできません。";
            return false;
        }

        if (element is StoryNodeUI || element is StoryStickyNote)
        {
            return true;
        }

        return base.AcceptsElement(element, ref reasonWhyNotAccepted);
    }
}
