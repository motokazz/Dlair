using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using System.Collections.Generic;
using UnityEngine;
using System;

public class StoryFlowGraphView : GraphView
{
    private StoryFlowWindow window;

    public StoryFlowGraphView(StoryFlowWindow window)
    {
        this.window = window;
        Insert(0, new GridBackground());
        this.AddManipulator(new ContentZoomer());
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        this.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

        // 右クリックメニューの登録
        this.AddManipulator(new ContextualMenuManipulator(menuEvent =>
        {
            // ① Start Node
            menuEvent.menu.AppendAction("Create/Start Node", actionEvent =>
            {
                Vector2 localMousePos = actionEvent.eventInfo.localMousePosition;
                Vector2 graphPos = contentViewContainer.WorldToLocal(localMousePos);
                CreateNodeUI<StartFlowNode>(graphPos);
            });

            // ==========================================
            // ★追加：② Play Clip Node
            // ==========================================
            menuEvent.menu.AppendAction("Create/Play Clip Node", actionEvent =>
            {
                Vector2 localMousePos = actionEvent.eventInfo.localMousePosition;
                Vector2 graphPos = contentViewContainer.WorldToLocal(localMousePos);
                CreateNodeUI<PlayClipFlowNode>(graphPos);
            });

            // ==========================================
            // ★追加：③ Condition Node (条件分岐ノード)
            // ==========================================
            menuEvent.menu.AppendAction("Create/Condition Node", actionEvent =>
            {
                Vector2 localMousePos = actionEvent.eventInfo.localMousePosition;
                Vector2 graphPos = contentViewContainer.WorldToLocal(localMousePos);

                // ジェネリック型に ConditionFlowNode を指定してノードを生成
                CreateNodeUI<ConditionFlowNode>(graphPos);
            });
        }));
    }

    public void CreateNodeUI<T>(Vector2 position) where T : BaseFlowNode
    {
        T nodeData = ScriptableObject.CreateInstance<T>();
        nodeData.guid = Guid.NewGuid().ToString();
        nodeData.position = position;

        StoryFlowNodeUI nodeUI = new StoryFlowNodeUI(nodeData);
        AddElement(nodeUI);
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        List<Port> compatiblePorts = new List<Port>();
        ports.ForEach(port =>
        {
            if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
            {
                compatiblePorts.Add(port);
            }
        });
        return compatiblePorts;
    }
}