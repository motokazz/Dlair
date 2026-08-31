using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(StoryGotoPanel))]
public class StoryGotoPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        StoryGotoPanel panel = (StoryGotoPanel)target;
        EditorGUILayout.Space();
        if (!GUILayout.Button("子ボタンからエントリを作成")) return;

        Undo.RecordObject(panel, "Fill Goto Entries");
        FillEntriesFromChildren(panel);
        EditorUtility.SetDirty(panel);
    }

    private static void FillEntriesFromChildren(StoryGotoPanel panel)
    {
        if (panel.entries == null)
        {
            panel.entries = new List<StoryGotoPanel.Entry>();
        }

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            bool exists = false;
            for (int e = 0; e < panel.entries.Count; e++)
            {
                StoryGotoPanel.Entry entry = panel.entries[e];
                if (entry == null) continue;
                if (entry.button == button) { exists = true; break; }
                if (entry.button == null && entry.labelName == button.gameObject.name) { exists = true; break; }
            }

            if (exists) continue;

            panel.entries.Add(new StoryGotoPanel.Entry
            {
                button = button,
                labelName = button.gameObject.name,
                displayState = panel.defaultDisplayState
            });
        }
    }
}
