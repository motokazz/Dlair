using UnityEngine;
using UnityEngine.Video;

[CreateNodeMenu("Story/1. Play Clip")]
public class PlayClipNode : BaseStoryNode
{
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    public string title = "New Video";
    public VideoClip clip;

    [Tooltip("-1で動画の最後まで再生。0より大きい数字を入れると、その秒数で次へ進みます。")]
    public float duration = -1f;

    public AudioClip audioClip;
    public bool isLooping = true;
    public float crossFadeDuration = 1.0f; // 常に有効なフェード秒数

    public override void Execute(VideoSelector selector)
    {
        // isStaticImage = false で本体に渡す
        selector.PlayMedia(false, clip, null, duration, isLooping, audioClip, crossFadeDuration, this);
    }
}