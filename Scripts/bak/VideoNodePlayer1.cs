using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;

[RequireComponent(typeof(VideoPlayer))]
public class VideoNodePlayer1 : MonoBehaviour
{
    [SerializeField] private RenderTexture targetTexture;  // RawImageに割り当て

    public VideoPlayer player;

    public event Action OnVideoEnded;

    private void Awake()
    {
        player = GetComponent<VideoPlayer>() ?? gameObject.AddComponent<VideoPlayer>();

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = targetTexture;
        player.isLooping = false;
        player.waitForFirstFrame = true;      // これが黒画面防止に重要
        player.skipOnDrop = false;
        player.playOnAwake = false;

        player.prepareCompleted += _ => OnPrepared();
        player.loopPointReached += _ => OnVideoEnded?.Invoke();
    }
    void Start()
    {
        player.Prepare();
    }

    public void PlayClip(VideoClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("Clip null");
            return;
        }

        player.Stop();
        player.time = 0;  // リセット
        player.clip = clip;
        player.Prepare();  // → OnPreparedでPlay
    }

    public void PauseClip()
    {
        player.Pause();
    }

    public void ResumeClip()
    {
        player.Play();
    }

    private void OnPrepared()
    {
        player.Play();
        Debug.Log($"再生開始: {player.clip?.name} ({player.clip.length:F2}s)");
    }

}