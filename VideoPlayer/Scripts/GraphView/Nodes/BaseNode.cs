using UnityEngine;

public abstract class BaseNode : ScriptableObject
{
    public string guid;
    public Vector2 position;

    // ★引数を StoryPlayer に変更！
    public virtual void Execute(StoryPlayer player)
    {
    }
}