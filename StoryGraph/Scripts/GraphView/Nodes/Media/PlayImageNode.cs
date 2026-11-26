using UnityEngine;

/// <summary>
/// 静止画と任意の音声を、指定秒数表示する。
/// 画像にアルファがあるときは、その透過も画面に出します。
/// </summary>
public class PlayImageNode : BaseNode
{
    public Texture2D image;

    [Tooltip("画像を表示し続ける秒数")]
    public float duration = 5.0f;

    public AudioClip audioClip;

    [Tooltip("選ぶと、この変数の Audio Clip を再生します。未選択なら Audio Clip 欄です。")]
    public string audioVariable = "";

    [Tooltip("この Audio Clip をどの音量スライダーで鳴らすか")]
    public StoryAudioBus audioBus = StoryAudioBus.BGM;
    public float crossFadeDuration = 1.0f;
    public bool isLooping;

    [Tooltip("0: 開始と同時 / 負の値: 表示終了時 / 表示秒数以上: 表示終了時")]
    public float portOutputTime = -1f;

    public override void Execute(StoryPlayer player)
    {
        if (image != null)
        {
            player.PlayImage(image, duration, ResolveAudio(), isLooping, crossFadeDuration, this, portOutputTime, audioBus);
        }
    }

    /// <summary>
    /// 変数が選ばれていればその Audio Clip。未選択ならノードの Audio Clip。
    /// </summary>
    public AudioClip ResolveAudio()
    {
        if (!string.IsNullOrWhiteSpace(audioVariable))
        {
            return GameManager.GetAudioClip(audioVariable.Trim());
        }

        return audioClip;
    }
}
