using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.Scripting.APIUpdating;
using DG.Tweening;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif

/// <summary>
/// StoryVariableDatabase で宣言された変数の、いまの値を見てボタンを Lock する。
/// 説明は StoryVariableLockButton.Summary。
/// </summary>
[MovedFrom(false, sourceClassName: "ShaderLockSpriteButton")]
[RequireComponent(typeof(Button), typeof(Image))]
public class StoryVariableLockButton : MonoBehaviour
{
    public const string Summary =
        "StoryVariableDatabase で宣言された変数の、いまの値を見てボタンを Lock します。\n" +
        "Bool は true のとき Unlock です。Int と Float は 0 より大きいとき Unlock です。\n" +
        "変数はデータベースの一覧から選びます。実行中に値が変わると、Lock 状態も追従します。\n" +
        "まだ実行中の値が無いときは、データベースの初期値を使います。条件を反転すると Unlock と Lock が入れ替わります。\n" +
        "マテリアル、Lock 画像、Unlock 画像はこのコンポーネントに登録します。Image の Source Image と Material はここから設定されます。";

    [Header("データベースとキーの設定")]
    [SerializeField] private StoryVariableDatabase variableDatabase;
    [SerializeField] private string targetKey = "UnlockFlag";
    [SerializeField] private bool invertCondition = false;

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
    private Tween progressTween;
    private Tween scaleTween;

    private readonly int progressId = Shader.PropertyToID("_Progress");
    private readonly int mainTexId = Shader.PropertyToID("_MainTex");
    private readonly int unlockTexId = Shader.PropertyToID("_UnlockTex");

    void Awake()
    {
        targetButton = GetComponent<Button>();
        targetImage = GetComponent<Image>();
        ApplyAppearance();
    }

    void OnValidate()
    {
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
    }

    void OnEnable()
    {
        GameManager.OnVariableChanged += HandleVariableChanged;
        UpdateButtonState();
    }

    void OnDisable()
    {
        GameManager.OnVariableChanged -= HandleVariableChanged;
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
    /// 宣言された変数の現在値から Lock 状態を決め、変化があればアニメーションする。
    /// </summary>
    public void UpdateButtonState()
    {
        if (variableDatabase == null || targetButton == null) return;

        StoryVariableDefinition definition = variableDatabase.GetDefinition(targetKey);
        bool newState = false;

        if (definition != null)
        {
            newState = IsUnlocked(definition);
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

    private void HandleVariableChanged(string key)
    {
        if (key != targetKey) return;
        UpdateButtonState();
    }

    /// <summary>
    /// 実行中の値があればそれを使い、まだ無いときだけ宣言時の初期値を使う。
    /// </summary>
    private bool IsUnlocked(StoryVariableDefinition definition)
    {
        switch (definition.type)
        {
            case StoryVariableType.Bool:
                if (Application.isPlaying && GameManager.HasBool(targetKey))
                    return GameManager.GetBool(targetKey);
                return definition.defaultBoolValue;
            case StoryVariableType.Int:
            {
                if (Application.isPlaying && GameManager.TryGetNumeric(targetKey, out float intValue))
                    return intValue > 0f;
                return definition.defaultIntValue > 0;
            }
            case StoryVariableType.Float:
            {
                if (Application.isPlaying && GameManager.TryGetNumeric(targetKey, out float floatValue))
                    return floatValue > 0f;
                return definition.defaultFloatValue > 0f;
            }
            default:
                return false;
        }
    }

    private void ApplyStateImmediately(bool isUnlocked)
    {
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
}

#if UNITY_EDITOR
/// <summary>
/// StoryVariableLockButton のインスペクタ。説明と、データベースの変数一覧を出す。
/// </summary>
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

        while (property.NextVisible(false))
        {
            if (property.name == "targetKey")
            {
                DrawVariablePopup(property);
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawVariablePopup(SerializedProperty keyProperty)
    {
        SerializedProperty databaseProperty = serializedObject.FindProperty("variableDatabase");
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

        if (keys.Count == 0)
        {
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("対象変数"));
            EditorGUILayout.HelpBox("StoryVariableDatabase を指定すると、宣言済みの変数から選べます。", MessageType.Info);
            return;
        }

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

        int index = 0;
        for (int i = 0; i < keys.Count; i++)
        {
            if (keys[i] == current)
            {
                index = i;
                break;
            }
        }

        int next = EditorGUILayout.Popup(
            new GUIContent("対象変数", "Lock 判定に使う変数。実行中の値を見ます。"),
            index,
            labels.ToArray());
        if (next != index)
            keyProperty.stringValue = keys[next];
    }
}
#endif
