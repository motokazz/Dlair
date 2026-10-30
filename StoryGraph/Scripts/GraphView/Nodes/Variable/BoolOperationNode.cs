using UnityEngine;

/// <summary>
/// Bool 変数への操作。On、Off、Toggle。
/// </summary>
public enum BoolOperation
{
    On,
    Off,
    Toggle
}

/// <summary>
/// Bool 変数を On / Off / Toggle する。
/// 変数名はデータベースか変数宣言ノードから選ぶ。
/// </summary>
public class BoolOperationNode : BaseNode
{
    public string variableName = "Flag";
    public BoolOperation operation = BoolOperation.On;

    public override void Execute(StoryPlayer player)
    {
        bool previous = GameManager.GetBool(variableName);
        bool next = previous;

        switch (operation)
        {
            case BoolOperation.On:
                next = true;
                break;
            case BoolOperation.Off:
                next = false;
                break;
            case BoolOperation.Toggle:
                next = !previous;
                break;
        }

        GameManager.SetBool(variableName, next);
        Debug.Log($"【Bool Operation Node】 {variableName}: {Format(previous)} → {Format(next)} ({GetOpLabel(operation)})");
        player.ContinueTo(this, "Next");
    }

    public string GetDisplayTitle()
    {
        string name = string.IsNullOrEmpty(variableName) ? "?" : variableName;
        return $"{GetOpLabel(operation)}  {name}";
    }

    public static string GetOpLabel(BoolOperation op)
    {
        switch (op)
        {
            case BoolOperation.On: return "ON";
            case BoolOperation.Off: return "OFF";
            case BoolOperation.Toggle: return "TOGGLE";
            default: return "BOOL";
        }
    }

    private static string Format(bool value)
    {
        return value ? "On" : "Off";
    }
}
