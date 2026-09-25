using UnityEngine;

public class PlaybackSpeedNode : BaseNode
{
    public VariableOperation operation = VariableOperation.Set;
    public float operandValue = 1f;
    public bool useVariableOperand;
    public string operandVariableName = "";

    public override void Execute(StoryPlayer player)
    {
        if (player == null) return;

        float finalOperand = ResolveOperand();

        if (player.dualPlayer != null)
        {
            float result = player.dualPlayer.ApplyPlaybackSpeedOperation(operation, finalOperand);
            Debug.Log($"【Playback Speed Node】playbackSpeed {VariableOperationNode.GetOpSymbol(operation)} {finalOperand} (結果: {result})");
        }
        else
        {
            Debug.LogWarning("【Playback Speed Node】DualVideoPlayer が未設定です。");
        }

        player.ContinueTo(this, "Next");
    }

    public string GetDisplayTitle()
    {
        string right = useVariableOperand && !string.IsNullOrEmpty(operandVariableName)
            ? operandVariableName
            : operandValue.ToString("0.###");
        return $"SPEED  {VariableOperationNode.GetOpSymbol(operation)} {right}";
    }

    private float ResolveOperand()
    {
        if (!useVariableOperand || string.IsNullOrEmpty(operandVariableName))
        {
            return operandValue;
        }

        return GameManager.TryGetNumeric(operandVariableName, out float value) ? value : 0f;
    }
}
