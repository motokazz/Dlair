using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 複数条件のまとめ方。全てが True か、いずれか 1 つが True か。
/// </summary>
public enum ConditionMultiMatch
{
    [InspectorName("全て")]
    All,
    [InspectorName("いずれか")]
    Any
}

/// <summary>
/// 1 行の参照先。変数か、再生記録か。
/// </summary>
public enum ConditionMultiSource
{
    [InspectorName("変数")]
    Variable,
    [InspectorName("再生記録")]
    Visit
}

/// <summary>
/// 複数条件の 1 行。変数比較か、再生記録の 0 / 1 比較。
/// </summary>
[Serializable]
public class ConditionMultiEntry
{
    public ConditionMultiSource source = ConditionMultiSource.Variable;
    public string variableName = "";
    public ConditionOperator comparison = ConditionOperator.GreaterThan;
    public int compareValue;

    public StoryGraph visitGraph;
    public string visitLabelGuid;
}

/// <summary>
/// 複数の比較をまとめて判定し、True / False へ進む。
/// 説明は ConditionMultiNode.Summary。
/// </summary>
public class ConditionMultiNode : BaseNode
{
    public const string Summary =
        "複数の条件をまとめて比べ、True / False へ進みます。\n" +
        "判定が「全て」のときは、すべての条件が True のとき True です。「いずれか」のときは、1 つでも True なら True です。\n" +
        "各条件は、変数か再生記録です。比較演算は Condition と同じです。Bool は Off=0 / On=1 です。\n" +
        "再生記録は StoryGotoUnlocks.json を見ます。選んだ StoryGraph の Label が記録にあれば 1、無ければ 0 として、同じ比較演算をします。\n" +
        "変数名はデータベースか変数宣言ノードから選びます。Label は Goto と同じ一覧から選びます。";

    public ConditionMultiMatch match = ConditionMultiMatch.All;

    public List<ConditionMultiEntry> conditions = new List<ConditionMultiEntry>
    {
        new ConditionMultiEntry()
    };

    /// <summary>
    /// 各条件を評価し、まとめた結果のポートへ進む。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        if (conditions == null || conditions.Count == 0)
        {
            Debug.LogWarning("【Condition Multi】条件がありません。False へ進みます。");
            if (player != null) player.ContinueTo(this, "False");
            return;
        }

        bool isTrue = Evaluate(out string detail);
        string mode = match == ConditionMultiMatch.Any ? "いずれか" : "全て";
        Debug.Log($"【Condition Multi】{mode} => {isTrue}（{detail}）");
        if (player != null)
        {
            player.ContinueTo(this, isTrue ? "True" : "False");
        }
    }

    /// <summary>
    /// グラフ上の説明文。Summary と同じです。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    /// <summary>
    /// ノードタイトル用の判定と条件一覧。
    /// </summary>
    public string GetDisplayTitle()
    {
        string mode = match == ConditionMultiMatch.Any ? "ANY" : "ALL";
        if (conditions == null || conditions.Count == 0) return mode;

        List<string> parts = new List<string>(conditions.Count);
        for (int i = 0; i < conditions.Count; i++)
        {
            ConditionMultiEntry entry = conditions[i];
            if (entry == null) continue;
            parts.Add(Describe(entry));
        }

        return parts.Count == 0 ? mode : mode + "  " + string.Join(", ", parts);
    }

    /// <summary>
    /// いまの条件をまとめて True か。条件が無いときは False。
    /// </summary>
    public bool Evaluate(out string detail)
    {
        if (conditions == null || conditions.Count == 0)
        {
            detail = "条件なし";
            return false;
        }

        int trueCount = 0;
        List<string> parts = new List<string>(conditions.Count);
        for (int i = 0; i < conditions.Count; i++)
        {
            bool row = EvaluateEntry(conditions[i], out string rowDetail);
            if (row) trueCount++;
            parts.Add(rowDetail);
        }

        detail = string.Join(" / ", parts);
        if (match == ConditionMultiMatch.Any) return trueCount > 0;
        return trueCount == conditions.Count;
    }

    private static bool EvaluateEntry(ConditionMultiEntry entry, out string detail)
    {
        if (entry == null)
        {
            detail = "? => False";
            return false;
        }

        int current = ReadLeft(entry, out string left);
        bool result = Compare(current, entry.comparison, entry.compareValue);
        detail = $"{left}({current}) {ConditionNode.GetOperatorSymbol(entry.comparison)} {entry.compareValue} => {result}";
        return result;
    }

    private static int ReadLeft(ConditionMultiEntry entry, out string left)
    {
        if (entry.source == ConditionMultiSource.Visit)
        {
            left = "Visit";
            if (entry.visitGraph == null || string.IsNullOrEmpty(entry.visitLabelGuid)) return 0;

            if (StoryVisitButtonEntry.IsStartTarget(entry.visitLabelGuid))
            {
                left = StoryVisitButtonEntry.StartTarget;
                if (StoryPlayer.FindStartNode(entry.visitGraph) == null) return 0;
                return StoryVisitLog.IsVisited(entry.visitGraph, StoryVisitButtonEntry.StartTarget) ? 1 : 0;
            }

            LabelNode label = entry.visitGraph.FindLabel(entry.visitLabelGuid);
            if (label == null) return 0;

            left = label.GetDisplayName();
            return StoryVisitLog.IsVisited(entry.visitGraph, label.GetDisplayName()) ? 1 : 0;
        }

        left = string.IsNullOrEmpty(entry.variableName) ? "?" : entry.variableName;
        if (GameManager.HasBool(entry.variableName))
        {
            return GameManager.GetBool(entry.variableName) ? 1 : 0;
        }

        return GameManager.GetParameter(entry.variableName);
    }

    private static string Describe(ConditionMultiEntry entry)
    {
        string left;
        if (entry.source == ConditionMultiSource.Visit)
        {
            if (StoryVisitButtonEntry.IsStartTarget(entry.visitLabelGuid))
                left = StoryVisitButtonEntry.StartTarget;
            else
            {
                LabelNode label = entry.visitGraph != null ? entry.visitGraph.FindLabel(entry.visitLabelGuid) : null;
                left = label != null ? label.GetDisplayName() : "Visit";
            }
        }
        else
        {
            left = string.IsNullOrEmpty(entry.variableName) ? "?" : entry.variableName;
        }

        return left + " " + ConditionNode.GetOperatorSymbol(entry.comparison) + " " + entry.compareValue;
    }

    private static bool Compare(int left, ConditionOperator op, int right)
    {
        switch (op)
        {
            case ConditionOperator.GreaterThan:
                return left > right;
            case ConditionOperator.GreaterOrEqual:
                return left >= right;
            case ConditionOperator.Equal:
                return left == right;
            case ConditionOperator.LessOrEqual:
                return left <= right;
            case ConditionOperator.LessThan:
                return left < right;
            case ConditionOperator.NotEqual:
                return left != right;
            default:
                return false;
        }
    }
}
