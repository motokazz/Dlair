using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;

[RequireComponent(typeof(VideoPlayer))]
public class VideoNodePlayer : MonoBehaviour
{
    [SerializeField] private RenderTexture targetTexture;  // RawImageに割り当て

    private VideoPlayer player;

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

    
    IEnumerator PlayerPrepare()
    {
        yield return new WaitUntil(()=> player.isPrepared);
        yield return null;
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
        PlayerPrepare();
        player.Play();
        Debug.Log($"再生開始: {player.clip?.name} ({player.clip.length:F2}s)");
    }

    // 切り替え時の黒残り防止（任意・呼ぶと良い）
    public void ClearTarget()
    {
        if (targetTexture == null) return;
        var prev = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, Color.black);  // または Color.clear
        RenderTexture.active = prev;
    }
}