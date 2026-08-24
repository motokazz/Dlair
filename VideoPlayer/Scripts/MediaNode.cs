using UnityEngine;
using UnityEngine.Video;
using XNode;
using System.Collections.Generic;

// ==========================================
// ★修正：using は必ず一番上に書く！
// ==========================================
#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
#endif

public class MiniGameSelectorAttribute : PropertyAttribute { }

[CreateNodeMenu("Media/Video Node")]
public class MediaNode : Node
{
    [System.Serializable] public struct Flow { }

    [Input(ShowBackingValue.Never, ConnectionType.Multiple)]
    public Flow enter;

    [Output(ShowBackingValue.Never, ConnectionType.Override)]
    public Flow next;

    public string title = "New Media";
    public Sprite thumbnail;
    public bool isStaticImage;
    public VideoClip clip;
    public Texture2D image;
    public float imageDuration = 5.0f;
    public AudioClip audioClip;
    public bool isLooping = true;

    [Header("Crossfade")]
    public bool overrideCrossfade = false;
    public float customCrossfadeDuration = 1.0f;

    [Header("Interactive Event")]
    [MiniGameSelector]
    public string eventId = "None";

    public float eventTriggerTime = 0f;
    public string eventParameter;

    [System.Serializable]
    public class BranchChoice
    {
        public string branchKey = "Button Text";
    }

    [Output(dynamicPortList = true)]
    public List<BranchChoice> choices = new List<BranchChoice>();

    public override object GetValue(NodePort port)
    {
        return null;
    }

    public MediaNode GetNextNode()
    {
        NodePort port = GetOutputPort("next");
        if (port != null && port.IsConnected) return port.Connection.node as MediaNode;
        return null;
    }

    public MediaNode GetBranchTarget(int index)
    {
        NodePort port = GetOutputPort("choices " + index);
        if (port != null && port.IsConnected) return port.Connection.node as MediaNode;
        return null;
    }
}

// ==========================================
// エディタ拡張部分（usingは一番上に移動済み）
// ==========================================
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(MiniGameSelectorAttribute))]
public class MiniGameSelectorDrawer : PropertyDrawer
{
    private static string[] eventOptions;

    private static void CacheEventOptions()
    {
        if (eventOptions != null) return;
        var interfaceType = typeof(IMiniGame);

        var miniGameTypes = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => interfaceType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract)
            .Select(t => t.Name)
            .ToList();

        var list = new List<string>();
        list.Add("None");
        list.AddRange(miniGameTypes);
        eventOptions = list.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        CacheEventOptions();

        int selectedIndex = System.Array.IndexOf(eventOptions, property.stringValue);
        if (selectedIndex < 0) selectedIndex = 0;

        selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, eventOptions);
        property.stringValue = eventOptions[selectedIndex];
    }
}
#endif