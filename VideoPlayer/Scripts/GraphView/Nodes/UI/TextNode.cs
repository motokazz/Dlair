using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class TextNode : BaseNode
{
    [Header("表示するUIプレハブ")]
    public GameObject textUIPrefab;

    [Header("本文")]
    [TextArea(3, 8)]
    public string message = "";

    [Header("オプション")]
    [Tooltip("オン: 全文表示後のクリックで Next へ進む / オフ: 表示開始と同時に Next へ進む")]
    public bool waitUntilComplete = true;

    [Tooltip("待ちありの場合、クリックで進んだあとにUIを破棄する")]
    public bool autoDestroyUI = true;

    [Tooltip("入力中のクリックで全文を出して打ちを飛ばす。進むのは全文表示後のクリック")]
    public bool clickToSkip = true;

    [NonSerialized]
    private GameObject currentUIInstance;

    [NonSerialized]
    private bool hasAdvanced;

    public override void Execute(StoryPlayer player)
    {
        hasAdvanced = false;

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
        currentUIInstance.SetActive(true);

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
                typewriter.Play(message ?? string.Empty);
                Advance(player);
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
            Advance(player);
        }
    }

    public string GetDisplayTitle()
    {
        if (string.IsNullOrWhiteSpace(message)) return "Text";

        string oneLine = message.Replace("\r\n", " ").Replace('\n', ' ').Trim();
        return oneLine.Length > 18 ? oneLine.Substring(0, 18) + "…" : oneLine;
    }

    private void Advance(StoryPlayer player)
    {
        if (hasAdvanced) return;
        hasAdvanced = true;

        if (autoDestroyUI && waitUntilComplete && currentUIInstance != null)
        {
            player.UnregisterSpawned(currentUIInstance);
            UnityEngine.Object.Destroy(currentUIInstance);
            currentUIInstance = null;
        }

        player.ContinueTo(this, "Next");
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
