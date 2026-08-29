using UnityEngine;

public abstract class BaseFlowNode : ScriptableObject
{
    public string guid;
    public Vector2 position;

    // ★引数を StoryFlowPlayer に変更！
    public virtual void Execute(StoryFlowPlayer player)
    {
    }
}