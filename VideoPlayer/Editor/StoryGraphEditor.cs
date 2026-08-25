using UnityEngine;
using UnityEditor;
using XNodeEditor;
using UnityEngine.Video;

// StoryGraph 専用のエディタ拡張
[CustomNodeGraphEditor(typeof(StoryGraph))]
public class StoryGraphEditor : NodeGraphEditor
{
    public override void OnDropObjects(Object[] objects)
    {
        Vector2 pos = NodeEditorWindow.current.WindowToGridPosition(Event.current.mousePosition);
        bool created = false;

        foreach (Object obj in objects)
        {
            // ① 動画（VideoClip）の場合
            if (obj is VideoClip videoClip)
            {
                // ★大修正：xNodeの正式な「CreateNode」メソッドを使用（Undoや自動セーブに対応）
                PlayClipNode node = CreateNode(typeof(PlayClipNode), pos) as PlayClipNode;
                node.clip = videoClip;
                node.title = videoClip.name;

                // ★重要：パラメーターを変更したのでUnityに「変更あり(Dirty)」と教える
                EditorUtility.SetDirty(node);
                created = true;
            }
            // ② 画像（Texture2D）の場合
            else if (obj is Texture2D texture)
            {
                PlayImageNode node = CreateNode(typeof(PlayImageNode), pos) as PlayImageNode;
                node.image = texture;
                node.title = texture.name;

                EditorUtility.SetDirty(node);
                created = true;
            }
            // ③ 画像（Sprite）の場合
            else if (obj is Sprite sprite)
            {
                PlayImageNode node = CreateNode(typeof(PlayImageNode), pos) as PlayImageNode;
                node.image = sprite.texture;
                node.title = sprite.name;

                EditorUtility.SetDirty(node);
                created = true;
            }

            if (created)
            {
                pos.x += 50;
                pos.y += 50;
            }
        }

        if (created)
        {
            // ★重要：グラフ自体が変更されたこともUnityに教え、強制的にディスクに保存する
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            NodeEditorWindow.current.Repaint();
        }
    }
}