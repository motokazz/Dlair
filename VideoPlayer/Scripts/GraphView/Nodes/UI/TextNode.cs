using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TextNode : BaseNode
{
    [Header("表示するUIプレハブ")]
    public GameObject textUIPrefab;

    [Header("本文")]
    [TextArea(3, 8)]
    public string message = "";

    [Header("見た目")]
    public Color fontColor = Color.white;
    public float fadeInDuration = 0f;
    public float fadeOutDuration = 0f;

    [Header("オプション")]
    [Tooltip("オン: 全文表示後のクリックから Next までの待ちを開始 / オフ: Typewriter 終了後に待ちを開始")]
    public bool waitUntilComplete = true;

    [Tooltip("Next に進むのと同時にUIを破棄します")]
    public bool autoDestroyUI = true;

    [Tooltip("Next に進むまでの時間（秒）。フェードアウトはこの時点から逆算して始まります")]
    public float destroyDelay = 0f;

    [Tooltip("入力中のクリックで全文を出して打ちを飛ばす。進むのは全文表示後のクリック")]
    public bool clickToSkip = true;

    [NonSerialized]
    private GameObject currentUIInstance;

    [NonSerialized]
    private bool hasAdvanced;

    [NonSerialized]
    private StoryPlayer currentPlayer;

    [NonSerialized]
    private Coroutine lifetimeRoutine;

    [NonSerialized]
    private Coroutine fadeInRoutine;

    [NonSerialized]
    private Coroutine continueRoutine;

    public override void Execute(StoryPlayer player)
    {
        StopLifetime(player);
        hasAdvanced = false;
        currentPlayer = player;
        currentUIInstance = null;

        Debug.Log($"【Text Node】テキストを表示します: {(textUIPrefab != null ? textUIPrefab.name : "None")}");

        if (textUIPrefab == null)
        {
            Debug.LogError("【Text Node】UIプレハブが設定されていません！");
            Advance(player);
            return;
        }

        Canvas parentCanvas = player.GetUICanvas();
        if (parentCanvas == null)
        {
            Debug.LogError("【Text Node】UIを表示するためのCanvasがシーン内に見つかりません！");
            Advance(player);
            return;
        }

        currentUIInstance = player.SpawnPrefab(textUIPrefab, parentCanvas.transform);
        player.ProtectSpawned(currentUIInstance);
        ApplyFontColor(currentUIInstance);
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(currentUIInstance);
        canvasGroup.alpha = fadeInDuration > 0f ? 0f : 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        currentUIInstance.SetActive(true);

        if (fadeInDuration > 0f)
        {
            fadeInRoutine = player.StartCoroutine(FadeCanvas(currentUIInstance, 0f, 1f, fadeInDuration));
        }

        TypewriterEffect typewriter = currentUIInstance.GetComponentInChildren<TypewriterEffect>(true);
        if (typewriter != null)
        {
            if (waitUntilComplete)
            {
                BindClick(currentUIInstance, typewriter, player);
                BindClick(typewriter.gameObject, typewriter, player);
                typewriter.Play(message ?? string.Empty);
            }
            else
            {
                typewriter.Play(message ?? string.Empty, () => FinishDisplay(player));
            }
            return;
        }

        TMP_Text tmp = currentUIInstance.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = message ?? string.Empty;
            Debug.LogWarning("【Text Node】TypewriterEffect がないため、本文を一気に表示します。");
        }
        else
        {
            Debug.LogWarning("【Text Node】プレハブ内に TypewriterEffect / TextMeshPro が見つかりません。");
        }

        if (waitUntilComplete)
        {
            BindClick(currentUIInstance, null, player);
        }
        else
        {
            continueRoutine = player.StartCoroutine(FinishAfterShow(player));
        }
    }

    public string GetDisplayTitle()
    {
        if (string.IsNullOrWhiteSpace(message)) return "Text";

        string oneLine = message.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        return oneLine.Length > 18 ? oneLine.Substring(0, 18) + "…" : oneLine;
    }

    private IEnumerator FinishAfterShow(StoryPlayer player)
    {
        yield return null;
        continueRoutine = null;
        FinishDisplay(player);
    }

    private void FinishDisplay(StoryPlayer player)
    {
        if (hasAdvanced) return;
        hasAdvanced = true;
        lifetimeRoutine = player.StartCoroutine(DestroyThenContinue(player));
    }

    private void Advance(StoryPlayer player)
    {
        FinishDisplay(player);
    }

    private IEnumerator DestroyThenContinue(StoryPlayer player)
    {
        GameObject instance = currentUIInstance;
        float nextAt = Mathf.Max(0f, destroyDelay);
        float fadeOut = autoDestroyUI ? Mathf.Max(0f, fadeOutDuration) : 0f;
        float fadeStart = Mathf.Max(0f, nextAt - fadeOut);

        float remaining = nextAt - fadeStart;

        if (fadeStart > 0f)
        {
            yield return new WaitForSeconds(fadeStart);
        }

        if (autoDestroyUI && instance != null && fadeOut > 0f && remaining > 0f)
        {
            if (fadeInRoutine != null && player != null)
            {
                player.StopCoroutine(fadeInRoutine);
                fadeInRoutine = null;
            }

            yield return FadeCanvas(instance, GetCanvasAlpha(instance), 0f, remaining);
        }
        else if (remaining > 0f)
        {
            yield return new WaitForSeconds(remaining);
        }

        if (autoDestroyUI)
        {
            DestroyInstance(player, instance);
        }

        player.ContinueTo(this, "Next");
        lifetimeRoutine = null;
    }

    private void DestroyInstance(StoryPlayer player, GameObject instance)
    {
        if (fadeInRoutine != null && player != null)
        {
            player.StopCoroutine(fadeInRoutine);
            fadeInRoutine = null;
        }

        if (instance == null) return;

        if (currentUIInstance == instance)
        {
            currentUIInstance = null;
        }

        player.UnregisterSpawned(instance);
        UnityEngine.Object.Destroy(instance);
    }

    private void StopLifetime(StoryPlayer player)
    {
        if (lifetimeRoutine != null && currentPlayer != null)
        {
            currentPlayer.StopCoroutine(lifetimeRoutine);
        }
        else if (lifetimeRoutine != null && player != null)
        {
            player.StopCoroutine(lifetimeRoutine);
        }

        if (fadeInRoutine != null && currentPlayer != null)
        {
            currentPlayer.StopCoroutine(fadeInRoutine);
        }
        else if (fadeInRoutine != null && player != null)
        {
            player.StopCoroutine(fadeInRoutine);
        }

        if (continueRoutine != null && currentPlayer != null)
        {
            currentPlayer.StopCoroutine(continueRoutine);
        }
        else if (continueRoutine != null && player != null)
        {
            player.StopCoroutine(continueRoutine);
        }

        lifetimeRoutine = null;
        fadeInRoutine = null;
        continueRoutine = null;
    }

    private void ApplyFontColor(GameObject root)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            texts[i].color = fontColor;
        }
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject root)
    {
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = root.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static float GetCanvasAlpha(GameObject root)
    {
        if (root == null) return 1f;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        return group != null ? group.alpha : 1f;
    }

    private static IEnumerator FadeCanvas(GameObject root, float from, float to, float duration)
    {
        if (root == null) yield break;

        CanvasGroup group = GetOrAddCanvasGroup(root);
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        group.alpha = from;
        if (to <= 0f)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        float time = 0f;
        while (time < duration)
        {
            if (root == null) yield break;
            time += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(time / duration));
            yield return null;
        }

        if (root == null) yield break;
        group.alpha = to;
    }

    private void BindClick(GameObject target, TypewriterEffect typewriter, StoryPlayer player)
    {
        if (target == null) return;

        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = target.AddComponent<EventTrigger>();
        }

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        entry.callback.AddListener(_ => OnClicked(typewriter, player));
        trigger.triggers.Add(entry);
    }

    private void OnClicked(TypewriterEffect typewriter, StoryPlayer player)
    {
        if (hasAdvanced) return;

        if (typewriter != null && typewriter.IsPlaying)
        {
            if (clickToSkip)
            {
                typewriter.Skip();
            }
            return;
        }

        Advance(player);
    }
}
