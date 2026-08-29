using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Video;

public class StoryFlowNodeUI : Node
{
    public string guid;
    public BaseFlowNode data;

    public StoryFlowNodeUI(BaseFlowNode nodeData)
    {
        this.guid = nodeData.guid;
        this.data = nodeData;

        // タイトルバーの名前を設定
        this.title = nodeData.GetType().Name.Replace("FlowNode", "");
        this.SetPosition(new Rect(nodeData.position, Vector2.zero));

        // 左側の丸ポッチ（入力）は共通
        Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Enter";
        inputContainer.Add(inputPort);

        // ==========================================
        // ★変更：ノードの種類によって「出力ポート」の数を変える
        // ==========================================
        if (nodeData is ConditionFlowNode)
        {
            // 条件分岐ノードの場合：True と False の2つの出力を作る
            Port truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            truePort.portName = "True";
            outputContainer.Add(truePort);

            Port falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            falsePort.portName = "False";
            outputContainer.Add(falsePort);
        }
        else
        {
            // 通常のノードの場合：Next の1つだけ作る
            Port outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            outputPort.portName = "Next";
            outputContainer.Add(outputPort);
        }

        // ノードの種類によって入力欄を自動生成する
        DrawNodeSettings();

        RefreshExpandedState();
        RefreshPorts();
    }

    private void DrawNodeSettings()
    {
        // もしデータが PlayClipFlowNode だったら…
        if (data is PlayClipFlowNode playClipNode)
        {
            ObjectField clipField = new ObjectField("Video Clip")
            {
                objectType = typeof(VideoClip),
                value = playClipNode.clip
            };

            clipField.RegisterValueChangedCallback(evt =>
            {
                playClipNode.clip = evt.newValue as VideoClip;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(clipField);
        }
        // ==========================================
        // ★追加：もしデータが ConditionFlowNode だったら…
        // ==========================================
        else if (data is ConditionFlowNode conditionNode)
        {
            // ※ conditionNode内に定義されている変数（フラグ名など）を入力する枠を作ります。
            // 以下は string 型の条件変数（例：conditionKey）がある場合の例です。
            // ご自身の ConditionFlowNode.cs の中身に合わせて書き換えてください。
            /*
            TextField conditionField = new TextField("Condition Key")
            {
                value = conditionNode.conditionKey
            };
            conditionField.RegisterValueChangedCallback(evt =>
            {
                conditionNode.conditionKey = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(conditionNode);
            });
            extensionContainer.Add(conditionField);
            */

            // UIを拡張したことを明示するラベル（動作確認用）
            Label label = new Label("Condition Settings");
            extensionContainer.Add(label);
        }
    }
}