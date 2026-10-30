using UnityEngine;

/// <summary>
/// サブグラフから呼び出し元へ戻る。サブグラフの外ならストーリーを終える。
/// </summary>
public class ExitNode : BaseNode
{
    public override void Execute(StoryPlayer player)
    {
        player.ExitSubGraphOrEnd();
    }
}
