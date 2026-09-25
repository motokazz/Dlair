using System.Collections.Generic;
using UnityEngine; // ← ★これが絶対に必要です！

[CreateAssetMenu(fileName = "New Story Graph", menuName = "Story/GraphView/New Graph")]
public class StoryGraph : ScriptableObject
{
    public List<BaseNode> nodes = new List<BaseNode>();
    public List<StoryLinkData> links = new List<StoryLinkData>();
    public List<StoryGroupData> groups = new List<StoryGroupData>();
    public List<StoryStickyNoteData> stickyNotes = new List<StoryStickyNoteData>();

    [HideInInspector]
    public string assetIdentity;

    public BaseNode GetNextNode(string currentGuid, string portName)
    {
        foreach (var link in links)
        {
            if (link.baseNodeGuid == currentGuid && link.portName == portName)
            {
                return nodes.Find(n => n.guid == link.targetNodeGuid);
            }
        }
        return null;
    }

    public LabelNode FindLabel(string labelGuid)
    {
        if (nodes == null || string.IsNullOrEmpty(labelGuid)) return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is LabelNode label && label.guid == labelGuid)
            {
                return label;
            }
        }

        return null;
    }

    public LabelNode FindLabelByName(string labelName)
    {
        if (nodes == null || string.IsNullOrWhiteSpace(labelName)) return null;

        string trimmed = labelName.Trim();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is LabelNode label && string.Equals(label.GetDisplayName(), trimmed, System.StringComparison.Ordinal))
            {
                return label;
            }
        }

        return null;
    }

    public bool TryFindLabel(string labelGuid, out StoryGraph owner, out LabelNode label)
    {
        return TryFindLabel(this, labelGuid, new HashSet<StoryGraph>(), out owner, out label);
    }

    public bool TryFindLabelByName(string labelName, out StoryGraph owner, out LabelNode label)
    {
        return TryFindLabelByName(this, labelName, new HashSet<StoryGraph>(), out owner, out label);
    }

    private static bool TryFindLabel(StoryGraph current, string labelGuid, HashSet<StoryGraph> visited, out StoryGraph owner, out LabelNode label)
    {
        owner = null;
        label = null;
        if (current == null || string.IsNullOrEmpty(labelGuid) || !visited.Add(current)) return false;

        label = current.FindLabel(labelGuid);
        if (label != null)
        {
            owner = current;
            return true;
        }

        if (current.nodes == null) return false;
        for (int i = 0; i < current.nodes.Count; i++)
        {
            if (current.nodes[i] is not SubGraphNode subGraphNode || subGraphNode.subGraph == null) continue;
            if (TryFindLabel(subGraphNode.subGraph, labelGuid, visited, out owner, out label)) return true;
        }

        return false;
    }

    private static bool TryFindLabelByName(StoryGraph current, string labelName, HashSet<StoryGraph> visited, out StoryGraph owner, out LabelNode label)
    {
        owner = null;
        label = null;
        if (current == null || string.IsNullOrWhiteSpace(labelName) || !visited.Add(current)) return false;

        label = current.FindLabelByName(labelName);
        if (label != null)
        {
            owner = current;
            return true;
        }

        if (current.nodes == null) return false;
        for (int i = 0; i < current.nodes.Count; i++)
        {
            if (current.nodes[i] is not SubGraphNode subGraphNode || subGraphNode.subGraph == null) continue;
            if (TryFindLabelByName(subGraphNode.subGraph, labelName, visited, out owner, out label)) return true;
        }

        return false;
    }

    public static bool WouldCreateCycle(StoryGraph parentGraph, StoryGraph nestedGraph)
    {
        if (parentGraph == null || nestedGraph == null) return false;
        if (parentGraph == nestedGraph) return true;
        return nestedGraph.ContainsGraph(parentGraph);
    }

    public bool ContainsGraph(StoryGraph target)
    {
        if (target == null) return false;
        return ContainsGraph(this, target, new HashSet<StoryGraph>());
    }

    private static bool ContainsGraph(StoryGraph current, StoryGraph target, HashSet<StoryGraph> visited)
    {
        if (current == null || target == null) return false;
        if (!visited.Add(current)) return false;

        if (current.nodes == null) return false;
        foreach (BaseNode node in current.nodes)
        {
            if (node is not SubGraphNode subGraphNode || subGraphNode.subGraph == null) continue;
            if (subGraphNode.subGraph == target) return true;
            if (ContainsGraph(subGraphNode.subGraph, target, visited)) return true;
        }

        return false;
    }
}