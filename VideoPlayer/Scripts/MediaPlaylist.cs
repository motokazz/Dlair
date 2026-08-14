using UnityEngine;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "NewMediaPlaylist", menuName = "VideoPlayer/Media Playlist")]
public class MediaPlaylist : ScriptableObject
{
    [System.Serializable]
    public class BranchChoice
    {
        public string buttonText = "選択肢のテキスト";
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

        [Tooltip("このメディアを通常再生時にループさせるかどうか")]
        public bool isLooping = true;

        public bool isUnlockedByDefault = true;
        public string unlockId;

        [Header("Interactive Branching")]
        public BranchChoice[] choices;

        // ★追加：インタラクティブ待機時専用のループ設定
        [Tooltip("分岐待ちの間、この動画をループさせ続けるか（チェックを外すと最後のフレームで静止して待ちます）")]
        public bool loopWhileWaiting = true;
    }

    [Header("Playlist Items")]
    public MediaData[] items;
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(MediaPlaylist.MediaData))]
public class MediaDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty titleProp = property.FindPropertyRelative("title");
        string displayName = string.IsNullOrEmpty(titleProp.stringValue) ? label.text : titleProp.stringValue;

        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            displayName,
            true
        );

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            var isStaticImageProp = property.FindPropertyRelative("isStaticImage");

            float y = position.y + EditorGUIUtility.singleLineHeight + 2;
            float height = EditorGUIUtility.singleLineHeight;

            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), titleProp);
            y += height + 2;

            var thumbnailProp = property.FindPropertyRelative("thumbnail");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), thumbnailProp);
            y += height + 2;

            y += 4;

            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), isStaticImageProp);
            y += height + 2;

            if (isStaticImageProp.boolValue)
            {
                var imageProp = property.FindPropertyRelative("image");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), imageProp, new GUIContent("Image (Texture)"));
                y += height + 2;

                var imageDurationProp = property.FindPropertyRelative("imageDuration");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), imageDurationProp, new GUIContent("Duration (Seconds)"));
                y += height + 2;
            }
            else
            {
                var clipProp = property.FindPropertyRelative("clip");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), clipProp, new GUIContent("Video Clip"));
                y += height + 2;
            }

            var audioClipProp = property.FindPropertyRelative("audioClip");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), audioClipProp, new GUIContent("Audio (Optional)"));
            y += height + 2;

            if (!isStaticImageProp.boolValue || audioClipProp.objectReferenceValue != null)
            {
                var isLoopingProp = property.FindPropertyRelative("isLooping");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), isLoopingProp, new GUIContent("Loop Playback"));
                y += height + 2;
            }

            y += 4;

            var isUnlockedProp = property.FindPropertyRelative("isUnlockedByDefault");
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), isUnlockedProp, new GUIContent("Unlocked by Default"));
            y += height + 2;

            if (!isUnlockedProp.boolValue)
            {
                var unlockIdProp = property.FindPropertyRelative("unlockId");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), unlockIdProp, new GUIContent("Unlock ID (for Save)"));
                y += height + 2;
            }

            y += 4;

            var choicesProp = property.FindPropertyRelative("choices");
            float choicesHeight = EditorGUI.GetPropertyHeight(choicesProp, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, choicesHeight), choicesProp, true);
            y += choicesHeight + 2;

            // ★追加：選択肢が設定されている時だけ「待機時のループ設定」を表示する
            if (choicesProp.arraySize > 0)
            {
                var loopWhileProp = property.FindPropertyRelative("loopWhileWaiting");
                EditorGUI.PropertyField(new Rect(position.x, y, position.width, height), loopWhileProp, new GUIContent("Loop While Waiting"));
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

        var isStaticImageProp = property.FindPropertyRelative("isStaticImage");
        var audioClipProp = property.FindPropertyRelative("audioClip");
        var isUnlockedProp = property.FindPropertyRelative("isUnlockedByDefault");
        var choicesProp = property.FindPropertyRelative("choices");

        int lines = 6;
        if (isStaticImageProp.boolValue) lines += 2;
        else lines += 1;
        if (!isStaticImageProp.boolValue || audioClipProp.objectReferenceValue != null) lines += 1;
        lines += 1;
        if (!isUnlockedProp.boolValue) lines += 1;

        // ★選択肢がある場合は1行増やす
        if (choicesProp.arraySize > 0) lines += 1;

        float choicesHeight = EditorGUI.GetPropertyHeight(choicesProp, true);
        return (EditorGUIUtility.singleLineHeight + 2) * lines + 12 + choicesHeight;
    }
}
#endif