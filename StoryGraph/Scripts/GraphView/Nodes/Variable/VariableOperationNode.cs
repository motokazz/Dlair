using System.Globalization;
using UnityEngine;

/// <summary>
/// 変数へ代入・加減乗除する。計算は常に小数。Float 変数はそのまま入れ、それ以外は結果の小数点以下を切り捨てる。
/// 右辺は数値か、別の変数。変数は Float、Int、Bool を数値として読む。
/// 変数名はデータベースか変数宣言ノードから選ぶ。
/// </summary>
public class VariableOperationNode : BaseNode
{
    public string variableName = "A";                                      // 演算対象の変数名
    public VariableOperation operation = VariableOperation.Add;            // 演算の種類 (代入/加算/減算/乗算/除算/剰余)
    
    public bool useVariableOperand = false;                                 // 他の変数の値を使うか
    public int operandValue = 1;                                           // 小数を入れるまではこちらを使う
    public float floatOperandValue = 1f;                                   // 右辺の小数
    [HideInInspector] public bool floatOperandReady;                       // 小数フィールドを編集したら true
    public string operandVariableName = "";                                // 参照する他の変数名

    public float FloatLiteral()
    {
        return floatOperandReady ? floatOperandValue : operandValue;
    }

    public override void Execute(StoryPlayer player)
    {
        float finalOperand = FloatLiteral();
        if (useVariableOperand && !string.IsNullOrEmpty(operandVariableName))
        {
            finalOperand = GameManager.TryGetNumeric(operandVariableName, out float number) ? number : 0f;
        }

        if (GameManager.HasFloat(variableName))
        {
            GameManager.ApplyOperation(variableName, operation, finalOperand);
            Debug.Log($"【Variable Operation Node】 {variableName} {GetOpSymbol(operation)} {finalOperand} (結果: {GameManager.GetFloat(variableName)})");
        }
        else
        {
            GameManager.ApplyOperationToInt(variableName, operation, finalOperand);
            Debug.Log($"【Variable Operation Node】 {variableName} {GetOpSymbol(operation)} {finalOperand} (結果: {GameManager.GetParameter(variableName)})");
        }

        player.ContinueTo(this, "Next");
    }

    public string GetDisplayTitle()
    {
        string right = useVariableOperand && !string.IsNullOrEmpty(operandVariableName)
            ? operandVariableName
            : FloatLiteral().ToString("0.###", CultureInfo.InvariantCulture);

        return $"{GetOpLabel(operation)}  {variableName} {GetOpSymbol(operation)} {right}";
    }

    public static string GetOpLabel(VariableOperation op)
    {
        switch (op)
        {
            case VariableOperation.Set: return "SET";
            case VariableOperation.Add: return "ADD";
            case VariableOperation.Subtract: return "SUB";
            case VariableOperation.Multiply: return "MUL";
            case VariableOperation.Divide: return "DIV";
            case VariableOperation.Modulo: return "MOD";
            default: return "OP";
        }
    }

    public static string GetOpSymbol(VariableOperation op)
    {
        switch (op)
        {
            case VariableOperation.Set: return "=";
            case VariableOperation.Add: return "+=";
            case VariableOperation.Subtract: return "-=";
            case VariableOperation.Multiply: return "*=";
            case VariableOperation.Divide: return "/=";
            case VariableOperation.Modulo: return "%=";
            default: return "=";
        }
    }
}