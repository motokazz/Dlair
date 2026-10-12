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

    [Header("画像設定 (※Lock画像はImageのSourceImageを使用します)")]
    [SerializeField, Tooltip("Unlock時の画像")]
    private Sprite unlockSprite;

    [Header("アニメーション設定")]
    [SerializeField] private float animationDuration = 0.6f;
    [SerializeField] private Vector3 lockScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] private Vector3 unlockScale = Vector3.one;

    [Header("イベント")]
    public UnityEvent onUnlockedEvent;
    public UnityEvent onLockedEvent;

    private Button targetButton;
    private Image targetImage;
    private Material instancedMaterial;
    private bool? previousState = null;
    private Tween progressTween;

    // ShaderのプロパティID
    private readonly int progressId = Shader.PropertyToID("_Progress");
    private readonly int unlockTexId = Shader.PropertyToID("_UnlockTex");
    // ※ _MainTexの取得は不要になりました（UIが勝手にやってくれるため）

    void Awake()
    {
        targetButton = GetComponent<Button>();
        targetImage = GetComponent<Image>();

        instancedMaterial = Instantiate(targetImage.material);
        targetImage.material = instancedMaterial;

        // 【ここがポイント】
        // Unlock用のテクスチャ「だけ」をShaderに送り込みます。
        // Lock用の画像は、Imageコンポーネントが勝手に _MainTex に送り続けてくれる仕様をそのまま利用します。
        if (unlockSprite != null) instancedMaterial.SetTexture(unlockTexId, unlockSprite.texture);
    }

    void OnEnable()
    {
        UpdateButtonState();
    }

    void OnDestroy()
    {
        progressTween?.Kill();
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
        transform.localScale = isUnlocked ? unlockScale : lockScale;
        instancedMaterial.SetFloat(progressId, isUnlocked ? 1f : 0f);
    }

    private void PlayTransitionAnimation(bool isUnlocked)
    {
        progressTween?.Kill();

        float targetProgress = isUnlocked ? 1f : 0f;
        Vector3 targetScale = isUnlocked ? unlockScale : lockScale;

        progressTween = instancedMaterial.DOFloat(targetProgress, progressId, animationDuration)
            .SetEase(Ease.InOutSine)
            .OnUpdate(() => targetImage.SetMaterialDirty())
            .OnComplete(() =>
            {
                if (isUnlocked) onUnlockedEvent?.Invoke();
                else onLockedEvent?.Invoke();
            });

        transform.DOScale(targetScale, animationDuration).SetEase(Ease.OutBack);
    }
}