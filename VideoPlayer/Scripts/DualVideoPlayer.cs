using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.InputSystem;

public class DualVideoPlayer : MonoBehaviour
{
    [Header("Video Players")]
    public VideoPlayer playerA;
    public VideoPlayer playerB;

    [Header("Render Textures")]
    public RenderTexture texA;
    public RenderTexture texB;

    [Header("Transition Material")]
    public Material transitionMaterial;

    [Header("Test Clips")]
    public VideoClip firstClip;
    public VideoClip secondClip;

    [Header("Settings")]
    public float transitionDuration = 1.0f;

    private bool isPlayerA_Active = true;
    private bool isTransitioning = false;

    // ★ 重複実行を防ぐための重要なロックフラグ
    private bool isWaitingForFirstFrame = false;
    private bool triggerTransitionRequested = false;

    private void Start()
    {
        ConfigurePlayerEvents(playerA);
        ConfigurePlayerEvents(playerB);

        // テクスチャの割り当ては「永遠に固定」。A=Main, B=Sub
        transitionMaterial.SetTexture("_MainTex", texA);
        transitionMaterial.SetTexture("_SubTex", texB);
        // 0.0 は Aの映像を表示する状態
        transitionMaterial.SetFloat("_Transition", 0f);

        if (firstClip != null)
        {
            playerA.clip = firstClip;
            playerA.Play();
        }
    }

    private void ConfigurePlayerEvents(VideoPlayer player)
    {
        player.sendFrameReadyEvents = true;
        player.prepareCompleted += OnVideoPrepared;
        player.frameReady += OnFrameReady;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (!isTransitioning && secondClip != null)
            {
                RequestPlayNextVideo(secondClip);
            }
        }

        // メインスレッド(Update)で1回だけコルーチンを起動する
        if (triggerTransitionRequested)
        {
            triggerTransitionRequested = false;
            StartCoroutine(TransitionRoutine());
        }
    }

    public void RequestPlayNextVideo(VideoClip nextClip)
    {
        isTransitioning = true;
        isWaitingForFirstFrame = true; // ★ 次の映像の準備待ちを開始

        VideoPlayer nextPlayer = isPlayerA_Active ? playerB : playerA;

        // ゴミフレームが残らないように黒で塗りつぶす
        RenderTexture targetRT = isPlayerA_Active ? texB : texA;
        Graphics.Blit(Texture2D.blackTexture, targetRT);

        nextPlayer.clip = nextClip;
        nextPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        source.Play();
        source.Pause();
    }

    private void OnFrameReady(VideoPlayer source, long frameIdx)
    {
        VideoPlayer nextPlayer = isPlayerA_Active ? playerB : playerA;

        // ★ 「最初のフレーム待ち状態」のとき、1回だけしかここを通さない
        if (isWaitingForFirstFrame && source == nextPlayer && frameIdx >= 0)
        {
            isWaitingForFirstFrame = false; // ★ 即座にロックを閉じる
            triggerTransitionRequested = true; // Updateに開始を依頼
        }
    }

    private IEnumerator TransitionRoutine()
    {
        VideoPlayer activePlayer = isPlayerA_Active ? playerA : playerB;
        VideoPlayer nextPlayer = isPlayerA_Active ? playerB : playerA;

        nextPlayer.Play();

        float time = 0;

        // ★ Ping-Pong方式: A->Bへの遷移なら 0->1、B->Aへの遷移なら 1->0 に動かす
        float startValue = isPlayerA_Active ? 0f : 1f;
        float endValue = isPlayerA_Active ? 1f : 0f;

        while (time < transitionDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / transitionDuration);

            float currentValue = Mathf.Lerp(startValue, endValue, t);
            transitionMaterial.SetFloat("_Transition", currentValue);
            yield return null;
        }

        // 確実に目標値に着地させる
        transitionMaterial.SetFloat("_Transition", endValue);

        // 古い動画を停止
        activePlayer.Stop();

        // アクティブ状態を反転
        isPlayerA_Active = !isPlayerA_Active;
        isTransitioning = false;

        // ★ トランジション完了後、テクスチャの入れ替え等のリセット処理は「一切不要」。
        // 次回は 1->0 （または 0->1）に向かって Transition 値が動くだけなのでチラつきません。
    }

    private void OnDestroy()
    {
        if (playerA != null) { playerA.prepareCompleted -= OnVideoPrepared; playerA.frameReady -= OnFrameReady; }
        if (playerB != null) { playerB.prepareCompleted -= OnVideoPrepared; playerB.frameReady -= OnFrameReady; }
    }
}