using UnityEngine;
using XNode;

// ★線を繋ぐための「ダミーの型」を外に出しました
[System.Serializable]
public struct StoryFlow { }

public abstract class BaseStoryNode : Node
{
    // ★修正：TypeConstraint.None を付けることで、どんな出力ポート(NextでもChoicesでも)とも線を繋げる「最強の受け口」になります！
    [Input(ShowBackingValue.Never, ConnectionType.Multiple, TypeConstraint.None)]
    public StoryFlow enter;

    public abstract void Execute(VideoSelector selector);

    // ★追加：xNodeのポートを機能させるために必須のおまじない
    public override object GetValue(NodePort port)
    {
        return null;
    }

    public BaseStoryNode GetNextNode(string portName = "next")
    {
        NodePort port = GetOutputPort(portName);
        if (port != null && port.IsConnected)
        {
            return port.Connection.node as BaseStoryNode;
        }
        return null;
    }
}