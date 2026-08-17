using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TypewriterEffect : MonoBehaviour
{
    [Tooltip("1文字が表示されるスピード（秒）")]
    public float typeSpeed = 0.05f;

    [Tooltip("文字表示が完了した時に発火するイベント")]
    public UnityEvent onComplete;

    private TextMeshProUGUI uiText;
    private Coroutine typingCoroutine;
    private string currentFullText;
    private Action currentCallback;

    // ==========================================
    // ★追加：コンポーネントが取得できているか確実にチェックする処理
    // ==========================================
    private void EnsureSetup()
    {
        if (uiText == null)
        {
            uiText = GetComponent<TextMeshProUGUI>();
        }
    }

    public void Play(string message, Action onCompleteCallback = null)
    {
        EnsureSetup(); // ★ 使う直前に確実に準備させる！

        currentFullText = message;
        currentCallback = onCompleteCallback;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);

        gameObject.SetActive(true);
        uiText.text = ""; // 準備できているので、絶対にエラーにならない

        typingCoroutine = StartCoroutine(TypeTextRoutine());
    }

    public void Skip()
    {
        EnsureSetup();

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
            uiText.text = currentFullText; // 一気に全表示
            Complete();
        }
    }

    private IEnumerator TypeTextRoutine()
    {
        foreach (char c in currentFullText)
        {
            uiText.text += c;
            yield return new WaitForSeconds(typeSpeed);
        }
        typingCoroutine = null;
        Complete();
    }

    private void Complete()
    {
        onComplete?.Invoke();
        currentCallback?.Invoke();
        currentCallback = null;
    }
}