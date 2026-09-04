using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class DualVideoPlayer : MonoBehaviour
{
    public enum AspectMode { Stretch, FitInside, FitOutside }

    [Header("Video Players")]
    public VideoPlayer playerA;
    public VideoPlayer playerB;

    [Header("Audio Sources")]
    public AudioSource audioSourceA;
    public AudioSource audioSourceB;

    [Header("Render Textures")]
    public RenderTexture texA;
    public RenderTexture texB;

    [Header("Transition Material")]
    public Material transitionMaterial;

    [Header("Settings")]
    public AspectMode aspectMode = AspectMode.FitOutside;

    public const float MinPlaybackSpeed = 0f;
    public const float MaxPlaybackSpeed = 10f;

    [Tooltip("現在の再生速度。1 が等速。ノード操作やクリップ切替でも維持されます。")]
    [SerializeField]
    private float playbackSpeed = 1f;

    public bool isPlayerA_Active = true;
    public bool IsTransitioning { get; private set; } = false;
    public float PlaybackSpeed => playbackSpeed;

    private Material glMaterial;
    private AudioSource bgmSource1;
    private AudioSource bgmSource2;
    private AudioSource currentBgmSource;

    private void Start()
    {
        if (audioSourceA != null) { playerA.audioOutputMode = VideoAudioOutputMode.AudioSource; playerA.EnableAudioTrack(0, true); playerA.SetTargetAudioSource(0, audioSourceA); }
        if (audioSourceB != null) { playerB.audioOutputMode = VideoAudioOutputMode.AudioSource; playerB.EnableAudioTrack(0, true); playerB.SetTargetAudioSource(0, audioSourceB); }

        bgmSource1 = gameObject.AddComponent<AudioSource>(); bgmSource1.playOnAwake = false;
        bgmSource2 = gameObject.AddComponent<AudioSource>(); bgmSource2.playOnAwake = false;
        currentBgmSource = bgmSource1;

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) glMaterial = new Material(shader);

        ClearRenderTexture(texA);
        ClearRenderTexture(texB);
        isPlayerA_Active = true;
        transitionMaterial.SetTexture("_MainTex", texA);
        transitionMaterial.SetTexture("_SubTex", texB);
        transitionMaterial.SetFloat("_Transition", 1f);
        ApplyPlaybackSpeedToPlayers();
    }

    public float ApplyPlaybackSpeedOperation(VariableOperation op, float operand)
    {
        float current = playbackSpeed;
        float result = current;

        switch (op)
        {
            case VariableOperation.Set:
                result = operand;
                break;
            case VariableOperation.Add:
                result = current + operand;
                break;
            case VariableOperation.Subtract:
                result = current - operand;
                break;
            case VariableOperation.Multiply:
                result = current * operand;
                break;
            case VariableOperation.Divide:
                if (operand != 0f) result = current / operand;
                else Debug.LogWarning("【DualVideoPlayer】再生速度のゼロ除算が試みられました。");
                break;
            case VariableOperation.Modulo:
                if (operand != 0f) result = current % operand;
                else Debug.LogWarning("【DualVideoPlayer】再生速度のゼロ剰余が試みられました。");
                break;
        }

        SetPlaybackSpeed(result);
        return playbackSpeed;
    }

    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, MinPlaybackSpeed, MaxPlaybackSpeed);
        ApplyPlaybackSpeedToPlayers();
    }

    private void ApplyPlaybackSpeedToPlayers()
    {
        if (playerA != null) playerA.playbackSpeed = playbackSpeed;
        if (playerB != null) playerB.playbackSpeed = playbackSpeed;
    }

    private void ClearRenderTexture(RenderTexture rt)
    {
        if (rt == null) return;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = previous;
    }

    // ==========================================
    // ★大修正：キャッシュ破壊のトリックを導入！
    // ==========================================
    private void SetupVideoPlayer(VideoPlayer player, RenderTexture targetRT, VideoClip clip, bool isLooping)
    {
        player.Stop();

        // 1. ターゲットを一度nullにして、前回の動画の記憶（キャッシュ）を強制的に消去する！
        player.targetTexture = null;

        // 2. モードとクリップを設定
        player.renderMode = VideoRenderMode.RenderTexture;
        player.clip = clip;
        player.isLooping = isLooping;

        // 3. この「記憶喪失」の状態でアスペクト比を叩き込む
        VideoAspectRatio ratio = VideoAspectRatio.Stretch;
        if (aspectMode == AspectMode.FitInside) ratio = VideoAspectRatio.FitInside;
        else if (aspectMode == AspectMode.FitOutside) ratio = VideoAspectRatio.FitOutside;
        player.aspectRatio = ratio;

        // 4. 最後にRenderTextureを再アタッチ（これで新しい動画の比率として正しく計算される）
        player.targetTexture = targetRT;
        player.playbackSpeed = playbackSpeed;
    }

    private void DrawImageToRenderTexture(Texture2D image, RenderTexture targetRT)
    {
        if (image == null || targetRT == null) return;
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = targetRT;
        GL.Clear(true, true, Color.black);

        if (aspectMode == AspectMode.Stretch || glMaterial == null)
        {
            Graphics.Blit(image, targetRT);
            RenderTexture.active = previous;
            return;
        }

        float srcAspect = (float)image.width / image.height;
        float dstAspect = (float)targetRT.width / targetRT.height;
        float w = 1.0f; float h = 1.0f;

        if (aspectMode == AspectMode.FitInside) { if (srcAspect > dstAspect) h = dstAspect / srcAspect; else w = srcAspect / dstAspect; }
        else if (aspectMode == AspectMode.FitOutside) { if (srcAspect > dstAspect) w = srcAspect / dstAspect; else h = dstAspect / srcAspect; }

        float x = (1.0f - w) * 0.5f; float y = (1.0f - h) * 0.5f;
        GL.PushMatrix();
        GL.Viewport(new Rect(0, 0, targetRT.width, targetRT.height));
        GL.LoadOrtho();

        glMaterial.mainTexture = image;
        glMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        GL.TexCoord2(0, 0); GL.Vertex3(x, y, 0);
        GL.TexCoord2(0, 1); GL.Vertex3(x, y + h, 0);
        GL.TexCoord2(1, 1); GL.Vertex3(x + w, y + h, 0);
        GL.TexCoord2(1, 0); GL.Vertex3(x + w, y, 0);
        GL.End();

        GL.PopMatrix();
        RenderTexture.active = previous;
    }

    public IEnumerator WarmUpDecoder(VideoClip dummyClip)
    {
        IsTransitioning = true;
        transitionMaterial.SetFloat("_GlobalAlpha", 0f);

        SetupVideoPlayer(playerA, texA, dummyClip, false);

        playerA.Prepare();
        while (!playerA.isPrepared) yield return null;

        playerA.Play();

        yield return new WaitForSeconds(0.5f);
        playerA.Stop();

        bgmSource1.Stop();
        bgmSource2.Stop();
        ClearRenderTexture(texA);
        ClearRenderTexture(texB);

        isPlayerA_Active = false;
        transitionMaterial.SetFloat("_Transition", 1f);
        IsTransitioning = false;
    }

    public void PlayFirstMedia(bool isStaticImage, VideoClip clip, Texture2D image, bool isLooping, AudioClip bgmClip)
    {
        if (IsTransitioning) return;
        StartCoroutine(PlayFirstMediaRoutine(isStaticImage, clip, image, isLooping, bgmClip));
    }

    private IEnumerator PlayFirstMediaRoutine(bool isStaticImage, VideoClip clip, Texture2D image, bool isLooping, AudioClip bgmClip)
    {
        IsTransitioning = true;
        isPlayerA_Active = true;
        ClearRenderTexture(texA);
        ClearRenderTexture(texB);

        transitionMaterial.SetTexture("_SubTex", texB);
        transitionMaterial.SetFloat("_Transition", 1f);

        currentBgmSource.Stop();
        if (bgmClip != null) { currentBgmSource.clip = bgmClip; currentBgmSource.loop = isLooping; currentBgmSource.volume = 1f; currentBgmSource.Play(); }

        if (isStaticImage)
        {
            playerA.Stop();
            DrawImageToRenderTexture(image, texA);
            yield return null;
        }
        else
        {
            SetupVideoPlayer(playerA, texA, clip, isLooping); // ★魔法のメソッドを通す！

            playerA.Prepare();
            while (!playerA.isPrepared) yield return null;

            playerA.Play();
            playerA.playbackSpeed = playbackSpeed;
            yield return WaitAndPreRender(playerA, texA);
        }

        float time = 0;
        float fadeInDuration = 0.5f;
        while (time < fadeInDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / fadeInDuration);
            transitionMaterial.SetFloat("_Transition", Mathf.Lerp(1f, 0f, t));
            if (transitionMaterial.GetFloat("_GlobalAlpha") < 1.0f) transitionMaterial.SetFloat("_GlobalAlpha", Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        transitionMaterial.SetFloat("_Transition", 0f);
        transitionMaterial.SetFloat("_GlobalAlpha", 1.0f);
        IsTransitioning = false;
    }

    public void RequestPlayNextMedia(bool isStaticImage, VideoClip nextClip, Texture2D nextImage, bool isLooping, AudioClip nextBgm, float fadeDuration)
    {
        if (IsTransitioning) return;
        StartCoroutine(TransitionRoutine(isStaticImage, nextClip, nextImage, isLooping, nextBgm, fadeDuration));
    }

    private IEnumerator TransitionRoutine(bool isStaticImage, VideoClip nextClip, Texture2D nextImage, bool isLooping, AudioClip nextBgm, float fadeDuration)
    {
        IsTransitioning = true;
        VideoPlayer activePlayer = isPlayerA_Active ? playerA : playerB;
        VideoPlayer nextPlayer = isPlayerA_Active ? playerB : playerA;
        RenderTexture targetRT = isPlayerA_Active ? texB : texA;

        activePlayer.isLooping = false;
        ClearRenderTexture(targetRT);
        bool keepSameBgm = (currentBgmSource.isPlaying && nextBgm != null && currentBgmSource.clip == nextBgm);
        AudioSource fadingOutBgmSource = null;
        AudioSource fadingInBgmSource = null;

        if (keepSameBgm) { currentBgmSource.loop = isLooping; }
        else
        {
            fadingOutBgmSource = currentBgmSource;
            fadingInBgmSource = (currentBgmSource == bgmSource1) ? bgmSource2 : bgmSource1;
            fadingInBgmSource.Stop();
            if (nextBgm != null) { fadingInBgmSource.clip = nextBgm; fadingInBgmSource.loop = isLooping; fadingInBgmSource.volume = 0f; fadingInBgmSource.Play(); }
        }

        if (isStaticImage)
        {
            nextPlayer.Stop();
            DrawImageToRenderTexture(nextImage, targetRT);
            yield return null;
        }
        else
        {
            SetupVideoPlayer(nextPlayer, targetRT, nextClip, isLooping); // ★ここでも魔法のメソッドを通す！

            nextPlayer.Prepare();
            while (!nextPlayer.isPrepared) yield return null;

            nextPlayer.Play();
            nextPlayer.playbackSpeed = playbackSpeed;
            yield return WaitAndPreRender(nextPlayer, targetRT);
        }

        float time = 0;
        float startValue = isPlayerA_Active ? 0f : 1f;
        float endValue = isPlayerA_Active ? 1f : 0f;

        while (time < fadeDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = (fadeDuration > 0f) ? Mathf.Clamp01(time / fadeDuration) : 1f;
            transitionMaterial.SetFloat("_Transition", Mathf.Lerp(startValue, endValue, t));
            if (transitionMaterial.GetFloat("_GlobalAlpha") < 1.0f) transitionMaterial.SetFloat("_GlobalAlpha", Mathf.Lerp(0f, 1f, t));
            if (!keepSameBgm) { if (nextBgm != null) fadingInBgmSource.volume = t; if (fadingOutBgmSource.isPlaying) fadingOutBgmSource.volume = 1f - t; }
            yield return null;
        }

        transitionMaterial.SetFloat("_Transition", endValue);
        transitionMaterial.SetFloat("_GlobalAlpha", 1.0f);
        if (!keepSameBgm) { if (nextBgm != null) fadingInBgmSource.volume = 1f; fadingOutBgmSource.Stop(); currentBgmSource = fadingInBgmSource; }

        activePlayer.Stop();
        isPlayerA_Active = !isPlayerA_Active;
        IsTransitioning = false;
    }

    private IEnumerator WaitAndPreRender(VideoPlayer player, RenderTexture rt)
    {
        yield return null; yield return null; yield return null;
        float timeout = 1.0f; float timer = 0f;
        while (player.time <= 0.05f && timer < timeout) { timer += Time.unscaledDeltaTime; yield return null; }
    }

    public VideoPlayer GetActivePlayer() { return isPlayerA_Active ? playerA : playerB; }
}