using UnityEngine;

[CreateNodeMenu("Story/3. Add Parameter")]
public class ParameterNode : BaseStoryNode
{
    // ★修正：Flow を StoryFlow に変更
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    public string paramName = "A";
    public int addValue = 10;
    public override void Execute(VideoSelector selector)
    {
        // GameManagerに数値を足して、一瞬で次のノードへ進む
        GameManager.AddParameter(paramName, addValue);
        selector.ExecuteNode(GetNextNode("next"));
    }
}