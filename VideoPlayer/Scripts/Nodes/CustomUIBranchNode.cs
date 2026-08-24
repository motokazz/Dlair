using System.Collections.Generic;
using UnityEngine;

[CreateNodeMenu("Story/5. Custom UI Branch (自作UI分岐)")]
[NodeTint("#b71c1c")] // デザイン系のノードなので赤色にして目立たせます
public class CustomUIBranchNode : BaseStoryNode
{
    [Header("自作UIのプレハブ")]
    public GameObject customUIPrefab;

    [System.Serializable]
    public class BranchChoice
    {
        public string memo = "メモ（表示はされません）"; // エディタ上で自分が分かりやすいように書くメモ
        public string requiredParam = "";
        public int requiredValue = 0;
    }

    [Output(dynamicPortList = true)]
    public List<BranchChoice> choices = new List<BranchChoice>();

    public override void Execute(VideoSelector selector)
    {
        // 本体に「自作のUIを出してね」と命令する
        selector.ShowCustomChoices(this);
    }
}