using System;

/// <summary>
/// ノード間の1本の接続。出発ノード、ポート名、到着ノード。
/// </summary>
[Serializable]
public class StoryLinkData
{
    public string baseNodeGuid;   // 線の出発点となるノードのID
    public string portName;       // 線の出発点のポート名（例："next", "choices 0"など）
    public string targetNodeGuid; // 線の到着点となるノードのID
}