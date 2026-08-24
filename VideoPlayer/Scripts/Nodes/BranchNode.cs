using System.Collections.Generic;
using UnityEngine;

[CreateNodeMenu("Story/2. Branch (Choices)")]
public class BranchNode : BaseStoryNode
{
    [System.Serializable]
    public class BranchChoice
    {
        public string buttonText = "Choice";
        public string requiredParam = "";
        public int requiredValue = 0;
    }

    [Output(dynamicPortList = true)]
    public List<BranchChoice> choices = new List<BranchChoice>();

    public string messageText = ""; // Typewriterで出す文字など

    public override void Execute(VideoSelector selector)
    {
        // 本体に「選択肢UIを出してね」と命令するだけ
        selector.ShowChoices(this);
    }
}