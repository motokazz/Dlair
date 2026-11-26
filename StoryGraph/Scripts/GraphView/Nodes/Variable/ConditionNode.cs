using System.Globalization;
using UnityEngine;

/// <summary>
/// 数値の比較。より大きい、以上、等しい、以下、より小さい、等しくない。
/// </summary>
public enum ConditionOperator
{
    GreaterThan,      // > (より大きい)
    GreaterOrEqual,   // >= (以上)
    Equal,            // == (等しい)
    LessOrEqual,      // <= (以下)
    LessThan,         // < (より小さい)
    NotEqual          // != (等しくない)
}

/// <summary>
/// 変数を数値として比較し、True / False へ進む。
/// Float はそのまま比べる。Bool は Off=0 / On=1。それ以外は整数。
/// 変数名はデータベースか変数宣言ノードから選ぶ。
/// </summary>
public class ConditionNode : BaseNode
{
    public string variableName = "A";                                      // 判定する変数名
    public ConditionOperator comparison = ConditionOperator.GreaterThan;  // 比較演算子
    public int compareValue = 0;                                           // 整数・Bool の比較値。Float で小数を入れるまではこちらを使う
    public float floatCompareValue;                                        // Float 変数へ使う比較値
    [HideInInspector] public bool floatCompareReady;                       // 小数フィールドを編集したら true

    public float FloatCompareValue()
    {
        return floatCompareReady ? floatCompareValue : compareValue;
    }

    public override void Execute(StoryPlayer player)
    {
        bool isTrue;
        string detail;
        if (GameManager.HasFloat(variableName))
        {
            float currentValue = GameManager.GetFloat(variableName);
            float right = FloatCompareValue();
            isTrue = EvaluateFloat(currentValue, comparison, right);
            detail = $"{variableName}(現在値: {currentValue}) {GetOperatorSymbol(comparison)} {right} => 結果: {isTrue}";
        }
        else
        {
            int currentValue = GameManager.HasBool(variableName)
                ? (GameManager.GetBool(variableName) ? 1 : 0)
                : GameManager.GetParameter(variableName);
            isTrue = EvaluateCondition(currentValue, comparison, compareValue);
            detail = $"{variableName}(現在値: {currentValue}) {GetOperatorSymbol(comparison)} {compareValue} => 結果: {isTrue}";
        }

        Debug.Log($"【Condition Node】条件判定: {detail}");

        string portName = isTrue ? "True" : "False";
        Debug.Log($"【Condition Node】{portName} ルートへ進みます。");
        player.ContinueTo(this, portName);
    }

    public string GetDisplayTitle(bool floatLiteral = false)
    {
        string right = floatLiteral
            ? FloatCompareValue().ToString("0.###", CultureInfo.InvariantCulture)
            : compareValue.ToString(CultureInfo.InvariantCulture);
        return $"IF  {variableName} {GetOperatorSymbol(comparison)} {right}";
    }

    private static bool EvaluateFloat(float left, ConditionOperator op, float right)
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
