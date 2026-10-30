using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 説明は PlayClipNode.Summary。
/// </summary>
public class PlayClipNode : BaseNode, ISerializationCallbackReceiver
{
    public const string Summary =
        "Video Clip と Audio Clip を再生します。Video Clip が無くても Audio Clip だけ再生でき、そのときは画面の映像を変えません。\n" +
        "動画のループと音声のループは別々です。映像のクロスフェードと音声のフェードも別々です。\n" +
        "Next の秒数は、Video Clip があるときはその長さ、無いときは Audio Clip の長さが基準です。0 は開始と同時、負の値か長さ以上は終端です。\n" +
        "タイトル右のボタンでノード全体、設定ボタンで項目だけ、折りたたみと展開ができます。";

    public const string OutputTimingTooltip =
        "0: 開始と同時に Next へ。負の値、または基準の長さ以上: 終端。基準は Video Clip の長さ。Video Clip が無いときは Audio Clip の長さ。";

    public const string VideoFadeTooltip =
        "映像を切り替える秒数。Audio Clip の音量には影響しません。";

    public const string AudioFadeTooltip =
        "BGM・Voice・SE の音量を、鳴っている音からこの Audio Clip へ切り替える秒数。0 はすぐに切り替わります。映像のクロスフェードとは別です。";

    public VideoClip clip;
    public AudioClip audioClip;

    [Tooltip("この Audio Clip をどの音量スライダーで鳴らすか")]
    public StoryAudioBus audioBus = StoryAudioBus.BGM;

    [Tooltip(VideoFadeTooltip)]
    public float crossFadeDuration = 1.0f;

    [Tooltip(AudioFadeTooltip)]
    public float audioFadeDuration = 1.0f;

    [Tooltip("Video Clip のループ。Audio Clip には影響しません。")]
    public bool isLooping;

    [Tooltip("Audio Clip のループ。Video Clip には影響しません。")]
    public bool audioLoop;

    [Tooltip(OutputTimingTooltip)]
    public float portOutputTime = -1f;

    [Tooltip("オンでノード全体を展開、オフで折りたたみ。")]
    public bool nodeExpanded = true;

    [Tooltip("オンで設定項目を展開、オフで折りたたみ。")]
    public bool settingsExpanded = true;

    [SerializeField]
    private int portOutputTimeVersion = 4;

    /// <summary>
    /// グラフ上の説明文。Summary と同じです。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    /// <summary>
    /// Next の基準が動画の長さなら true。動画が無いときは音声の長さです。
    /// </summary>
    public bool UsesVideoForTiming => clip != null;

    /// <summary>
    /// Next の基準にする長さ（秒）。動画があればその長さ、無ければ音声の長さ。
    /// </summary>
    public float GetTimingLength()
    {
        if (clip != null) return Mathf.Max(0f, (float)clip.length);
        if (audioClip != null) return Mathf.Max(0f, audioClip.length);
        return 0f;
    }

    public override void Execute(StoryPlayer player)
    {
        if (player == null) return;
        if (clip == null && audioClip == null) return;

        player.PlayVideo(
            clip,
            this,
            isLooping,
            portOutputTime,
            audioClip,
            crossFadeDuration,
            audioBus,
            separateAudioLoop: true,
            audioLoop: audioLoop,
            audioFadeDuration: audioFadeDuration);
    }

    public void OnBeforeSerialize()
    {
        if (portOutputTimeVersion < 2)
        {
            portOutputTimeVersion = 2;
        }
    }

    public void OnAfterDeserialize()
    {
        if (portOutputTimeVersion < 1)
        {
            if (Mathf.Approximately(portOutputTime, 0f))
            {
                portOutputTime = -1f;
            }
            portOutputTimeVersion = 1;
        }

        if (portOutputTimeVersion < 2)
        {
            if (Mathf.Approximately(crossFadeDuration, 0f))
            {
                crossFadeDuration = 1.0f;
            }
            portOutputTimeVersion = 2;
        }

        if (portOutputTimeVersion < 3)
        {
            audioLoop = isLooping;
            nodeExpanded = true;
            settingsExpanded = true;
            portOutputTimeVersion = 3;
        }

        if (portOutputTimeVersion < 4)
        {
            audioFadeDuration = crossFadeDuration;
            portOutputTimeVersion = 4;
        }
    }
}
