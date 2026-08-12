using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // DOTween 推奨（なければLerpで代用可）
using System.Collections;

/// <summary>
/// QTE入力関連のUI表示
/// </summary>
public class QTEGuide : MonoBehaviour
{
    [Header("必須UI")]
    [SerializeField] private Image progressImage;      // 円形プログレス（Image Type: Filled, Fill Method: Radial 360）
    [SerializeField] private Image successIcon;        // 成功時のアイコン（チェックや矢印など）
    [SerializeField] private Image failIcon;           // 失敗時のアイコン（×など）
    [SerializeField] private CanvasGroup canvasGroup;  // 全体の出現/消滅用

    [Header("設定")]
    [SerializeField] private float appearDuration = 0.3f;
    [SerializeField] private float disappearDuration = 0.2f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    private float elapsed;
    private Coroutine coroutine;
    private void Awake()
    {
        Init();
    }

    public void NewQTE(float start,float duration)
    {
        if(coroutine!=null)
        {
            StopCoroutine(coroutine);
        }
        Init();
        coroutine = StartCoroutine( Wait(start,duration));
    }

    
    private IEnumerator Wait(float start,float duration)
    {
        yield return new WaitForSeconds(start);
        StartWindow(duration);
    }


    private void Init()
    {
        canvasGroup.alpha = 0f;
        successIcon.gameObject.SetActive(false);
        failIcon.gameObject.SetActive(false);
        progressImage.fillAmount = 0f;

        progressImage.DOKill();
        successIcon.DOKill();
        failIcon.DOKill();
    }



    // 入力窓開始
    public void StartWindow(float duration)
    {
        elapsed = 0f;

        // 出現アニメ
        canvasGroup.alpha = 0f;
        progressImage.color = normalColor;
        canvasGroup.DOFade(1f, appearDuration);
        
        progressImage.fillAmount = 0f;
        if(duration > 0f) progressImage.DOColor(warningColor, duration);
    }

    // 入力成功
    public void ShowSuccess()
    {
        progressImage.color = successColor;
        progressImage.fillAmount = 1f;

        successIcon.color = successColor;
        successIcon.gameObject.SetActive(true);
        successIcon.transform.DOScale(1.3f, 0.15f).SetLoops(2, LoopType.Yoyo).OnComplete(() => {
            successIcon.DOFade(0f, 0.15f);
            progressImage.DOKill();
            progressImage.DOFade(0f, 0.15f).OnComplete(()=>Init());
        });
    }

    // 入力失敗 / タイムアウト
    public void ShowFail()
    {
        progressImage.color = failColor;

        failIcon.color = failColor;
        failIcon.gameObject.SetActive(true);
        failIcon.transform.DOScale(1.3f, 0.15f).SetLoops(2, LoopType.Yoyo).OnComplete(() => {
            failIcon.DOFade(0f, 0.15f);
            progressImage.DOKill();
            progressImage.DOFade(0f, 0.15f).OnComplete(() => Init());
        });

    }

    // 失敗でも成功でも無い状態
    public void Vanish()
    {
        progressImage.DOKill();
        successIcon.DOKill();
        failIcon.DOKill();
        canvasGroup.DOFade(0f, 0.15f);
    }
}