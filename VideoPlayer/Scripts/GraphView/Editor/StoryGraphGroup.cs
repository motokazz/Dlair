using UnityEditor.Experimental.GraphView;

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
