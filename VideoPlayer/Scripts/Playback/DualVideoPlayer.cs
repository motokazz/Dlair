using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 2台の VideoPlayer を切り替え、クロスフェードで映像を出す。
/// 動画の音声と、BGM・ボイス・SE の再生もここで行う。
/// </summary>
public class DualVideoPlayer : MonoBehaviour
{
    /// <summary>
    /// 映像を RenderTexture に収めるときの比率。引き伸ばし、内側合わせ、外側合わせ。
    /// </summary>
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

    [Tooltip("映像を表示している RawImage。DualVideoPlayer のマテリアル差し替えを画面側にも反映します。")]
    public RawImage displayImage;

    [Header("Settings")]
    public AspectMode aspectMode = AspectMode.FitOutside;

    /// <summary>再生速度の下限。</summary>
    public const float MinPlaybackSpeed = 0f;
    /// <summary>再生速度の上限。</summary>
    public const float MaxPlaybackSpeed = 10f;

    [Tooltip("現在の再生速度。1 が等速。ノード操作やクリップ切替でも維持されます。")]
    [SerializeField]
    private float playbackSpeed = 1f;

    public bool isPlayerA_Active = true;
    /// <summary>クロスフェード中なら true。この間の再生要求は受け付けない。</summary>
    public bool IsTransitioning { get; private set; } = false;
    /// <summary>現在の再生速度。1 が等速。</summary>
    public float PlaybackSpeed => playbackSpeed;

    private Material glMaterial;
    private Material runtimeMaterial;
    private AudioSource voiceSource1;
    private AudioSource voiceSource2;
    private AudioSource currentVoiceSource;
    private AudioSource seSource1;
    private AudioSource seSource2;
    private AudioSource currentSeSource;
    private AudioSource bgmSource1;
    private AudioSource bgmSource2;
    private AudioSource currentBgmSource;
    private int playbackGeneration;
    private int voiceFadeGeneration;
    private int seFadeGeneration;

    private Material ActiveMaterial => runtimeMaterial != null ? runtimeMaterial : transitionMaterial;

    private void Awake()
    {
        if (transitionMaterial != null)
            runtimeMaterial = new Material(transitionMaterial);

        ResolveDisplayImage();
        ApplyMaterialToDisplay();
    }

    private void Start()
    {
        ConfigureDirectAudio(playerA);
        ConfigureDirectAudio(playerB);

        bgmSource1 = gameObject.AddComponent<AudioSource>(); bgmSource1.playOnAwake = false;
        bgmSource2 = gameObject.AddComponent<AudioSource>(); bgmSource2.playOnAwake = false;
        currentBgmSource = bgmSource1;
        voiceSource1 = AddFlatSource();
        voiceSource2 = AddFlatSource();
        currentVoiceSource = voiceSource1;
        seSource1 = AddFlatSource();
        seSource2 = AddFlatSource();
        currentSeSource = seSource1;
        AssignBgmMixerGroup();

        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) glMaterial = new Material(shader);

        ClearRenderTexture(texA);
        ClearRenderTexture(texB);
        isPlayerA_Active = true;
        BindTransitionTextures();
        SetTransitionFloat("_Transition", 1f);
        ApplyPlaybackSpeedToPlayers();
    }

    private void AssignBgmMixerGroup()
    {
        AudioMixerGroup bgmGroup = AudioOptions.FindGroupForOption("BGM");
        AudioMixerGroup voiceGroup = AudioOptions.FindGroupForOption("Voice");
        AudioMixerGroup seGroup = AudioOptions.FindGroupForOption("SE");
        if (bgmGroup != null)
        {
            bgmSource1.outputAudioMixerGroup = bgmGroup;
            bgmSource2.outputAudioMixerGroup = bgmGroup;
        }
        if (voiceGroup != null)
        {
            voiceSource1.outputAudioMixerGroup = voiceGroup;
            voiceSource2.outputAudioMixerGroup = voiceGroup;
        }
        if (seGroup != null)
        {
            seSource1.outputAudioMixerGroup = seGroup;
            seSource2.outputAudioMixerGroup = seGroup;
        }
    }

    private AudioSource AddFlatSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private AudioSource CurrentSource(StoryAudioBus bus)
    {
        if (bus == StoryAudioBus.Voice) return currentVoiceSource;
        if (bus == StoryAudioBus.SE) return currentSeSource;
        return currentBgmSource;
    }

    private AudioSource OtherSource(StoryAudioBus bus, AudioSource current)
    {
        if (bus == StoryAudioBus.Voice) return current == voiceSource1 ? voiceSource2 : voiceSource1;
        if (bus == StoryAudioBus.SE) return current == seSource1 ? seSource2 : seSource1;
        return current == bgmSource1 ? bgmSource2 : bgmSource1;
    }

    private void SetCurrentSource(StoryAudioBus bus, AudioSource source)
    {
        if (bus == StoryAudioBus.Voice) currentVoiceSource = source;
        else if (bus == StoryAudioBus.SE) currentSeSource = source;
        else currentBgmSource = source;
    }

    private void StopBusExcept(StoryAudioBus bus, AudioSource keep)
    {
        if (bus == StoryAudioBus.Voice)
        {
            if (voiceSource1 != null && voiceSource1 != keep) voiceSource1.Stop();
            if (voiceSource2 != null && voiceSource2 != keep) voiceSource2.Stop();
            return;
        }

        if (bus == StoryAudioBus.SE)
        {
            if (seSource1 != null && seSource1 != keep) seSource1.Stop();
            if (seSource2 != null && seSource2 != keep) seSource2.Stop();
            return;
        }

        if (bgmSource1 != null && bgmSource1 != keep) bgmSource1.Stop();
        if (bgmSource2 != null && bgmSource2 != keep) bgmSource2.Stop();
    }

    private int FadeGeneration(StoryAudioBus bus)
    {
        if (bus == StoryAudioBus.Voice) return voiceFadeGeneration;
        if (bus == StoryAudioBus.SE) return seFadeGeneration;
        return playbackGeneration;
    }

    private int NextFadeGeneration(StoryAudioBus bus)
    {
        if (bus == StoryAudioBus.Voice) return ++voiceFadeGeneration;
        if (bus == StoryAudioBus.SE) return ++seFadeGeneration;
        return ++playbackGeneration;
    }

    private void PlayCategoryClip(StoryAudioBus bus, AudioClip clip, bool loop)
    {
        if (clip == null || bus == StoryAudioBus.BGM) return;

        AudioSource source = CurrentSource(bus);
        if (source == null) return;

        StopBusExcept(bus, source);
        AudioOptions.ApplyAll();
        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.volume = 1f;
        source.Play();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null) return;

        if (displayImage != null && displayImage.material == runtimeMaterial)
            displayImage.material = transitionMaterial;

        Destroy(runtimeMaterial);
        runtimeMaterial = null;
    }

    private void ResolveDisplayImage()
    {
        if (displayImage != null) return;

        RawImage[] images = FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (transitionMaterial != null)
        {
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].material == transitionMaterial)
                {
                    displayImage = images[i];
                    return;
                }
            }
        }

        if (images.Length == 1)
            displayImage = images[0];
        else if (images.Length == 0)
            Debug.LogWarning("【DualVideoPlayer】表示用 RawImage が見つかりません。transitionMaterial を差し替えても画面に反映されません。");
        else
            Debug.LogWarning("【DualVideoPlayer】表示用 RawImage が複数あります。Inspector の Display Image を指定してください。");
    }

    private void ApplyMaterialToDisplay()
    {
        Material mat = ActiveMaterial;
        if (displayImage == null || mat == null) return;

        displayImage.material = mat;
        displayImage.texture = null;
    }

    private void BindTransitionTextures()
    {
        Material mat = ActiveMaterial;
        if (mat == null) return;
        mat.SetTexture("_MainTex", texA);
        mat.SetTexture("_SubTex", texB);
    }

    private void SetTransitionFloat(string property, float value)
    {
        Material mat = ActiveMaterial;
        if (mat != null) mat.SetFloat(property, value);
    }

    private float GetTransitionFloat(string property)
    {
        Material mat = ActiveMaterial;
        return mat != null ? mat.GetFloat(property) : 0f;
    }

    /// <summary>
    /// 再生速度へ演算を適用し、クランプ後の速度を返す。
    /// </summary>
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

    /// <summary>
    /// 再生速度を範囲内に収め、両方の VideoPlayer へ反映する。
    /// </summary>
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

        if (clip != null && clip.audioTrackCount > 0)
            player.controlledAudioTrackCount = clip.audioTrackCount;

        ConfigureDirectAudio(player);
    }

    private IEnumerator PrepareVideoAudio(VideoPlayer player)
    {
        ConfigureDirectAudio(player);
        player.Prepare();
        while (!player.isPrepared)
            yield return null;
    }

    private void PlayWithVolume(VideoPlayer player)
    {
        player.Play();
        ApplyDirectVolumes(player);
        StartCoroutine(ReapplyDirectVolume(player));
    }

    private void ConfigureDirectAudio(VideoPlayer player)
    {
        if (player == null) return;

        // AudioSource に乗せると、この環境では動画の音声が出ない。
        player.audioOutputMode = VideoAudioOutputMode.Direct;
        if (player.controlledAudioTrackCount < 1)
            player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetDirectAudioMute(0, false);
    }

    private void ApplyDirectVolumes(VideoPlayer player)
    {
        if (player == null) return;

        ushort trackCount = player.audioTrackCount;
        if (trackCount < 1)
            trackCount = player.controlledAudioTrackCount > 0 ? player.controlledAudioTrackCount : (ushort)1;

        float voice = 1f;
        float se = 1f;
        for (ushort track = 0; track < trackCount; track++)
        {
            player.EnableAudioTrack(track, true);
            player.SetDirectAudioMute(track, false);
            player.SetDirectAudioVolume(track, track == 0 ? voice : se);
        }
    }

    private IEnumerator ReapplyDirectVolume(VideoPlayer player)
    {
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            ApplyDirectVolumes(player);
        }
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

    /// <summary>
    /// ダミークリップを一度再生してデコーダを温め、画面は非表示のまま止める。
    /// </summary>
    public IEnumerator WarmUpDecoder(VideoClip dummyClip)
    {
        IsTransitioning = true;
        SetTransitionFloat("_GlobalAlpha", 0f);

        SetupVideoPlayer(playerA, texA, dummyClip, false);
        yield return PrepareVideoAudio(playerA);
        PlayWithVolume(playerA);

        yield return new WaitForSeconds(0.5f);
        playerA.Stop();

        bgmSource1.Stop();
        bgmSource2.Stop();
        ClearRenderTexture(texA);
        ClearRenderTexture(texB);

        isPlayerA_Active = false;
        SetTransitionFloat("_Transition", 1f);
        IsTransitioning = false;
    }

    /// <summary>
    /// 最初のメディアを Player A で再生し、フェードインする。遷移中は無視する。
    /// </summary>
    public void PlayFirstMedia(bool isStaticImage, VideoClip clip, Texture2D image, bool isLooping, AudioClip bgmClip, StoryAudioBus audioBus = StoryAudioBus.BGM, bool separateAudioLoop = false, bool audioLoop = false)
    {
        if (IsTransitioning) return;
        NextFadeGeneration(audioBus);
        bool resolvedAudioLoop = separateAudioLoop ? audioLoop : isLooping;
        StartCoroutine(PlayFirstMediaRoutine(isStaticImage, clip, image, isLooping, bgmClip, audioBus, resolvedAudioLoop));
    }

    private IEnumerator PlayFirstMediaRoutine(bool isStaticImage, VideoClip clip, Texture2D image, bool isLooping, AudioClip bgmClip, StoryAudioBus audioBus, bool audioLoop)
    {
        IsTransitioning = true;
        isPlayerA_Active = true;
        ClearRenderTexture(texA);
        ClearRenderTexture(texB);

        BindTransitionTextures();
        SetTransitionFloat("_Transition", 1f);

        if (audioBus == StoryAudioBus.BGM && currentBgmSource != null)
        {
            StopBgmExcept(currentBgmSource);
            bool keepSameClip = bgmClip != null && currentBgmSource.isPlaying && currentBgmSource.clip == bgmClip;
            if (keepSameClip)
            {
                currentBgmSource.loop = audioLoop;
            }
            else
            {
                currentBgmSource.Stop();
                if (bgmClip != null)
                {
                    AudioOptions.ApplyAll();
                    currentBgmSource.clip = bgmClip;
                    currentBgmSource.loop = audioLoop;
                    currentBgmSource.volume = 1f;
                    currentBgmSource.Play();
                }
            }
        }
        else if (audioBus != StoryAudioBus.BGM)
        {
            PlayCategoryClip(audioBus, bgmClip, audioLoop);
        }

        if (isStaticImage)
        {
            playerA.Stop();
            DrawImageToRenderTexture(image, texA);
            yield return null;
        }
        else
        {
            SetupVideoPlayer(playerA, texA, clip, isLooping);
            yield return PrepareVideoAudio(playerA);
            PlayWithVolume(playerA);
            playerA.playbackSpeed = playbackSpeed;
            yield return WaitAndPreRender(playerA, texA);
        }

        float time = 0;
        float fadeInDuration = 0.5f;
        while (time < fadeInDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / fadeInDuration);
            SetTransitionFloat("_Transition", Mathf.Lerp(1f, 0f, t));
            if (GetTransitionFloat("_GlobalAlpha") < 1.0f) SetTransitionFloat("_GlobalAlpha", Mathf.Lerp(0f, 1f, t));
            yield return null;
        }

        SetTransitionFloat("_Transition", 0f);
        SetTransitionFloat("_GlobalAlpha", 1.0f);
        IsTransitioning = false;
    }

    /// <summary>
    /// 次のメディアを待機側へ用意し、指定時間でクロスフェードする。遷移中は無視する。
    /// audioFadeDuration が 0 以上のときは、そのバスの音量だけその秒数で切り替える。負の値は映像と同じ秒数。
    /// </summary>
    public void RequestPlayNextMedia(bool isStaticImage, VideoClip nextClip, Texture2D nextImage, bool isLooping, AudioClip nextBgm, float fadeDuration, StoryAudioBus audioBus = StoryAudioBus.BGM, bool separateAudioLoop = false, bool audioLoop = false, float audioFadeDuration = -1f)
    {
        if (IsTransitioning) return;
        NextFadeGeneration(audioBus);
        bool resolvedAudioLoop = separateAudioLoop ? audioLoop : isLooping;
        StartCoroutine(TransitionRoutine(isStaticImage, nextClip, nextImage, isLooping, nextBgm, fadeDuration, audioBus, resolvedAudioLoop, audioFadeDuration));
    }

    private IEnumerator TransitionRoutine(bool isStaticImage, VideoClip nextClip, Texture2D nextImage, bool isLooping, AudioClip nextBgm, float fadeDuration, StoryAudioBus audioBus, bool audioLoop, float audioFadeDuration)
    {
        IsTransitioning = true;
        VideoPlayer activePlayer = isPlayerA_Active ? playerA : playerB;
        VideoPlayer nextPlayer = isPlayerA_Active ? playerB : playerA;
        RenderTexture targetRT = isPlayerA_Active ? texB : texA;

        activePlayer.isLooping = false;
        ClearRenderTexture(targetRT);
        bool playBgm = audioBus == StoryAudioBus.BGM;
        bool keepSameBgm = playBgm && currentBgmSource.isPlaying && nextBgm != null && currentBgmSource.clip == nextBgm;
        AudioSource fadingOutBgmSource = null;
        AudioSource fadingInBgmSource = null;
        AudioSource fadingOutCategorySource = null;
        AudioSource fadingInCategorySource = null;

        if (!playBgm)
        {
            AudioSource currentCategory = CurrentSource(audioBus);
            bool keepSameCategory = nextBgm != null && currentCategory != null && currentCategory.isPlaying && currentCategory.clip == nextBgm;
            if (keepSameCategory)
            {
                currentCategory.loop = audioLoop;
            }
            else if (nextBgm != null && currentCategory != null)
            {
                fadingOutCategorySource = currentCategory;
                fadingInCategorySource = OtherSource(audioBus, currentCategory);
                if (fadingInCategorySource != null)
                {
                    fadingInCategorySource.Stop();
                    AudioOptions.ApplyAll();
                    fadingInCategorySource.clip = nextBgm;
                    fadingInCategorySource.loop = audioLoop;
                    fadingInCategorySource.volume = 0f;
                    fadingInCategorySource.Play();
                }
            }
        }
        else if (keepSameBgm) { currentBgmSource.loop = audioLoop; }
        else
        {
            fadingOutBgmSource = currentBgmSource;
            fadingInBgmSource = (currentBgmSource == bgmSource1) ? bgmSource2 : bgmSource1;
            fadingInBgmSource.Stop();
            if (nextBgm != null)
            {
                AudioOptions.ApplyAll();
                fadingInBgmSource.clip = nextBgm;
                fadingInBgmSource.loop = audioLoop;
                fadingInBgmSource.volume = 0f;
                fadingInBgmSource.Play();
            }
        }

        if (isStaticImage)
        {
            nextPlayer.Stop();
            DrawImageToRenderTexture(nextImage, targetRT);
            yield return null;
        }
        else
        {
            SetupVideoPlayer(nextPlayer, targetRT, nextClip, isLooping);
            yield return PrepareVideoAudio(nextPlayer);
            PlayWithVolume(nextPlayer);
            nextPlayer.playbackSpeed = playbackSpeed;
            yield return WaitAndPreRender(nextPlayer, targetRT);
        }

        float visualDuration = Mathf.Max(0f, fadeDuration);
        float soundDuration = audioFadeDuration < 0f ? visualDuration : Mathf.Max(0f, audioFadeDuration);
        float time = 0;
        float startValue = isPlayerA_Active ? 0f : 1f;
        float endValue = isPlayerA_Active ? 1f : 0f;
        float limit = Mathf.Max(visualDuration, soundDuration);

        while (time < limit)
        {
            time += Time.unscaledDeltaTime;
            float visualT = visualDuration > 0f ? Mathf.Clamp01(time / visualDuration) : 1f;
            float audioT = soundDuration > 0f ? Mathf.Clamp01(time / soundDuration) : 1f;
            SetTransitionFloat("_Transition", Mathf.Lerp(startValue, endValue, visualT));
            if (GetTransitionFloat("_GlobalAlpha") < 1.0f) SetTransitionFloat("_GlobalAlpha", Mathf.Lerp(0f, 1f, visualT));
            if (playBgm && !keepSameBgm) { if (nextBgm != null) fadingInBgmSource.volume = audioT; if (fadingOutBgmSource.isPlaying) fadingOutBgmSource.volume = 1f - audioT; }
            else if (fadingInCategorySource != null) { fadingInCategorySource.volume = audioT; if (fadingOutCategorySource != null && fadingOutCategorySource.isPlaying) fadingOutCategorySource.volume = 1f - audioT; }
            yield return null;
        }

        SetTransitionFloat("_Transition", endValue);
        SetTransitionFloat("_GlobalAlpha", 1.0f);
        if (playBgm && !keepSameBgm) { if (nextBgm != null) fadingInBgmSource.volume = 1f; fadingOutBgmSource.Stop(); currentBgmSource = fadingInBgmSource; }
        else if (fadingInCategorySource != null) { fadingInCategorySource.volume = 1f; if (fadingOutCategorySource != null) fadingOutCategorySource.Stop(); SetCurrentSource(audioBus, fadingInCategorySource); }

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

    /// <summary>
    /// 映像は変えず、AudioClip だけを再生する。映像の遷移中は無視する。
    /// 同じバスで別の音が鳴っているときは fadeDuration で音量を重ねる。
    /// </summary>
    public void PlayAudioOnly(AudioClip clip, bool audioLoop, float fadeDuration, StoryAudioBus audioBus)
    {
        if (clip == null || IsTransitioning) return;

        AudioSource current = CurrentSource(audioBus);
        bool crossFade = fadeDuration > 0f
            && current != null
            && current.isPlaying
            && current.clip != clip;

        if (crossFade)
        {
            int generation = NextFadeGeneration(audioBus);
            StartCoroutine(CrossFadeBusRoutine(audioBus, clip, audioLoop, fadeDuration, generation));
            return;
        }

        NextFadeGeneration(audioBus);
        PlayAttachedAudio(clip, audioLoop, audioBus);
    }

    private void PlayAttachedAudio(AudioClip clip, bool audioLoop, StoryAudioBus audioBus)
    {
        if (clip == null) return;

        if (audioBus != StoryAudioBus.BGM)
        {
            AudioSource current = CurrentSource(audioBus);
            if (current != null && current.isPlaying && current.clip == clip)
            {
                StopBusExcept(audioBus, current);
                current.loop = audioLoop;
                return;
            }

            PlayCategoryClip(audioBus, clip, audioLoop);
            return;
        }

        if (currentBgmSource == null) return;

        StopBgmExcept(currentBgmSource);
        if (currentBgmSource.isPlaying && currentBgmSource.clip == clip)
        {
            currentBgmSource.loop = audioLoop;
            return;
        }

        currentBgmSource.Stop();
        AudioOptions.ApplyAll();
        currentBgmSource.clip = clip;
        currentBgmSource.loop = audioLoop;
        currentBgmSource.volume = 1f;
        currentBgmSource.Play();
    }

    private void StopBgmExcept(AudioSource keep)
    {
        if (bgmSource1 != null && bgmSource1 != keep) bgmSource1.Stop();
        if (bgmSource2 != null && bgmSource2 != keep) bgmSource2.Stop();
    }

    private IEnumerator CrossFadeBusRoutine(StoryAudioBus bus, AudioClip clip, bool audioLoop, float fadeDuration, int generation)
    {
        AudioSource fadingOut = CurrentSource(bus);
        AudioSource fadingIn = OtherSource(bus, fadingOut);
        if (fadingIn == null || fadingOut == null) yield break;

        fadingIn.Stop();
        AudioOptions.ApplyAll();
        fadingIn.clip = clip;
        fadingIn.loop = audioLoop;
        fadingIn.volume = 0f;
        fadingIn.Play();

        float time = 0f;
        while (time < fadeDuration)
        {
            if (generation != FadeGeneration(bus)) yield break;

            time += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(time / fadeDuration) : 1f;
            fadingIn.volume = t;
            if (fadingOut.isPlaying) fadingOut.volume = 1f - t;
            yield return null;
        }

        if (generation != FadeGeneration(bus)) yield break;

        fadingIn.volume = 1f;
        fadingOut.Stop();
        SetCurrentSource(bus, fadingIn);
    }

    /// <summary>
    /// いま画面に出している側の VideoPlayer を返す。
    /// </summary>
    public VideoPlayer GetActivePlayer() { return isPlayerA_Active ? playerA : playerB; }
}