using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Sprites;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lock 判定の参照先。変数か、再生記録か。
/// </summary>
public enum StoryLockSource
{
    [InspectorName("変数")]
    Variable,
    [InspectorName("再生記録")]
    Visit
}

/// <summary>
/// 追加の Unlock 条件。変数か、再生記録か。
/// </summary>
[Serializable]
public class StoryLockStage
{
    public StoryLockSource source = StoryLockSource.Variable;
    public StoryVariableDatabase variableDatabase;
    public string targetKey = "";
    public StoryGraph visitGraph;
    public string visitLabelGuid;
    public bool invertCondition;
    public Sprite reachedSprite;
}

/// <summary>
/// 変数の現在値、または StoryGotoUnlocks.json の再生記録を見てボタンを Lock する。
/// 追加の段階があるときは、すべての段階が Unlock のときだけ Unlock する。
/// 説明は StoryVariableLockButton.Summary。
/// </summary>
[MovedFrom(false, sourceClassName: "ShaderLockSpriteButton")]
[RequireComponent(typeof(Button), typeof(Image))]
public class StoryVariableLockButton : MonoBehaviour
{
    public const string Summary =
        "変数の現在値、または StoryGotoUnlocks.json の再生記録を見てボタンを Lock します。\n" +
        "変数は StoryVariableDatabase の一覧から選びます。Bool は true のとき Unlock です。Int と Float は 0 より大きいとき Unlock です。\n" +
        "再生記録は StoryGraph とその Label から選びます。そのグラフのアセット名とラベル名の組が StoryGotoUnlocks.json にあるとき Unlock です。\n" +
        "実行中に値や再生記録が変わると、Lock 状態も追従します。まだ実行中の値が無い変数は、データベースの初期値を使います。再生記録は、記録が無いとき Lock です。\n" +
        "条件を反転すると Unlock と Lock が入れ替わります。\n" +
        "ゲーム実行後、そのボタンが最初に表示されて Unlock のときだけ、アンロックアニメーションを一度再生します。同じ実行中にもう一度表示しても再生しません。\n" +
        "マテリアル、Lock 画像、Unlock 画像はこのコンポーネントに登録します。Image の Source Image と Material はここから設定されます。\n" +
        "Unlock 画像は、Lock 画像とは別の切り出しでも、その Sprite の範囲を表示します。\n" +
        "追加の段階を置くと、1段階目と追加の段階がすべて Unlock のときだけボタンを押せます。各段階は変数か再生記録です。\n" +
        "追加が無いときは、これまでと同じく 1段階目だけで判定し、Lock 画像と Unlock 画像を使います。\n" +
        "追加の段階には、その手前まで進んだときの画像を登録します。何も満たしていないときは Lock 画像、途中はその画像、全部揃ったときは Unlock 画像です。途中ではボタンは押せません。\n" +
        "表示中に段階が進むと、今の画像から次の画像へ切り替わります。いったん消して再表示したときは、その段階の画像をすぐ出します。";

    [SerializeField] private StoryLockSource lockSource = StoryLockSource.Variable;

    [Header("データベースとキーの設定")]
    [SerializeField] private StoryVariableDatabase variableDatabase;
    [SerializeField] private string targetKey = "UnlockFlag";

    [Header("再生記録")]
    [SerializeField] private StoryGraph visitGraph;
    [SerializeField] private string visitLabelGuid;

    [SerializeField] private bool invertCondition = false;

    [Tooltip("1段階目に加えて見る条件。すべて Unlock のときだけボタンを Unlock にします。")]
    [SerializeField] private List<StoryLockStage> extraStages = new List<StoryLockStage>();

    [Header("見た目")]
    [SerializeField] private Material material;
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Sprite unlockSprite;

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
    private int previousReached = -1;
    private Sprite shownSprite;
    private bool shownOnce;
    private Tween progressTween;
    private Tween scaleTween;

    private readonly int progressId = Shader.PropertyToID("_Progress");
    private readonly int mainTexId = Shader.PropertyToID("_MainTex");
    private readonly int unlockTexId = Shader.PropertyToID("_UnlockTex");
    private readonly int unlockTilingId = Shader.PropertyToID("_UnlockTiling");
    private readonly int unlockOffsetId = Shader.PropertyToID("_UnlockOffset");

    void Awake()
    {
        targetButton = GetComponent<Button>();
        targetImage = GetComponent<Image>();
        ApplyAppearance();
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        Image image = GetComponent<Image>();
        if (image == null || lockSprite == null || image.sprite == lockSprite) return;
        image.sprite = lockSprite;
    }

    private void ApplyAppearance()
    {
        if (targetImage == null) return;

        if (lockSprite != null)
            targetImage.sprite = lockSprite;

        if (material == null) return;

        instancedMaterial = Instantiate(material);
        PrepareForUi(instancedMaterial);
        ApplyTextures();
        targetImage.material = instancedMaterial;
    }

    private static void PrepareForUi(Material target)
    {
        SetFloatIfPresent(target, "_Surface", 1f);
        SetFloatIfPresent(target, "_ZWrite", 0f);
        SetFloatIfPresent(target, "_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        SetFloatIfPresent(target, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(target, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(target, "_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        SetFloatIfPresent(target, "_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        target.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        target.SetOverrideTag("RenderType", "Transparent");
        target.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetFloatIfPresent(Material target, string name, float value)
    {
        if (target.HasProperty(name))
            target.SetFloat(name, value);
    }

    private void ApplyTextures()
    {
        if (instancedMaterial == null) return;

        Sprite locked = lockSprite != null ? lockSprite : targetImage.sprite;
        if (locked != null)
            instancedMaterial.SetTexture(mainTexId, locked.texture);
        if (unlockSprite != null)
            instancedMaterial.SetTexture(unlockTexId, unlockSprite.texture);
        ApplyUnlockUv(locked, unlockSprite);
    }

    /// <summary>
    /// Image の UV は表示中の画像の範囲。次の画像はその Sprite 自身の範囲で読む。
    /// </summary>
    private void ApplyUnlockUv(Sprite source, Sprite targetSprite)
    {
        Vector2 tiling = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (source != null && targetSprite != null && source.texture != null && targetSprite.texture != null)
        {
            Vector4 lockUv = DataUtility.GetOuterUV(source);
            Vector4 unlockUv = DataUtility.GetOuterUV(targetSprite);
            Vector2 lockSize = new Vector2(lockUv.z - lockUv.x, lockUv.w - lockUv.y);
            Vector2 unlockSize = new Vector2(unlockUv.z - unlockUv.x, unlockUv.w - unlockUv.y);
            tiling = new Vector2(
                lockSize.x > 0.0001f ? unlockSize.x / lockSize.x : 1f,
                lockSize.y > 0.0001f ? unlockSize.y / lockSize.y : 1f);
            offset = new Vector2(
                unlockUv.x - lockUv.x * tiling.x,
                unlockUv.y - lockUv.y * tiling.y);
        }

        if (instancedMaterial.HasProperty(unlockTilingId))
            instancedMaterial.SetVector(unlockTilingId, new Vector4(tiling.x, tiling.y, 0f, 0f));
        if (instancedMaterial.HasProperty(unlockOffsetId))
            instancedMaterial.SetVector(unlockOffsetId, new Vector4(offset.x, offset.y, 0f, 0f));
    }

    void OnEnable()
    {
        GameManager.OnVariableChanged += HandleVariableChanged;
        StoryVisitLog.OnChanged += HandleVisitChanged;
        ApplyTextures();
        if (shownOnce)
            ApplyShownState();
        else
            UpdateButtonState();
        shownOnce = true;
    }

    void OnDisable()
    {
        GameManager.OnVariableChanged -= HandleVariableChanged;
        StoryVisitLog.OnChanged -= HandleVisitChanged;
    }

    void LateUpdate()
    {
        // 初期化やセーブ消去は変更イベントを出さないため、表示中は現在値を見続ける。
        UpdateButtonState();
    }

    void OnDestroy()
    {
        progressTween?.Kill();
        scaleTween?.Kill();
        if (instancedMaterial != null) Destroy(instancedMaterial);
    }

    /// <summary>
    /// 変数か再生記録から Lock 状態を決め、変化があればアニメーションする。
    /// 実行後の最初の表示で Unlock のときだけ、アンロックアニメーションを一度再生する。
    /// </summary>
    public void UpdateButtonState()
    {
        if (targetButton == null) return;
        if (SkipUntilConfigured()) return;
        if (HasExtraStage())
        {
            if (Application.isPlaying)
                UpdateStagedState(true);
            return;
        }

        bool newState = CurrentUnlocked();

        if (!previousState.HasValue)
        {
            if (Application.isPlaying && newState)
            {
                ApplyStateImmediately(false);
                targetButton.interactable = true;
                PlayTransitionAnimation(true);
            }
            else
            {
                ApplyStateImmediately(newState);
            }
        }
        else if (previousState.Value != newState)
        {
            targetButton.interactable = newState;
            PlayTransitionAnimation(newState);
        }

        previousState = newState;
    }

    /// <summary>
    /// 再表示ではアニメーションを再生せず、いまの Lock 状態をすぐ出す。
    /// </summary>
    private void ApplyShownState()
    {
        if (targetButton == null) return;
        if (SkipUntilConfigured()) return;
        if (HasExtraStage())
        {
            if (Application.isPlaying)
                UpdateStagedState(false);
            return;
        }

        bool newState = CurrentUnlocked();
        ApplyStateImmediately(newState);
        previousState = newState;
    }

    /// <summary>
    /// 進んだ段階の画像を出す。ボタンを押せるのは、全部揃ったときだけ。
    /// 表示中に段階が変わったときだけ、今の画像から次の画像へ切り替える。
    /// </summary>
    private void UpdateStagedState(bool allowAnimation)
    {
        int total = StageCount();
        int reached = CountReached();
        bool unlocked = reached >= total;
        Sprite sprite = SpriteForReached(reached, total);
        bool first = previousReached < 0;

        if (allowAnimation && !first && previousReached == reached)
            return;

        if (!allowAnimation || !Application.isPlaying)
        {
            ApplyStagedImmediate(sprite, unlocked);
        }
        else if (first && unlocked)
        {
            ApplyStagedImmediate(lockSprite != null ? lockSprite : sprite, false);
            targetButton.interactable = true;
            PlaySpriteTransition(sprite, true);
        }
        else if (first)
        {
            ApplyStagedImmediate(sprite, unlocked);
        }
        else
        {
            targetButton.interactable = unlocked;
            PlaySpriteTransition(sprite, unlocked);
        }

        previousReached = reached;
        previousState = unlocked;
    }

    private int StageCount()
    {
        return 1 + (extraStages != null ? extraStages.Count : 0);
    }

    /// <summary>
    /// 先頭から連続して満たした段階の数。0 は Lock のまま。
    /// </summary>
    public int ReachedStageCount()
    {
        return CountReached();
    }

    /// <summary>
    /// 先頭から連続して満たした段階の数。
    /// </summary>
    private int CountReached()
    {
        if (!StageUnlocked(lockSource, variableDatabase, targetKey, visitGraph, visitLabelGuid, invertCondition))
            return 0;

        int reached = 1;
        if (extraStages == null) return reached;

        for (int i = 0; i < extraStages.Count; i++)
        {
            StoryLockStage stage = extraStages[i];
            if (stage == null) return reached;
            if (!StageUnlocked(stage.source, stage.variableDatabase, stage.targetKey, stage.visitGraph, stage.visitLabelGuid, stage.invertCondition))
                return reached;
            reached++;
        }

        return reached;
    }

    /// <summary>
    /// 0 は Lock 画像、最後まで進んだときは Unlock 画像、途中はその段階の画像。
    /// </summary>
    private Sprite SpriteForReached(int reached, int total)
    {
        if (reached >= total)
            return unlockSprite != null ? unlockSprite : lockSprite;
        if (reached <= 0 || extraStages == null)
            return lockSprite;

        for (int level = reached; level >= 1; level--)
        {
            int index = level - 1;
            if (index >= extraStages.Count) continue;
            StoryLockStage stage = extraStages[index];
            if (stage != null && stage.reachedSprite != null)
                return stage.reachedSprite;
        }

        return lockSprite;
    }

    private void ApplyStagedImmediate(Sprite sprite, bool unlocked)
    {
        progressTween?.Kill();
        scaleTween?.Kill();
        targetButton.interactable = unlocked;
        transform.localScale = unlocked ? unlockScale : lockScale;
        ShowSprite(sprite);
    }

    private void ShowSprite(Sprite sprite)
    {
        if (sprite == null) sprite = lockSprite;
        shownSprite = sprite;
        if (targetImage != null && sprite != null)
            targetImage.sprite = sprite;

        if (instancedMaterial == null || sprite == null || sprite.texture == null) return;
        instancedMaterial.SetTexture(mainTexId, sprite.texture);
        SetProgress(0f);
    }

    /// <summary>
    /// 今出している画像から、次の段階の画像へ切り替える。
    /// </summary>
    private void PlaySpriteTransition(Sprite to, bool unlocked)
    {
        if (to == null) to = lockSprite;
        Sprite from = shownSprite != null ? shownSprite : lockSprite;
        bool unlockChanged = !previousState.HasValue ? unlocked : previousState.Value != unlocked;
        bool canFade = from != null && to != null && from != to
            && instancedMaterial != null && instancedMaterial.HasProperty(progressId);

        if (!canFade)
        {
            ApplyStagedImmediate(to, unlocked);
            if (unlockChanged) InvokeStateEvent(unlocked);
            return;
        }

        progressTween?.Kill();
        scaleTween?.Kill();

        if (targetImage != null)
            targetImage.sprite = from;
        instancedMaterial.SetTexture(mainTexId, from.texture);
        instancedMaterial.SetTexture(unlockTexId, to.texture);
        ApplyUnlockUv(from, to);
        SetProgress(0f);
        shownSprite = to;

        if (unlockChanged)
        {
            Vector3 targetScale = unlocked ? unlockScale : lockScale;
            scaleTween = transform.DOScale(targetScale, animationDuration).SetEase(Ease.OutBack);
        }

        progressTween = instancedMaterial.DOFloat(1f, progressId, animationDuration)
            .SetEase(Ease.InOutSine)
            .OnUpdate(() =>
            {
                if (targetImage != null) targetImage.SetMaterialDirty();
            })
            .OnComplete(() =>
            {
                if (unlockChanged) InvokeStateEvent(unlocked);
            });
    }

    /// <summary>
    /// 追加の段階が無い、変数の 1段階目で、データベースも無いときは何もしない。
    /// </summary>
    private bool SkipUntilConfigured()
    {
        return !HasExtraStage()
            && lockSource == StoryLockSource.Variable
            && variableDatabase == null;
    }

    private bool HasExtraStage()
    {
        return extraStages != null && extraStages.Count > 0;
    }

    /// <summary>
    /// 1段階目と追加の段階が、すべて Unlock か。
    /// </summary>
    private bool CurrentUnlocked()
    {
        if (!StageUnlocked(lockSource, variableDatabase, targetKey, visitGraph, visitLabelGuid, invertCondition))
            return false;

        if (!HasExtraStage()) return true;

        for (int i = 0; i < extraStages.Count; i++)
        {
            StoryLockStage stage = extraStages[i];
            if (stage == null) return false;
            if (!StageUnlocked(stage.source, stage.variableDatabase, stage.targetKey, stage.visitGraph, stage.visitLabelGuid, stage.invertCondition))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 1段階の判定。参照できないときは Unlock ではない。
    /// </summary>
    private static bool StageUnlocked(
        StoryLockSource source,
        StoryVariableDatabase database,
        string key,
        StoryGraph graph,
        string labelGuid,
        bool invert)
    {
        bool unlocked;
        bool hasSource = source == StoryLockSource.Visit
            ? TryVisitUnlocked(graph, labelGuid, out unlocked)
            : TryVariableUnlocked(database, key, out unlocked);
        if (!hasSource) return false;
        if (invert) unlocked = !unlocked;
        return unlocked;
    }

    private void HandleVariableChanged(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (lockSource == StoryLockSource.Variable && key == targetKey)
        {
            UpdateButtonState();
            return;
        }

        if (!HasExtraStage()) return;
        for (int i = 0; i < extraStages.Count; i++)
        {
            StoryLockStage stage = extraStages[i];
            if (stage == null || stage.source != StoryLockSource.Variable || stage.targetKey != key) continue;
            UpdateButtonState();
            return;
        }
    }

    private void HandleVisitChanged()
    {
        if (lockSource == StoryLockSource.Visit)
        {
            UpdateButtonState();
            return;
        }

        if (!HasExtraStage()) return;
        for (int i = 0; i < extraStages.Count; i++)
        {
            StoryLockStage stage = extraStages[i];
            if (stage == null || stage.source != StoryLockSource.Visit) continue;
            UpdateButtonState();
            return;
        }
    }

    /// <summary>
    /// 割り当てた StoryGraph の Label が、StoryGotoUnlocks.json に記録されているか。
    /// グラフか Label が無いときは参照できない。
    /// </summary>
    private static bool TryVisitUnlocked(StoryGraph graph, string labelGuid, out bool unlocked)
    {
        unlocked = false;
        if (graph == null || string.IsNullOrEmpty(labelGuid))
            return false;

        if (StoryVisitButtonEntry.IsStartTarget(labelGuid))
        {
            if (StoryPlayer.FindStartNode(graph) == null)
                return false;
            if (Application.isPlaying)
                unlocked = StoryVisitLog.IsVisited(graph, StoryVisitButtonEntry.StartTarget);
            return true;
        }

        LabelNode label = graph.FindLabel(labelGuid);
        if (label == null)
            return false;

        if (Application.isPlaying)
            unlocked = StoryVisitLog.IsVisited(graph, label.GetDisplayName());
        return true;
    }

    /// <summary>
    /// 宣言された変数の現在値。定義が無いときは参照できない。
    /// </summary>
    private static bool TryVariableUnlocked(StoryVariableDatabase database, string key, out bool unlocked)
    {
        unlocked = false;
        if (database == null) return false;

        StoryVariableDefinition definition = database.GetDefinition(key);
        if (definition == null) return false;

        unlocked = IsUnlocked(definition, key);
        return true;
    }

    /// <summary>
    /// 実行中の値があればそれを使い、まだ無いときだけ宣言時の初期値を使う。
    /// </summary>
    private static bool IsUnlocked(StoryVariableDefinition definition, string key)
    {
        switch (definition.type)
        {
            case StoryVariableType.Bool:
                if (Application.isPlaying && GameManager.HasBool(key))
                    return GameManager.GetBool(key);
                return definition.defaultBoolValue;
            case StoryVariableType.Int:
            {
                if (Application.isPlaying && GameManager.TryGetNumeric(key, out float intValue))
                    return intValue > 0f;
                return definition.defaultIntValue > 0;
            }
            case StoryVariableType.Float:
            {
                if (Application.isPlaying && GameManager.TryGetNumeric(key, out float floatValue))
                    return floatValue > 0f;
                return definition.defaultFloatValue > 0f;
            }
            default:
                return false;
        }
    }

    private void ApplyStateImmediately(bool isUnlocked)
    {
        progressTween?.Kill();
        scaleTween?.Kill();
        targetButton.interactable = isUnlocked;
        transform.localScale = isUnlocked ? unlockScale : lockScale;
        SetProgress(isUnlocked ? 1f : 0f);
    }

    private void PlayTransitionAnimation(bool isUnlocked)
    {
        progressTween?.Kill();
        scaleTween?.Kill();

        float targetProgress = isUnlocked ? 1f : 0f;
        Vector3 targetScale = isUnlocked ? unlockScale : lockScale;

        scaleTween = transform.DOScale(targetScale, animationDuration).SetEase(Ease.OutBack);

        if (instancedMaterial != null && instancedMaterial.HasProperty(progressId))
        {
            progressTween = instancedMaterial.DOFloat(targetProgress, progressId, animationDuration)
                .SetEase(Ease.InOutSine)
                .OnUpdate(() => targetImage.SetMaterialDirty())
                .OnComplete(() => InvokeStateEvent(isUnlocked));
            return;
        }

        scaleTween.OnComplete(() => InvokeStateEvent(isUnlocked));
    }

    private void SetProgress(float value)
    {
        if (instancedMaterial == null || !instancedMaterial.HasProperty(progressId)) return;
        instancedMaterial.SetFloat(progressId, value);
        if (targetImage != null) targetImage.SetMaterialDirty();
    }

    private void InvokeStateEvent(bool isUnlocked)
    {
        if (isUnlocked) onUnlockedEvent?.Invoke();
        else onLockedEvent?.Invoke();
    }

    /// <summary>
    /// 一括生成から、1段階目の再生記録と共通設定を書き込む。
    /// すでに表示中なら見た目と Lock 状態を更新する。
    /// </summary>
    public void ApplyGeneratedVisit(
        StoryGraph graph,
        string labelGuid,
        bool invert,
        List<StoryLockStage> stages,
        Material sharedMaterial,
        Sprite lockedSprite,
        Sprite unlockedSprite,
        float duration,
        Vector3 lockedScale,
        Vector3 unlockedScale)
    {
        lockSource = StoryLockSource.Visit;
        variableDatabase = null;
        targetKey = "";
        visitGraph = graph;
        visitLabelGuid = labelGuid ?? "";
        invertCondition = invert;
        extraStages = CopyStages(stages);
        material = sharedMaterial;
        lockSprite = lockedSprite;
        unlockSprite = unlockedSprite;
        animationDuration = duration;
        lockScale = lockedScale;
        unlockScale = unlockedScale;

        if (targetButton == null) return;
        RebuildAppearance();
        UpdateButtonState();
    }

    /// <summary>
    /// 1段階目が、指定した再生記録か。
    /// </summary>
    public bool MatchesVisit(StoryGraph graph, string labelGuid)
    {
        return lockSource == StoryLockSource.Visit
            && visitGraph == graph
            && (visitLabelGuid ?? "") == (labelGuid ?? "");
    }

    /// <summary>
    /// 1段階目の再生記録と、共通設定が、指定どおりか。
    /// </summary>
    public bool MatchesGeneratedVisit(
        StoryGraph graph,
        string labelGuid,
        bool invert,
        List<StoryLockStage> stages,
        Material sharedMaterial,
        Sprite lockedSprite,
        Sprite unlockedSprite,
        float duration,
        Vector3 lockedScale,
        Vector3 unlockedScale)
    {
        if (!MatchesVisit(graph, labelGuid)) return false;
        if (invertCondition != invert) return false;
        if (material != sharedMaterial) return false;
        if (lockSprite != lockedSprite) return false;
        if (unlockSprite != unlockedSprite) return false;
        if (!Mathf.Approximately(animationDuration, duration)) return false;
        if (lockScale != lockedScale) return false;
        if (unlockScale != unlockedScale) return false;
        return SameStages(extraStages, stages);
    }

    private void RebuildAppearance()
    {
        if (instancedMaterial != null)
        {
            if (Application.isPlaying) Destroy(instancedMaterial);
            else DestroyImmediate(instancedMaterial);
            instancedMaterial = null;
        }

        ApplyAppearance();
    }

    private static List<StoryLockStage> CopyStages(List<StoryLockStage> stages)
    {
        List<StoryLockStage> copy = new List<StoryLockStage>();
        if (stages == null) return copy;

        for (int i = 0; i < stages.Count; i++)
        {
            StoryLockStage stage = stages[i];
            if (stage == null)
            {
                copy.Add(new StoryLockStage());
                continue;
            }

            copy.Add(new StoryLockStage
            {
                source = stage.source,
                variableDatabase = stage.variableDatabase,
                targetKey = stage.targetKey,
                visitGraph = stage.visitGraph,
                visitLabelGuid = stage.visitLabelGuid,
                invertCondition = stage.invertCondition,
                reachedSprite = stage.reachedSprite
            });
        }

        return copy;
    }

    private static bool SameStages(List<StoryLockStage> current, List<StoryLockStage> stages)
    {
        int count = current != null ? current.Count : 0;
        int other = stages != null ? stages.Count : 0;
        if (count != other) return false;

        for (int i = 0; i < count; i++)
        {
            if (!SameStage(current[i], stages[i])) return false;
        }

        return true;
    }

    private static bool SameStage(StoryLockStage current, StoryLockStage stage)
    {
        if (current == null || stage == null) return current == stage;
        return current.source == stage.source
            && current.variableDatabase == stage.variableDatabase
            && SameText(current.targetKey, stage.targetKey)
            && current.visitGraph == stage.visitGraph
            && SameText(current.visitLabelGuid, stage.visitLabelGuid)
            && current.invertCondition == stage.invertCondition
            && current.reachedSprite == stage.reachedSprite;
    }

    private static bool SameText(string current, string other)
    {
        return (current ?? "") == (other ?? "");
    }
}

#if UNITY_EDITOR
/// <summary>
/// StoryVariableLockButton のインスペクタ。説明と、変数または再生記録の一覧を出す。追加の段階と複数選択に対応する。
/// </summary>
[CanEditMultipleObjects]
[CustomEditor(typeof(StoryVariableLockButton))]
public class StoryVariableLockButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(StoryVariableLockButton.Summary, MessageType.Info);

        SerializedProperty property = serializedObject.GetIterator();
        property.NextVisible(true);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(property);
        }

        SerializedProperty sourceProperty = serializedObject.FindProperty("lockSource");
        bool sourceMixed = sourceProperty != null && sourceProperty.hasMultipleDifferentValues;
        bool visit = sourceProperty != null && sourceProperty.enumValueIndex == (int)StoryLockSource.Visit;
        bool showVariable = sourceMixed || !visit;
        bool showVisit = sourceMixed || visit;

        while (property.NextVisible(false))
        {
            if (property.name == "lockSource")
            {
                EditorGUILayout.LabelField("1段階目", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(property, new GUIContent("判定", "変数の値か、StoryGotoUnlocks.json の再生記録か。"));
                continue;
            }

            if (property.name == "variableDatabase" || property.name == "targetKey")
            {
                if (!showVariable) continue;
                if (property.name == "targetKey")
                    DrawVariablePopup(serializedObject.FindProperty("variableDatabase"), property);
                else
                    EditorGUILayout.PropertyField(property, true);
                continue;
            }

            if (property.name == "visitGraph" || property.name == "visitLabelGuid")
            {
                if (!showVisit) continue;
                if (property.name == "visitLabelGuid")
                    DrawVisitLabelPopup(serializedObject.FindProperty("visitGraph"), property);
                else
                    EditorGUILayout.PropertyField(property, new GUIContent("StoryGraph", "このグラフのアセット名と Label の組で、StoryGotoUnlocks.json を見ます。"));
                continue;
            }

            if (property.name == "extraStages")
            {
                DrawExtraStages(property);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawExtraStages(SerializedProperty listProperty)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("追加の段階", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("1段階目と、ここにある段階がすべて Unlock のときだけボタンを押せます。各段階の画像は、その手前まで進んだときに出します。全部揃ったときは Unlock 画像です。", MessageType.Info);

        bool mixedCount = ExtraStageCountsDiffer();
        int shown = mixedCount ? 0 : listProperty.arraySize;
        EditorGUI.showMixedValue = mixedCount;
        int nextCount;
        bool countChanged;
        try
        {
            EditorGUI.BeginChangeCheck();
            nextCount = EditorGUILayout.IntField(new GUIContent("追加の段階数", "1段階目のあとに見る段階の数。"), shown);
            countChanged = EditorGUI.EndChangeCheck();
        }
        finally
        {
            EditorGUI.showMixedValue = false;
        }

        if (countChanged)
        {
            listProperty.arraySize = Mathf.Max(0, nextCount);
            return;
        }

        if (mixedCount)
        {
            EditorGUILayout.HelpBox("段階数が揃うと、各段階を編集できます。段階数を変えると、選択中の全部が同じ数になります。", MessageType.Info);
            return;
        }

        for (int i = 0; i < listProperty.arraySize; i++)
            DrawStage(listProperty.GetArrayElementAtIndex(i), i);
    }

    private void DrawStage(SerializedProperty stage, int index)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField((index + 2) + "段階目", EditorStyles.boldLabel);

        SerializedProperty sourceProperty = stage.FindPropertyRelative("source");
        EditorGUILayout.PropertyField(sourceProperty, new GUIContent("判定", "変数の値か、StoryGotoUnlocks.json の再生記録か。"));

        bool sourceMixed = sourceProperty != null && sourceProperty.hasMultipleDifferentValues;
        bool visit = sourceProperty != null && sourceProperty.enumValueIndex == (int)StoryLockSource.Visit;
        if (sourceMixed || !visit)
        {
            SerializedProperty databaseProperty = stage.FindPropertyRelative("variableDatabase");
            EditorGUILayout.PropertyField(databaseProperty, new GUIContent("データベース"));
            DrawVariablePopup(databaseProperty, stage.FindPropertyRelative("targetKey"));
        }

        if (sourceMixed || visit)
        {
            SerializedProperty graphProperty = stage.FindPropertyRelative("visitGraph");
            EditorGUILayout.PropertyField(graphProperty, new GUIContent("StoryGraph", "このグラフのアセット名と Label の組で、StoryGotoUnlocks.json を見ます。"));
            DrawVisitLabelPopup(graphProperty, stage.FindPropertyRelative("visitLabelGuid"));
        }

        EditorGUILayout.PropertyField(stage.FindPropertyRelative("invertCondition"), new GUIContent("条件を反転", "この段階だけ Unlock と Lock を入れ替えます。"));
        EditorGUILayout.PropertyField(
            stage.FindPropertyRelative("reachedSprite"),
            new GUIContent((index + 1) + "段階目までの画像", "手前の段階まで満たして、この段階はまだのときに出す画像。未設定なら Lock 画像のままです。全部満たしたときは Unlock 画像です。"));
    }

    private bool ExtraStageCountsDiffer()
    {
        UnityEngine.Object[] targets = serializedObject.targetObjects;
        if (targets == null || targets.Length <= 1) return false;

        int count = -1;
        for (int i = 0; i < targets.Length; i++)
        {
            using (SerializedObject other = new SerializedObject(targets[i]))
            {
                SerializedProperty list = other.FindProperty("extraStages");
                int size = list != null ? list.arraySize : 0;
                if (count < 0) count = size;
                else if (count != size) return true;
            }
        }

        return false;
    }

    private void DrawVariablePopup(SerializedProperty databaseProperty, SerializedProperty keyProperty)
    {
        StoryVariableDatabase database = databaseProperty != null
            ? databaseProperty.objectReferenceValue as StoryVariableDatabase
            : null;

        string current = keyProperty.stringValue ?? "";
        List<string> keys = new List<string>();
        List<string> labels = new List<string>();
        if (database != null && database.variables != null)
        {
            for (int i = 0; i < database.variables.Count; i++)
            {
                StoryVariableDefinition definition = database.variables[i];
                if (definition == null || string.IsNullOrEmpty(definition.key)) continue;
                if (definition.type != StoryVariableType.Bool
                    && definition.type != StoryVariableType.Int
                    && definition.type != StoryVariableType.Float) continue;
                if (keys.Contains(definition.key)) continue;

                keys.Add(definition.key);
                labels.Add(definition.key + " (" + definition.type + ")");
            }
        }

        if (databaseProperty != null && databaseProperty.hasMultipleDifferentValues)
        {
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("対象変数"));
            EditorGUILayout.HelpBox("データベースが揃うと、宣言済みの変数から選べます。", MessageType.Info);
            return;
        }

        if (keys.Count == 0)
        {
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("対象変数"));
            EditorGUILayout.HelpBox("StoryVariableDatabase を指定すると、宣言済みの変数から選べます。", MessageType.Info);
            return;
        }

        if (!keyProperty.hasMultipleDifferentValues)
        {
            if (string.IsNullOrEmpty(current))
            {
                keys.Insert(0, "");
                labels.Insert(0, "（未選択）");
            }
            else if (!keys.Contains(current))
            {
                keys.Insert(0, current);
                labels.Insert(0, current);
            }
        }

        DrawChoicePopup(
            new GUIContent("対象変数", "Lock 判定に使う変数。実行中の値を見ます。"),
            keyProperty,
            keys,
            labels);
    }

    private void DrawVisitLabelPopup(SerializedProperty graphProperty, SerializedProperty guidProperty)
    {
        StoryGraph graph = graphProperty != null
            ? graphProperty.objectReferenceValue as StoryGraph
            : null;

        string current = guidProperty.stringValue ?? "";
        List<string> guids = new List<string>();
        List<string> labels = new List<string>();
        StoryVisitButtonEntry.CollectLabelChoices(graph, guids, labels);

        if (graphProperty != null && graphProperty.hasMultipleDifferentValues)
        {
            EditorGUILayout.PropertyField(guidProperty, new GUIContent("対象ラベル"));
            EditorGUILayout.HelpBox("StoryGraph が揃うと、その Label から選べます。", MessageType.Info);
            return;
        }

        if (guids.Count == 0)
        {
            EditorGUILayout.HelpBox("StoryGraph を指定すると、その Label から選べます。そのグラフのアセット名とラベル名の組が StoryGotoUnlocks.json にあるとき Unlock です。", MessageType.Info);
            return;
        }

        if (!guidProperty.hasMultipleDifferentValues)
        {
            if (string.IsNullOrEmpty(current))
            {
                guids.Insert(0, "");
                labels.Insert(0, "（未選択）");
            }
            else if (!guids.Contains(current))
            {
                string shortGuid = current.Length > 8 ? current.Substring(0, 8) : current;
                guids.Insert(0, current);
                labels.Insert(0, "(欠損) " + shortGuid);
            }
        }

        DrawChoicePopup(
            new GUIContent("対象ラベル", "Lock 判定に使う Label。"),
            guidProperty,
            guids,
            labels);
    }

    private static void DrawChoicePopup(GUIContent label, SerializedProperty valueProperty, List<string> values, List<string> labels)
    {
        bool mixed = valueProperty.hasMultipleDifferentValues;
        string current = valueProperty.stringValue ?? "";
        int index = 0;
        if (!mixed)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == current)
                {
                    index = i;
                    break;
                }
            }
        }

        EditorGUI.showMixedValue = mixed;
        int next;
        bool changed;
        try
        {
            EditorGUI.BeginChangeCheck();
            next = EditorGUILayout.Popup(label, index, labels.ToArray());
            changed = EditorGUI.EndChangeCheck();
        }
        finally
        {
            EditorGUI.showMixedValue = false;
        }

        if (changed && next >= 0 && next < values.Count)
            valueProperty.stringValue = values[next];
    }
}
#endif
