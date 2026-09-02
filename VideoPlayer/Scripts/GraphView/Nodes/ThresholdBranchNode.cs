using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ThresholdBranchEntry
{
    public string name = "A";
    public float threshold = 0f;
}

public class ThresholdBranchNode : BaseNode
{
    public string variableName = "A";
    public List<ThresholdBranchEntry> branches = new List<ThresholdBranchEntry>
    {
        new ThresholdBranchEntry { name = "A", threshold = 0f },
        new ThresholdBranchEntry { name = "B", threshold = 10f }
    };

    public override void Execute(StoryPlayer player)
    {
        float currentValue = GameManager.TryGetNumeric(variableName, out float value) ? value : 0f;
        ThresholdBranchEntry selected = PickBranch(currentValue);
        if (selected == null || string.IsNullOrEmpty(selected.name))
        {
            Debug.LogWarning("【Threshold Branch】分岐先がありません。");
            return;
        }

        if (currentValue > selected.threshold)
        {
            Debug.Log($"【Threshold Branch】{variableName}={currentValue} > {selected.threshold} → '{selected.name}'");
        }
        else
        {
            Debug.Log($"【Threshold Branch】{variableName}={currentValue} は未達のため最小設定値 '{selected.name}' ({selected.threshold}) へ");
        }

        player.ContinueTo(this, selected.name);
    }

    public ThresholdBranchEntry PickBranch(float currentValue)
    {
        if (branches == null || branches.Count == 0) return null;

        ThresholdBranchEntry best = null;
        ThresholdBranchEntry smallest = null;
        for (int i = 0; i < branches.Count; i++)
        {
            ThresholdBranchEntry entry = branches[i];
            if (entry == null || string.IsNullOrEmpty(entry.name)) continue;

            if (smallest == null || entry.threshold < smallest.threshold)
            {
                smallest = entry;
            }

            if (currentValue <= entry.threshold) continue;
            if (best == null || entry.threshold > best.threshold)
            {
                best = entry;
            }
        }

        return best != null ? best : smallest;
    }

    public string GetDisplayTitle()
    {
        string varLabel = string.IsNullOrEmpty(variableName) ? "?" : variableName;
        if (branches == null || branches.Count == 0) return $"{varLabel}  >";

        List<string> parts = new List<string>(branches.Count);
        for (int i = 0; i < branches.Count; i++)
        {
            ThresholdBranchEntry entry = branches[i];
            if (entry == null) continue;
            parts.Add(FormatThreshold(entry.threshold));
        }

        return parts.Count == 0 ? $"{varLabel}  >" : $"{varLabel}  > {string.Join(":", parts)}";
    }

    public static string NextDefaultName(List<ThresholdBranchEntry> list)
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

    public static float NextDefaultThreshold(List<ThresholdBranchEntry> list)
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

        return any ? max + 10f : 0f;
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
