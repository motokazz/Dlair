using UnityEngine;

public class VariableOperationNode : BaseNode
{
    public string variableName = "A";                                      // 演算対象の変数名
    public VariableOperation operation = VariableOperation.Add;            // 演算の種類 (代入/加算/減算/乗算/除算/剰余)
    
    public bool useVariableOperand = false;                                 // 他の変数の値を使うか
    public int operandValue = 1;                                           // 直接指定する数値
    public string operandVariableName = "";                                // 参照する他の変数名

    public override void Execute(StoryPlayer player)
    {
        int finalOperand = operandValue;
        if (useVariableOperand && !string.IsNullOrEmpty(operandVariableName))
        {
            finalOperand = GameManager.GetParameter(operandVariableName, 0);
        }

        GameManager.ApplyOperation(variableName, operation, finalOperand);
        Debug.Log($"【Variable Operation Node】 {variableName} {GetOpSymbol(operation)} {finalOperand} (結果: {GameManager.GetParameter(variableName)})");

        player.ContinueTo(this, "Next");
    }

    public string GetDisplayTitle()
    {
        string right = useVariableOperand && !string.IsNullOrEmpty(operandVariableName)
            ? operandVariableName
            : operandValue.ToString();
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