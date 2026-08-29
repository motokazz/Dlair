using UnityEngine;

public class ConditionFlowNode : BaseFlowNode
{
    public string variableName = "i"; // 判定する変数名
    public int compareValue = 0;      // 比較する数値

    public override void Execute(StoryFlowPlayer player)
    {
        Debug.Log($"【Condition Node】条件分岐を判定します: {variableName} > {compareValue} ?");

        // ★今はまだ変数管理システムがないので、テストとして無条件で「True」ルートに進むようにしておきます
        // （後ほど、実際の変数を読み込んで判定する仕組みを追加します）
        bool isTrue = true;

        // Trueなら "True" ポッチ、Falseなら "False" ポッチに繋がっているノードを探す
        string portName = isTrue ? "True" : "False";

        BaseFlowNode nextNode = player.flowGraph.GetNextNode(this.guid, portName);

        if (nextNode != null)
        {
            Debug.Log($"条件結果は {isTrue}！ {portName} のルートへ進みます。");
            nextNode.Execute(player);
        }
        else
        {
            Debug.LogWarning($"{portName} ルートの先にノードが繋がっていません！");
        }
    }
}