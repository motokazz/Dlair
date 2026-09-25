using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RandomBranchEntry
{
    public string name = "A";
    public float weight = 1f;
}

public class RandomBranchNode : BaseNode
{
    public List<RandomBranchEntry> branches = new List<RandomBranchEntry>
    {
        new RandomBranchEntry { name = "A", weight = 1f },
        new RandomBranchEntry { name = "B", weight = 1f }
    };

    public override void Execute(StoryPlayer player)
    {
        RandomBranchEntry selected = PickBranch();
        if (selected == null || string.IsNullOrEmpty(selected.name))
        {
            Debug.LogWarning("【Random Branch】分岐先がありません。");
            return;
        }

        Debug.Log($"【Random Branch】'{selected.name}' を選択 (weight: {selected.weight})");
        player.ContinueTo(this, selected.name);
    }

    public RandomBranchEntry PickBranch()
    {
        if (branches == null || branches.Count == 0) return null;

        float total = GetTotalWeight();
        if (total <= 0f)
        {
            return branches[UnityEngine.Random.Range(0, branches.Count)];
        }

        float roll = UnityEngine.Random.Range(0f, total);
        float accum = 0f;
        RandomBranchEntry lastValid = null;

        for (int i = 0; i < branches.Count; i++)
        {
            RandomBranchEntry entry = branches[i];
            if (entry == null || entry.weight <= 0f) continue;

            lastValid = entry;
            accum += entry.weight;
            if (roll < accum) return entry;
        }

        return lastValid;
    }

    public float GetTotalWeight()
    {
        float total = 0f;
        if (branches == null) return 0f;

        for (int i = 0; i < branches.Count; i++)
        {
            RandomBranchEntry entry = branches[i];
            if (entry != null && entry.weight > 0f)
            {
                total += entry.weight;
            }
        }

        return total;
    }

    public string GetDisplayTitle()
    {
        if (branches == null || branches.Count == 0) return "RANDOM";

        List<string> parts = new List<string>(branches.Count);
        for (int i = 0; i < branches.Count; i++)
        {
            RandomBranchEntry entry = branches[i];
            if (entry == null) continue;
            parts.Add(FormatWeight(Mathf.Max(0f, entry.weight)));
        }

        return parts.Count == 0 ? "RANDOM" : "RANDOM  " + string.Join(":", parts);
    }

    public static string NextDefaultName(List<RandomBranchEntry> list)
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

    private static string FormatWeight(float weight)
    {
        if (Mathf.Approximately(weight, Mathf.Round(weight)))
        {
            return Mathf.RoundToInt(weight).ToString();
        }

        return weight.ToString("0.##");
    }
}
