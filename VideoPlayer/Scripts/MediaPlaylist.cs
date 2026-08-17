using UnityEngine;
using UnityEngine.Video;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NewMediaPlaylist", menuName = "VideoPlayer/Media Playlist")]
public class MediaPlaylist : ScriptableObject
{
    [Header("Playlist Global Settings")]
    [Tooltip("このプレイリスト全体の基本クロスフェード時間（秒）")]
    public float defaultCrossfadeDuration = 1.0f;

    [System.Serializable]
    public class BranchChoice
    {
        public string branchKey = "分岐名 または キー";
        public string targetId;
    }

    [System.Serializable]
    public class MediaData
    {
        public string title;
        public Sprite thumbnail;
        public bool isStaticImage;
        public VideoClip clip;
        public Texture2D image;
        public float imageDuration = 5.0f;
        public AudioClip audioClip;
        public bool isLooping = true;
        public bool isUnlockedByDefault = true;
        public string unlockId;

        public bool overrideCrossfade = false;
        public float customCrossfadeDuration = 1.0f;

        public string eventId = "None";

        // ==========================================
        // ★追加：イベント発火のタイミング（秒）
        // ==========================================
        public float eventTriggerTime = 0f;

        public string eventParameter;
        public BranchChoice[] choices;
    }
    public MediaData[] items;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(MediaPlaylist.MediaData))]
public class MediaDataDrawer : PropertyDrawer
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

        var list = new System.Collections.Generic.List<string>();
        list.Add("None");
        list.AddRange(miniGameTypes); 
        eventOptions = list.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        SerializedProperty titleProp = property.FindPropertyRelative("title");
        string displayName = string.IsNullOrEmpty(titleProp.stringValue) ? "New Media" : titleProp.stringValue;
        Rect rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        
        property.isExpanded = EditorGUI.Foldout(rect, property.isExpanded, displayName, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.PropertyField(rect, titleProp);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            var thumbnailProp = property.FindPropertyRelative("thumbnail");
            EditorGUI.PropertyField(rect, thumbnailProp);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            rect.y += 8; 
            var isStaticImageProp = property.FindPropertyRelative("isStaticImage");
            EditorGUI.PropertyField(rect, isStaticImageProp);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            if (isStaticImageProp.boolValue)
            {
                var imageProp = property.FindPropertyRelative("image");
                EditorGUI.PropertyField(rect, imageProp, new GUIContent("Image (Texture)"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
                var imageDurationProp = property.FindPropertyRelative("imageDuration");
                EditorGUI.PropertyField(rect, imageDurationProp, new GUIContent("Duration (Seconds)"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
            }
            else
            {
                var clipProp = property.FindPropertyRelative("clip");
                EditorGUI.PropertyField(rect, clipProp, new GUIContent("Video Clip"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
            }
            rect.y += 8;
            var audioClipProp = property.FindPropertyRelative("audioClip");
            EditorGUI.PropertyField(rect, audioClipProp, new GUIContent("Audio (Optional)"));
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            if (!isStaticImageProp.boolValue || audioClipProp.objectReferenceValue != null)
            {
                var isLoopingProp = property.FindPropertyRelative("isLooping");
                EditorGUI.PropertyField(rect, isLoopingProp, new GUIContent("Loop Playback"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
            }
            rect.y += 8; 
            var isUnlockedProp = property.FindPropertyRelative("isUnlockedByDefault");
            EditorGUI.PropertyField(rect, isUnlockedProp, new GUIContent("Unlocked by Default"));
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            if (!isUnlockedProp.boolValue)
            {
                var unlockIdProp = property.FindPropertyRelative("unlockId");
                EditorGUI.PropertyField(rect, unlockIdProp, new GUIContent("Unlock ID (for Save)"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
            }

            rect.y += 8;
            var overrideFadeProp = property.FindPropertyRelative("overrideCrossfade");
            EditorGUI.PropertyField(rect, overrideFadeProp, new GUIContent("Override Crossfade"));
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            if (overrideFadeProp.boolValue)
            {
                var customFadeProp = property.FindPropertyRelative("customCrossfadeDuration");
                EditorGUI.PropertyField(rect, customFadeProp, new GUIContent("Fade Duration (Sec)"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;
            }
            
            rect.y += 8;
            EditorGUI.LabelField(rect, "Interactive Settings", EditorStyles.boldLabel);
            rect.y += EditorGUIUtility.singleLineHeight + 2;
            
            CacheEventOptions();
            var eventIdProp = property.FindPropertyRelative("eventId");
            
            int selectedIndex = System.Array.IndexOf(eventOptions, eventIdProp.stringValue);
            if (selectedIndex < 0) selectedIndex = 0; 
            
            selectedIndex = EditorGUI.Popup(rect, "Event Type", selectedIndex, eventOptions);
            eventIdProp.stringValue = eventOptions[selectedIndex]; 
            rect.y += EditorGUIUtility.singleLineHeight + 2;

            string selectedEvent = eventIdProp.stringValue;
            
            if (selectedEvent != "None")
            {
                // ==========================================
                // ★追加：イベント発火タイミングの入力欄を表示
                // ==========================================
                var triggerTimeProp = property.FindPropertyRelative("eventTriggerTime");
                EditorGUI.PropertyField(rect, triggerTimeProp, new GUIContent("Show Event After (Sec) [0=End]"));
                rect.y += EditorGUIUtility.singleLineHeight + 2;

                if (selectedEvent != "AutoBranchController")
                {
                    var paramProp = property.FindPropertyRelative("eventParameter");
                    string paramLabel = (selectedEvent == "ChoicesController") ? "Prompt Text (Message)" : "Game Parameter (Optional)";
                    EditorGUI.PropertyField(rect, paramProp, new GUIContent(paramLabel));
                    rect.y += EditorGUIUtility.singleLineHeight + 2;
                }

                string listLabel = "Event Branches (Keys)";
                if (selectedEvent == "ChoicesController") listLabel = "Choices (Buttons)";
                else if (selectedEvent == "AutoBranchController") listLabel = "Target Video (Key ignored)";

                var choicesProp = property.FindPropertyRelative("choices");
                float choicesHeight = EditorGUI.GetPropertyHeight(choicesProp, true);
                rect.height = choicesHeight;
                EditorGUI.PropertyField(rect, choicesProp, new GUIContent(listLabel), true);
                rect.y += choicesHeight + 2;
                rect.height = EditorGUIUtility.singleLineHeight;
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight; 
        float h = EditorGUIUtility.singleLineHeight + 2; 
        h += EditorGUIUtility.singleLineHeight + 2; 
        h += EditorGUIUtility.singleLineHeight + 2; 
        h += 8; 
        var isStaticImageProp = property.FindPropertyRelative("isStaticImage");
        h += EditorGUIUtility.singleLineHeight + 2; 
        if (isStaticImageProp.boolValue) { h += EditorGUIUtility.singleLineHeight + 2; h += EditorGUIUtility.singleLineHeight + 2; }
        else h += EditorGUIUtility.singleLineHeight + 2; 
        h += 8; 
        var audioClipProp = property.FindPropertyRelative("audioClip");
        h += EditorGUIUtility.singleLineHeight + 2; 
        if (!isStaticImageProp.boolValue || audioClipProp.objectReferenceValue != null) h += EditorGUIUtility.singleLineHeight + 2; 
        h += 8; 
        var isUnlockedProp = property.FindPropertyRelative("isUnlockedByDefault");
        h += EditorGUIUtility.singleLineHeight + 2; 
        if (!isUnlockedProp.boolValue) h += EditorGUIUtility.singleLineHeight + 2; 
        h += 8;
        var overrideFadeProp = property.FindPropertyRelative("overrideCrossfade");
        h += EditorGUIUtility.singleLineHeight + 2; 
        if (overrideFadeProp.boolValue) h += EditorGUIUtility.singleLineHeight + 2; 
        h += 8; 
        h += EditorGUIUtility.singleLineHeight + 2; 
        
        var eventIdProp = property.FindPropertyRelative("eventId");
        h += EditorGUIUtility.singleLineHeight + 2; 

        string selectedEvent = eventIdProp.stringValue;
        if (selectedEvent != "None")
        {
            h += EditorGUIUtility.singleLineHeight + 2; // ★追加：Trigger Timeの高さ分を足す

            if (selectedEvent != "AutoBranchController")
            {
                h += EditorGUIUtility.singleLineHeight + 2; 
            }
            var choicesProp = property.FindPropertyRelative("choices");
            h += EditorGUI.GetPropertyHeight(choicesProp, true) + 2; 
        }
        return h + 4; 
    }
}
#endif