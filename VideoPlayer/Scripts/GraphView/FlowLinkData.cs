using System;

[Serializable]
public class FlowLinkData
{
    public string baseNodeGuid;   // 線の出発点となるノードのID
    public string portName;       // 線の出発点のポート名（例："next", "choices 0"など）
    public string targetNodeGuid; // 線の到着点となるノードのID
}