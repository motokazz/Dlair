using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;

[RequireComponent(typeof(Button), typeof(Image))]
public class ShaderLockSpriteButton : MonoBehaviour
{
    [Header("データベースとキーの設定")]
    [SerializeField] private StoryVariableDatabase variableDatabase;
    [SerializeField] private string targetKey = "UnlockFlag";
    [SerializeField] private bool invertCondition = false;

    [Header("画像 (Sprite) 設定")]
    [SerializeField] private Sprite unlockSprite;
    [SerializeField] private Sprite lockSprite;

    [Header("アニメーション設定")]
    [SerializeField, Tooltip("フラッシュして切り替わるまでの全体時間")]
    private float animationDuration = 0.6f;

    [Header("スケール設定")]
    [SerializeField] private Vector3 unlockScale = Vector3.one;
    [SerializeField] private Vector3 lockScale = new Vector3(0.9f, 0.9f, 1f);

    [Header("イベント")]
    public UnityEvent onUnlockedEvent;
    public UnityEvent onLockedEvent;

    private Button targetButton;
    private Image targetImage;
    private Material instancedMaterial; // マテリアルのインスタンス
    private bool? previousState = null;
    private Sequence animSequence;

    // ShaderのプロパティID
    private readonly int flashAmountPropertyId = Shader.PropertyToID("_FlashAmount");

    void Awake()
    {
        targetButton = GetComponent<Button>();
        targetImage = GetComponent<Image>();

        // マテリアルをインスタンス化（他のボタンと連動しないように個別のマテリアルにする）
        instancedMaterial = Instantiate(targetImage.material);
        targetImage.material = instancedMaterial;

        // 初期状態はフラッシュ0
        instancedMaterial.SetFloat(flashAmountPropertyId, 0f);
    }

    void OnEnable()
    {
        UpdateButtonState();
    }

    void OnDestroy()
    {
        animSequence?.Kill();
        if (instancedMaterial != null) Destroy(instancedMaterial);
    }

    public void UpdateButtonState()
    {
        if (variableDatabase == null) return;

        StoryVariableDefinition definition = variableDatabase.GetDefinition(targetKey);
        bool newState = false;

        if (definition != null && definition.type == StoryVariableType.Bool)
        {
            newState = definition.defaultBoolValue;
            if (invertCondition) newState = !newState;
        }

        if (!previousState.HasValue)
        {
            ApplyStateImmediately(newState);
        }
        else if (previousState.Value != newState)
        {
            targetButton.interactable = newState;
            PlayTransitionAnimation(newState);
        }

        previousState = newState;
    }

    private void ApplyStateImmediately(bool isUnlocked)
    {
        targetButton.interactable = isUnlocked;
        targetImage.sprite = isUnlocked ? unlockSprite : lockSprite;
        transform.localScale = isUnlocked ? unlockScale : lockScale;
        instancedMaterial.SetFloat(flashAmountPropertyId, 0f);
    }

    private void PlayTransitionAnimation(bool isUnlocked)
    {
        animSequence?.Kill();
        animSequence = DOTween.Sequence();

        // 半分の時間でフラッシュを最大にし、残り半分で戻す
        float halfDuration = animationDuration * 0.5f;

        if (isUnlocked)
        {
            // --- Lock → Unlock ---
            // 1. マテリアルを光らせる ＆ 少し大きくする
            animSequence.Append(instancedMaterial.DOFloat(1f, flashAmountPropertyId, halfDuration).SetEase(Ease.OutSine));
            animSequence.Join(transform.DOScale(unlockScale * 1.1f, halfDuration).SetEase(Ease.OutSine));

            // 2. フラッシュがピーク（真っ白）の瞬間に、画像をUnlockに差し替える
            animSequence.AppendCallback(() =>
            {
                targetImage.sprite = unlockSprite;
            });

            // 3. フラッシュを0に戻す ＆ 大きさを元に戻す
            animSequence.Append(instancedMaterial.DOFloat(0f, flashAmountPropertyId, halfDuration).SetEase(Ease.InSine));
            animSequence.Join(transform.DOScale(unlockScale, halfDuration).SetEase(Ease.OutBack));

            animSequence.OnComplete(() => onUnlockedEvent?.Invoke());
        }
        else
        {
            // --- Unlock → Lock ---
            // 1. マテリアルを光らせる ＆ 小さくする
            animSequence.Append(instancedMaterial.DOFloat(1f, flashAmountPropertyId, halfDuration).SetEase(Ease.OutSine));
            animSequence.Join(transform.DOScale(lockScale, halfDuration).SetEase(Ease.OutSine));

            // 2. ピークで画像をLockに差し替える
            animSequence.AppendCallback(() =>
            {
                targetImage.sprite = lockSprite;
            });

            // 3. フラッシュを0に戻す
            animSequence.Append(instancedMaterial.DOFloat(0f, flashAmountPropertyId, halfDuration).SetEase(Ease.InSine));

            animSequence.OnComplete(() => onLockedEvent?.Invoke());
        }
    }
}