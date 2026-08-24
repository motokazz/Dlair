using UnityEngine;
using XNode;

[CreateAssetMenu(fileName = "NewStoryGraph", menuName = "VideoPlayer/Story Graph (New)")]
public class StoryGraph : NodeGraph
{
    public float defaultCrossfadeDuration = 1.0f;
    public BaseStoryNode startNode;
}