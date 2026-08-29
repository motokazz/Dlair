using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEditor.Experimental.GraphView.Port;

// ★ Unity標準の Node クラスを直接継承します
public class ConditionNodeView : Node
{
    public ConditionFlowNode conditionNode;

    public ConditionNodeView(ConditionFlowNode node)
    {
        this.conditionNode = node;
        this.title = "Condition (条件分岐)";

        // 位置の復元
        SetPosition(new Rect(node.position, Vector2.zero));

        // 1. 入力ポート (Enter)
        Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Capacity.Multi, typeof(bool));
        inputPort.portName = "Enter";
        inputContainer.Add(inputPort);

        // 2. 出力ポート (True / False の2つ)
        Port truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Capacity.Single, typeof(bool));
        truePort.portName = "True";
        outputContainer.Add(truePort);

        Port falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Capacity.Single, typeof(bool));
        falsePort.portName = "False";
        outputContainer.Add(falsePort);

        // 3. 変数名の入力フィールド
        TextField varNameField = new TextField("変数名");
        varNameField.value = conditionNode.variableName;
        varNameField.RegisterValueChangedCallback(evt => {
            conditionNode.variableName = evt.newValue;
        });
        extensionContainer.Add(varNameField);

        // 4. 比較数値の入力フィールド
#if UNITY_2022_1_OR_NEWER
        IntegerField valField = new IntegerField("より大きい(>)");
#else
        UnityEditor.UIElements.IntegerField valField = new UnityEditor.UIElements.IntegerField("より大きい(>)");
#endif
        valField.value = conditionNode.compareValue;
        valField.RegisterValueChangedCallback(evt => {
            conditionNode.compareValue = evt.newValue;
        });
        extensionContainer.Add(valField);

        RefreshExpandedState();
        RefreshPorts();
    }
}