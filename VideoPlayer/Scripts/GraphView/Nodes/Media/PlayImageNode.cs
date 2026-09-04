using UnityEngine;

public class PlayImageNode : BaseNode
{
    public Texture2D image;

    [Tooltip("画像を表示し続ける秒数")]
    public float duration = 5.0f;

    public AudioClip audioClip;
    public float crossFadeDuration = 1.0f;
    public bool isLooping;

    [Tooltip("0: 開始と同時 / 負の値: 表示終了時 / 表示秒数以上: 表示終了時")]
    public float portOutputTime = -1f;

    public override void Execute(StoryPlayer player)
    {
        if (image != null)
        {
            player.PlayImage(image, duration, audioClip, isLooping, crossFadeDuration, this, portOutputTime);
        }
    }
}
