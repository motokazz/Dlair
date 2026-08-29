using UnityEngine;
using UnityEngine.Video;

public class PlayClipFlowNode : BaseFlowNode
{
    public VideoClip clip;

    public override void Execute(StoryFlowPlayer player)
    {
        if (clip != null)
        {
            // ★第2引数に this (自分自身のノード) を渡して、どこから呼ばれたか教える
            player.PlayVideo(clip, this);
        }
    }
}