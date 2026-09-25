using UnityEngine;

public enum ConditionOperator
{
    GreaterThan,      // > (より大きい)
    GreaterOrEqual,   // >= (以上)
    Equal,            // == (等しい)
    LessOrEqual,      // <= (以下)
    LessThan,         // < (より小さい)
    NotEqual          // != (等しくない)
}

public class ConditionNode : BaseNode
{
    public string variableName = "A";                                      // 判定する変数名
    public ConditionOperator comparison = ConditionOperator.GreaterThan;  // 比較演算子
    public int compareValue = 0;                                           // 比較する数値

    public override void Execute(StoryPlayer player)
    {
        int currentValue = GameManager.HasBool(variableName)
            ? (GameManager.GetBool(variableName) ? 1 : 0)
            : GameManager.GetParameter(variableName);
        bool isTrue = EvaluateCondition(currentValue, comparison, compareValue);

        Debug.Log($"【Condition Node】条件判定: {variableName}(現在値: {currentValue}) {GetOperatorSymbol(comparison)} {compareValue} => 結果: {isTrue}");

        // Trueなら "True" ポート、Falseなら "False" ポートに繋がっているノードを探す
        string portName = isTrue ? "True" : "False";
        Debug.Log($"【Condition Node】{portName} ルートへ進みます。");
        player.ContinueTo(this, portName);
    }

    public string GetDisplayTitle()
    {
        return $"IF  {variableName} {GetOperatorSymbol(comparison)} {compareValue}";
    }

    private bool EvaluateCondition(int left, ConditionOperator op, int right)
    {
        switch (op)
        {
            case ConditionOperator.GreaterThan:
                return left > right;
            case ConditionOperator.GreaterOrEqual:
                return left >= right;
            case ConditionOperator.Equal:
                return left == right;
            case ConditionOperator.LessOrEqual:
                return left <= right;
            case ConditionOperator.LessThan:
                return left < right;
            case ConditionOperator.NotEqual:
                return left != right;
            default:
                return false;
        }
    }

    public static string GetOperatorSymbol(ConditionOperator op)
    {
        switch (op)
        {
            case ConditionOperator.GreaterThan: return ">";
            case ConditionOperator.GreaterOrEqual: return ">=";
            case ConditionOperator.Equal: return "==";
            case ConditionOperator.LessOrEqual: return "<=";
            case ConditionOperator.LessThan: return "<";
            case ConditionOperator.NotEqual: return "!=";
            default: return "?";
        }
    }
}
