using UnityEngine;

/// <summary>
/// グラフ上のノードの共通データ。識別子、位置、実行を持つ。
/// </summary>
public abstract class BaseNode : ScriptableObject
{
    public string guid;
    public Vector2 position;

    // ★引数を StoryPlayer に変更！
    public virtual void Execute(StoryPlayer player)
    {
    }
}