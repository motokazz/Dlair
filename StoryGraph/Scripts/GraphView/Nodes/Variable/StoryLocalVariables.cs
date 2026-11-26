using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 実行中の StoryGraph に属するローカル変数。
/// 宣言したグラフの外では見えず、セーブしない。同名のグローバル変数より優先する。
/// </summary>
public static class StoryLocalVariables
{
    class Slot
    {
        public StoryVariableType type;
        public int intValue;
        public bool boolValue;
        public float floatValue;
        public string stringValue = "";
        public VideoClip videoClip;
        public AudioClip audioClip;
    }

    static int globalWrite;
    static StoryGraph active;
    static readonly Dictionary<StoryGraph, Dictionary<string, VariableDeclaration>> declared =
        new Dictionary<StoryGraph, Dictionary<string, VariableDeclaration>>();
    static readonly Dictionary<StoryGraph, Dictionary<string, Slot>> values =
        new Dictionary<StoryGraph, Dictionary<string, Slot>>();
    static readonly Dictionary<StoryGraph, HashSet<string>> callerHold =
        new Dictionary<StoryGraph, HashSet<string>>();

    /// <summary>
    /// 実行中のグラフを切り替える。そのグラフの未作成ローカル変数には初期値を入れる。
    /// </summary>
    public static void Activate(StoryGraph graph)
    {
        active = graph;
        if (graph == null) return;

        Dictionary<string, VariableDeclaration> definitions = Definitions(graph);
        Dictionary<string, Slot> table = Table(graph);
        foreach (KeyValuePair<string, VariableDeclaration> pair in definitions)
        {
            if (table.ContainsKey(pair.Key)) continue;
            table[pair.Key] = CreateSlot(pair.Value);
        }
    }

    public static void Clear()
    {
        active = null;
        declared.Clear();
        values.Clear();
        callerHold.Clear();
    }

    /// <summary>
    /// グローバル変数への読み書き中は、同名のローカル変数を使わない。
    /// </summary>
    public static void BeginGlobalWrite()
    {
        globalWrite++;
    }

    public static void EndGlobalWrite()
    {
        if (globalWrite > 0) globalWrite--;
    }

    public static bool Owns(string key)
    {
        if (globalWrite > 0 || active == null || string.IsNullOrEmpty(key)) return false;
        return Definitions(active).ContainsKey(key.Trim());
    }

    public static bool IsType(string key, StoryVariableType type)
    {
        return TryGetSlot(key, out Slot slot) && slot.type == type;
    }

    /// <summary>
    /// 指定グラフのローカル変数へ値を書く。そのグラフのローカル宣言に無い名前は false。
    /// 実行中でなくても書ける。すでにある値は上書きする。
    /// ここで書いた値は、次にローカル宣言ノードが宣言値で上書きするとき、1回だけ残す。
    /// </summary>
    public static bool TrySetDeclared(StoryGraph graph, string key, StoryVariableType type, int intValue, bool boolValue, float floatValue, string stringValue, VideoClip videoClip, AudioClip audioClip)
    {
        if (graph == null || string.IsNullOrWhiteSpace(key)) return false;

        string trimmed = key.Trim();
        if (!Definitions(graph).TryGetValue(trimmed, out VariableDeclaration declaration)) return false;
        if (declaration.type != type) return false;

        Table(graph)[trimmed] = new Slot
        {
            type = type,
            intValue = intValue,
            boolValue = boolValue,
            floatValue = floatValue,
            stringValue = stringValue ?? "",
            videoClip = videoClip,
            audioClip = audioClip
        };
        KeepCallerValue(graph, trimmed);
        if (active == graph) GameManager.NotifyChanged(trimmed);
        return true;
    }

    /// <summary>
    /// このノードの宣言を、実行中グラフのローカル変数へ書く。
    /// overwrite が false のときは、まだ無い名前だけ入れる。
    /// overwrite が true でも、SubGraph Input が直前に書いた値は残し、その保護はここで外す。
    /// </summary>
    public static void Write(LocalVariableDeclareNode node, bool overwrite)
    {
        if (node == null || active == null || node.variables == null) return;

        Dictionary<string, Slot> table = Table(active);
        for (int i = 0; i < node.variables.Count; i++)
        {
            VariableDeclaration entry = node.variables[i];
            if (entry == null || entry.type == StoryVariableType.Heading || string.IsNullOrWhiteSpace(entry.key)) continue;

            string key = entry.key.Trim();
            if (!overwrite && table.ContainsKey(key)) continue;
            if (!Definitions(active).ContainsKey(key)) continue;
            if (overwrite && TakeCallerValue(active, key)) continue;

            table[key] = CreateSlot(entry);
            GameManager.NotifyChanged(key);
        }
    }

    public static bool TryGetInt(string key, out int value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = 0;
            return false;
        }

        switch (slot.type)
        {
            case StoryVariableType.Float:
                value = Truncate(slot.floatValue);
                break;
            case StoryVariableType.Bool:
                value = slot.boolValue ? 1 : 0;
                break;
            case StoryVariableType.Int:
                value = slot.intValue;
                break;
            default:
                value = 0;
                break;
        }

        return true;
    }

    public static bool TryGetFloat(string key, out float value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = 0f;
            return false;
        }

        switch (slot.type)
        {
            case StoryVariableType.Float:
                value = slot.floatValue;
                break;
            case StoryVariableType.Bool:
                value = slot.boolValue ? 1f : 0f;
                break;
            case StoryVariableType.Int:
                value = slot.intValue;
                break;
            default:
                value = 0f;
                break;
        }

        return true;
    }

    public static bool TryGetBool(string key, out bool value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = false;
            return false;
        }

        value = slot.type == StoryVariableType.Bool && slot.boolValue;
        return true;
    }

    public static bool TryGetString(string key, out string value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = "";
            return false;
        }

        value = slot.type == StoryVariableType.String ? (slot.stringValue ?? "") : "";
        return true;
    }

    public static bool TryGetVideo(string key, out VideoClip value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = null;
            return false;
        }

        value = slot.type == StoryVariableType.VideoClip ? slot.videoClip : null;
        return true;
    }

    public static bool TryGetAudio(string key, out AudioClip value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = null;
            return false;
        }

        value = slot.type == StoryVariableType.AudioClip ? slot.audioClip : null;
        return true;
    }

    public static bool TryGetNumeric(string key, out float value)
    {
        if (!TryGetSlot(key, out Slot slot))
        {
            value = 0f;
            return false;
        }

        switch (slot.type)
        {
            case StoryVariableType.Float:
            case StoryVariableType.Int:
            case StoryVariableType.Bool:
                return TryGetFloat(key, out value);
            default:
                value = 0f;
                return false;
        }
    }

    public static bool TryGetDisplayText(string key, out string text)
    {
        text = "";
        if (!TryGetSlot(key, out Slot slot)) return false;

        switch (slot.type)
        {
            case StoryVariableType.String:
                text = slot.stringValue ?? "";
                return true;
            case StoryVariableType.Bool:
                text = slot.boolValue ? "On" : "Off";
                return true;
            case StoryVariableType.Float:
                text = slot.floatValue.ToString("0.########", CultureInfo.InvariantCulture);
                return true;
            case StoryVariableType.Int:
                text = slot.intValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case StoryVariableType.VideoClip:
                text = slot.videoClip != null ? slot.videoClip.name : "";
                return true;
            case StoryVariableType.AudioClip:
                text = slot.audioClip != null ? slot.audioClip.name : "";
                return true;
            default:
                return false;
        }
    }

    public static bool TrySetInt(string key, int value)
    {
        if (!TryGetSlot(key, out Slot slot)) return false;

        if (slot.type == StoryVariableType.Float) slot.floatValue = value;
        else if (slot.type == StoryVariableType.Int) slot.intValue = value;
        return true;
    }

    public static bool TrySetFloat(string key, float value)
    {
        if (!TryGetSlot(key, out Slot slot)) return false;

        if (slot.type == StoryVariableType.Float) slot.floatValue = value;
        else if (slot.type == StoryVariableType.Int) slot.intValue = Truncate(value);
        return true;
    }

    public static bool TrySetBool(string key, bool value)
    {
        if (!TryGetSlot(key, out Slot slot) || slot.type != StoryVariableType.Bool) return Owns(key);
        slot.boolValue = value;
        return true;
    }

    public static bool TrySetString(string key, string value)
    {
        if (!TryGetSlot(key, out Slot slot) || slot.type != StoryVariableType.String) return Owns(key);
        slot.stringValue = value ?? "";
        return true;
    }

    public static bool TrySetVideo(string key, VideoClip value)
    {
        if (!TryGetSlot(key, out Slot slot) || slot.type != StoryVariableType.VideoClip) return Owns(key);
        slot.videoClip = value;
        return true;
    }

    public static bool TrySetAudio(string key, AudioClip value)
    {
        if (!TryGetSlot(key, out Slot slot) || slot.type != StoryVariableType.AudioClip) return Owns(key);
        slot.audioClip = value;
        return true;
    }

    static Dictionary<string, VariableDeclaration> Definitions(StoryGraph graph)
    {
        if (declared.TryGetValue(graph, out Dictionary<string, VariableDeclaration> map)) return map;

        map = new Dictionary<string, VariableDeclaration>();
        if (graph.nodes != null)
        {
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] is not LocalVariableDeclareNode node || node.variables == null) continue;

                for (int v = 0; v < node.variables.Count; v++)
                {
                    VariableDeclaration entry = node.variables[v];
                    if (entry == null || entry.type == StoryVariableType.Heading || string.IsNullOrWhiteSpace(entry.key)) continue;

                    string key = entry.key.Trim();
                    if (!map.ContainsKey(key)) map.Add(key, entry);
                }
            }
        }

        declared[graph] = map;
        return map;
    }

    static void KeepCallerValue(StoryGraph graph, string key)
    {
        if (!callerHold.TryGetValue(graph, out HashSet<string> keys))
        {
            keys = new HashSet<string>();
            callerHold[graph] = keys;
        }

        keys.Add(key);
    }

    static bool TakeCallerValue(StoryGraph graph, string key)
    {
        if (graph == null || !callerHold.TryGetValue(graph, out HashSet<string> keys)) return false;
        return keys.Remove(key);
    }

    static Dictionary<string, Slot> Table(StoryGraph graph)
    {
        if (!values.TryGetValue(graph, out Dictionary<string, Slot> table))
        {
            table = new Dictionary<string, Slot>();
            values[graph] = table;
        }

        return table;
    }

    static bool TryGetSlot(string key, out Slot slot)
    {
        slot = null;
        if (globalWrite > 0 || active == null || string.IsNullOrEmpty(key)) return false;

        string trimmed = key.Trim();
        Dictionary<string, Slot> table = Table(active);
        if (table.TryGetValue(trimmed, out slot)) return true;
        if (!Definitions(active).TryGetValue(trimmed, out VariableDeclaration entry)) return false;

        slot = CreateSlot(entry);
        table[trimmed] = slot;
        return true;
    }

    static Slot CreateSlot(VariableDeclaration entry)
    {
        return new Slot
        {
            type = entry.type,
            intValue = entry.defaultIntValue,
            boolValue = entry.defaultBoolValue,
            floatValue = entry.defaultFloatValue,
            stringValue = entry.defaultStringValue ?? "",
            videoClip = entry.defaultVideoClip,
            audioClip = entry.defaultAudioClip
        };
    }

    static int Truncate(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)value;
    }
}
