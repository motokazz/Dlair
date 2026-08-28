using UnityEngine;
using UnityEditor;
using XNodeEditor;
using UnityEngine.Video;

// StoryGraph 専用のエディタ拡張
[CustomNodeGraphEditor(typeof(StoryGraph))]
public class StoryGraphEditor : NodeGraphEditor
{
    // ==========================================
    // ドラッグ＆ドロップでノードを自動作成する処理
    // ==========================================
    public override void OnDropObjects(Object[] objects)
    {
        Vector2 pos = NodeEditorWindow.current.WindowToGridPosition(Event.current.mousePosition);
        bool created = false;

        foreach (Object obj in objects)
        {
            if (obj is VideoClip videoClip)
            {
                PlayClipNode node = CreateNode(typeof(PlayClipNode), pos) as PlayClipNode;
                node.clip = videoClip;
                node.title = videoClip.name;
                EditorUtility.SetDirty(node);
                created = true;
            }
            else if (obj is Texture2D texture)
            {
                PlayImageNode node = CreateNode(typeof(PlayImageNode), pos) as PlayImageNode;
                node.image = texture;
                node.title = texture.name;
                EditorUtility.SetDirty(node);
                created = true;
            }
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
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            NodeEditorWindow.current.Repaint();
        }
    }

    // ==========================================
    // ★大進化：開いた瞬間に自動整頓＆ワープ
    // ==========================================
    public override void OnOpen()
    {
        base.OnOpen();

        StartNode startNode = null;
        foreach (var node in target.nodes)
        {
            if (node is StartNode sn)
            {
                startNode = sn;
                break;
            }
        }

        if (startNode != null)
        {
            // ★変更：Start Nodeが (0,0) から 1ピクセルでもズレていたら、自動的に直す！
            if (startNode.position.sqrMagnitude > 1f)
            {
                ResetCoordinatesToOrigin((StoryGraph)target, startNode);
                Debug.Log($"【自動整頓】{target.name} の Start Node が (0,0) からズレていたため、全ノードを自動補正しました。");
            }

            // 開いた瞬間にカメラを Start Node（整頓後は必ず 0,0） に自動ワープさせる
            EditorApplication.delayCall += () =>
            {
                if (NodeEditorWindow.current != null && NodeEditorWindow.current.graph == target)
                {
                    NodeEditorWindow.current.panOffset = -startNode.position;
                    NodeEditorWindow.current.zoom = 1f;
                    NodeEditorWindow.current.Repaint();
                }
            };
        }
    }

    // ==========================================
    // ★追加：画面左上の便利ボタンUI
    // ==========================================
    public override void OnGUI()
    {
        base.OnGUI();

        // 2つのボタンを置くため、縦幅を少し広くします（40 -> 80）
        GUILayout.BeginArea(new Rect(10, 10, 180, 80));

        // ① Start Node に戻るボタン
        if (GUILayout.Button("📍 Start Node に戻る", GUILayout.Height(30)))
        {
            foreach (var node in target.nodes)
            {
                if (node is StartNode)
                {
                    NodeEditorWindow.current.panOffset = -node.position;
                    NodeEditorWindow.current.zoom = 1f;
                    break;
                }
            }
        }

        GUILayout.Space(5);

        // ② 全ノードの座標を(0,0)基準にリセットするボタン
        if (GUILayout.Button("✨ 座標を(0,0)にリセット", GUILayout.Height(30)))
        {
            StartNode startNode = null;
            foreach (var node in target.nodes)
            {
                if (node is StartNode sn) { startNode = sn; break; }
            }

            if (startNode != null)
            {
                ResetCoordinatesToOrigin((StoryGraph)target, startNode);

                // 修正後、カメラも即座に(0,0)へワープさせる
                NodeEditorWindow.current.panOffset = Vector2.zero;
                NodeEditorWindow.current.zoom = 1f;
                NodeEditorWindow.current.Repaint();
            }
            else
            {
                Debug.LogWarning("Start Node が見つかりません！");
            }
        }

        GUILayout.EndArea();
    }

    // ==========================================
    // 座標リセットの共通処理（ボタンからも自動処理からも呼ばれる）
    // ==========================================
    private void ResetCoordinatesToOrigin(StoryGraph graph, StartNode startNode)
    {
        Vector2 offset = -startNode.position;
        if (offset == Vector2.zero) return;

        foreach (var node in graph.nodes)
        {
            if (node != null)
            {
                // 元に戻せるようにUndo履歴を記録
                Undo.RecordObject(node, "Reset Graph Coordinates");

                node.position += offset;
                EditorUtility.SetDirty(node);
            }
        }
        EditorUtility.SetDirty(graph);
        AssetDatabase.SaveAssets();
    }
}