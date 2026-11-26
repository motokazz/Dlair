using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// この StoryGraph の中だけで有効な変数をまとめて宣言する。
/// 同名のグローバル変数があっても、このグラフの実行中はこちらを使う。セーブしない。
/// </summary>
public class LocalVariableDeclareNode : BaseNode, IDeclarationList
{
    public const string Summary =
        "このグラフの中だけで有効な変数を宣言する。別のグラフの同名変数とは別物。このグラフの実行中は、同名のグローバル変数よりこちらを使う。セーブしない。";

    public List<VariableDeclaration> variables = new List<VariableDeclaration>
    {
        new VariableDeclaration()
    };

    public List<VariableDeclaration> Variables
    {
        get => variables;
        set => variables = value;
    }

    public string DeclarationHelp => GetHelpText();

    /// <summary>
    /// このグラフのローカル変数へ宣言値を入れ、Next へ進む。
    /// SubGraph Input が直前に書いた値は、この通過では戻さない。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        StoryLocalVariables.Write(this, true);
        if (player != null)
        {
            player.ContinueTo(this, "Next");
        }
    }

    /// <summary>
    /// ノードタイトル用の変数名一覧。
    /// </summary>
    public string GetDisplayTitle()
    {
        if (variables == null || variables.Count == 0) return "Local";

        List<string> parts = new List<string>(variables.Count);
        for (int i = 0; i < variables.Count; i++)
        {
            VariableDeclaration entry = variables[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key)) continue;
            parts.Add(entry.key.Trim());
        }

        return parts.Count == 0 ? "Local" : string.Join(", ", parts);
    }

    /// <summary>
    /// グラフ上の説明文。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }
}
