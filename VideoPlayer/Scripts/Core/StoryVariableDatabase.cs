using System;
using System.Collections.Generic;
using UnityEngine;

public enum StoryVariableType
{
    Int,
    Bool,
    Float,
    String
}

[Serializable]
public class StoryVariableDefinition
{
    public string key = "NewVariable";
    public string description = "";
    public StoryVariableType type = StoryVariableType.Int;

    public int defaultIntValue = 0;
    public bool defaultBoolValue = false;
    public float defaultFloatValue = 0f;
    public string defaultStringValue = "";
}

[CreateAssetMenu(fileName = "StoryVariableDatabase", menuName = "Story/Variable Database")]
public class StoryVariableDatabase : ScriptableObject
{
    public List<StoryVariableDefinition> variables = new List<StoryVariableDefinition>();

    public StoryVariableDefinition GetDefinition(string key)
    {
        return variables.Find(v => v.key == key);
    }

    public List<string> GetAllKeys()
    {
        List<string> keys = new List<string>();
        foreach (var v in variables)
        {
            if (!string.IsNullOrEmpty(v.key) && !keys.Contains(v.key))
            {
                keys.Add(v.key);
            }
        }
        return keys;
    }
}