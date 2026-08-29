using UnityEngine;

public class StartFlowNode : BaseFlowNode
{
    public override void Execute(StoryFlowPlayer player)
    {
        Debug.Log("【Start Node】次のノードを探します。");
        BaseFlowNode nextNode = player.flowGraph.GetNextNode(this.guid, "Next");

        if (nextNode != null)
        {
            nextNode.Execute(player);
        }
    }
}