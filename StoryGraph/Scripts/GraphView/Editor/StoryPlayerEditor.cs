using UnityEditor;
using UnityEngine;

/// <summary>
/// StoryPlayer の追加 UI 親を UIParent1 から表示する。
/// </summary>
[CustomEditor(typeof(StoryPlayer))]
public class StoryPlayerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty property = serializedObject.GetIterator();
        if (!property.NextVisible(true))
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(property, true);
        }

        while (property.NextVisible(false))
        {
            if (property.name == "uiParents")
                DrawUiParents(property);
            else
                EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawUiParents(SerializedProperty parents)
    {
        parents.isExpanded = EditorGUILayout.Foldout(
            parents.isExpanded,
            new GUIContent("UI Parents", "要素0が UIParent1。数は増減できる"),
            true);
        if (!parents.isExpanded) return;

        EditorGUI.indentLevel++;
        int size = Mathf.Max(0, EditorGUILayout.IntField("Size", parents.arraySize));
        parents.arraySize = size;
        for (int i = 0; i < parents.arraySize; i++)
        {
            EditorGUILayout.PropertyField(
                parents.GetArrayElementAtIndex(i),
                new GUIContent("UIParent" + (i + 1)));
        }

        EditorGUI.indentLevel--;
    }
}
