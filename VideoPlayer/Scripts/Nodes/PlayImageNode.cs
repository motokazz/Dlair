using UnityEngine;

[CreateNodeMenu("Story/2. Play Image")]
public class PlayImageNode : BaseStoryNode
{
    [Output(ShowBackingValue.Never, ConnectionType.Override)] public StoryFlow next;

    public string title = "New Image";
    public Texture2D image;

    [Tooltip("画像を表示し続ける秒数")]
    public float duration = 5.0f; // デフォルト5秒

    public AudioClip audioClip;
    public float crossFadeDuration = 1.0f; // 常に有効なフェード秒数

    public override void Execute(VideoSelector selector)
    {
        // isStaticImage = true で本体に渡す。画像はループしないので isLooping は false
        selector.PlayMedia(true, null, image, duration, false, audioClip, crossFadeDuration, this);
    }
}