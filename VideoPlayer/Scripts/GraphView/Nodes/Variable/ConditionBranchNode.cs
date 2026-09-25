using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, null, null, "ThresholdBranchEntry")]
public class ConditionBranchEntry
{
    public string name = "A";
    public float threshold = 0f;
}

[MovedFrom(true, null, null, "ThresholdBranchNode")]
public class ConditionBranchNode : BaseNode
{
    public string variableName = "A";
    public ConditionOperator comparison = ConditionOperator.GreaterThan;
    public List<ConditionBranchEntry> branches = new List<ConditionBranchEntry>
    {
        new ConditionBranchEntry { name = "A", threshold = 0f },
        new ConditionBranchEntry { name = "B", threshold = 1f }
    };

    public override void Execute(StoryPlayer player)
    {
        float currentValue = GameManager.TryGetNumeric(variableName, out float value) ? value : 0f;
        ConditionBranchEntry selected = PickBranch(currentValue);
        if (selected == null || string.IsNullOrEmpty(selected.name))
        {
            Debug.LogWarning("【Condition Branch】分岐先がありません。");
            return;
        }

        string op = ConditionNode.GetOperatorSymbol(comparison);
        if (Matches(currentValue, comparison, selected.threshold))
        {
            Debug.Log($"【Condition Branch】{variableName}={currentValue} {op} {selected.threshold} → '{selected.name}'");
        }
        else
        {
            Debug.Log($"【Condition Branch】{variableName}={currentValue} は該当なしのためフォールバック '{selected.name}' ({selected.threshold}) へ");
        }

        player.ContinueTo(this, selected.name);
    }

    public ConditionBranchEntry PickBranch(float currentValue)
    {
        if (branches == null || branches.Count == 0) return null;

        bool preferHighest = PrefersHighestMatch(comparison);
        ConditionBranchEntry best = null;
        ConditionBranchEntry smallest = null;
        ConditionBranchEntry largest = null;
        for (int i = 0; i < branches.Count; i++)
        {
            ConditionBranchEntry entry = branches[i];
            if (entry == null || string.IsNullOrEmpty(entry.name)) continue;

            if (smallest == null || entry.threshold < smallest.threshold)
            {
                smallest = entry;
            }

            if (largest == null || entry.threshold > largest.threshold)
            {
                largest = entry;
            }

            if (!Matches(currentValue, comparison, entry.threshold)) continue;
            if (best == null)
            {
                best = entry;
                continue;
            }

            if (preferHighest)
            {
                if (entry.threshold > best.threshold) best = entry;
            }
            else if (PrefersLowestMatch(comparison))
            {
                if (entry.threshold < best.threshold) best = entry;
            }
        }

        if (best != null) return best;
        return preferHighest || !PrefersLowestMatch(comparison) ? smallest : largest;
    }

    public string GetDisplayTitle()
    {
        string varLabel = string.IsNullOrEmpty(variableName) ? "?" : variableName;
        string op = ConditionNode.GetOperatorSymbol(comparison);
        if (branches == null || branches.Count == 0) return $"{varLabel}  {op}";

        List<string> parts = new List<string>(branches.Count);
        for (int i = 0; i < branches.Count; i++)
        {
            ConditionBranchEntry entry = branches[i];
            if (entry == null) continue;
            parts.Add(FormatThreshold(entry.threshold));
        }

        return parts.Count == 0 ? $"{varLabel}  {op}" : $"{varLabel}  {op} {string.Join(":", parts)}";
    }

    public string GetHelpText()
    {
        string op = ConditionNode.GetOperatorSymbol(comparison);
        switch (comparison)
        {
            case ConditionOperator.LessThan:
            case ConditionOperator.LessOrEqual:
                return $"現在値が設定値 {op} のときに分岐します。複数該当する場合はいちばん低い設定値へ。どれも該当しなければいちばん大きい設定値の分岐へ。";
            case ConditionOperator.Equal:
                return "現在値が設定値と等しいときに分岐します。一致がなければいちばん小さい設定値の分岐へ。";
            default:
                return $"現在値が設定値 {op} のときに分岐します。複数該当する場合はいちばん高い設定値へ。どれも該当しなければいちばん小さい設定値の分岐へ。";
        }
    }

    public string GetThresholdTooltip()
    {
        string op = ConditionNode.GetOperatorSymbol(comparison);
        return $"現在値がこの値 {op} のときに、この分岐の候補になります";
    }

    public static string NextDefaultName(List<ConditionBranchEntry> list)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        HashSet<string> used = new HashSet<string>();
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && !string.IsNullOrEmpty(list[i].name))
                {
                    used.Add(list[i].name);
                }
            }
        }

        for (int i = 0; i < alphabet.Length; i++)
        {
            string candidate = alphabet[i].ToString();
            if (!used.Contains(candidate)) return candidate;
        }

        int n = 1;
        while (used.Contains("Branch " + n)) n++;
        return "Branch " + n;
    }

    public static float NextDefaultThreshold(List<ConditionBranchEntry> list)
    {
        float max = float.NegativeInfinity;
        bool any = false;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                any = true;
                if (list[i].threshold > max) max = list[i].threshold;
            }
        }

        return any ? max + 1f : 0f;
    }

    public static bool Matches(float currentValue, ConditionOperator op, float threshold)
    {
        switch (op)
        {
            case ConditionOperator.GreaterThan:
                return currentValue > threshold;
            case ConditionOperator.GreaterOrEqual:
                return currentValue >= threshold;
            case ConditionOperator.Equal:
                return Mathf.Approximately(currentValue, threshold);
            case ConditionOperator.LessOrEqual:
                return currentValue <= threshold;
            case ConditionOperator.LessThan:
                return currentValue < threshold;
            case ConditionOperator.NotEqual:
                return !Mathf.Approximately(currentValue, threshold);
            default:
                return false;
        }
    }

    private static bool PrefersHighestMatch(ConditionOperator op)
    {
        return op == ConditionOperator.GreaterThan || op == ConditionOperator.GreaterOrEqual;
    }

    private static bool PrefersLowestMatch(ConditionOperator op)
    {
        return op == ConditionOperator.LessThan || op == ConditionOperator.LessOrEqual;
    }

    private static string FormatThreshold(float threshold)
    {
        if (Mathf.Approximately(threshold, Mathf.Round(threshold)))
        {
            return Mathf.RoundToInt(threshold).ToString();
        }

        return threshold.ToString("0.##");
    }
}
