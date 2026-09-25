using UnityEngine;
using UnityEngine.Video;

public class PlayClipNode : BaseNode, ISerializationCallbackReceiver
{
    public VideoClip clip;
    public AudioClip audioClip;
    public float crossFadeDuration = 1.0f;
    public bool isLooping;

    [Tooltip("0: 開始と同時 / 負の値: 終端 / 動画の長さ以上: 終端")]
    public float portOutputTime = -1f;

    [SerializeField]
    private int portOutputTimeVersion;

    public override void Execute(StoryPlayer player)
    {
        if (clip != null)
        {
            player.PlayVideo(clip, this, isLooping, portOutputTime, audioClip, crossFadeDuration);
        }
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
    }
}