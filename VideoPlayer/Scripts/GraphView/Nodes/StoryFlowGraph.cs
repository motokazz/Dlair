using System.Collections.Generic;
using UnityEngine; // ← ★これが絶対に必要です！

[CreateAssetMenu(fileName = "New Story Flow Graph", menuName = "Story/GraphView/New Flow Graph")]
public class StoryFlowGraph : ScriptableObject
{
    public List<BaseFlowNode> nodes = new List<BaseFlowNode>();
    public List<FlowLinkData> links = new List<FlowLinkData>();

    public BaseFlowNode GetNextNode(string currentGuid, string portName)
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
}