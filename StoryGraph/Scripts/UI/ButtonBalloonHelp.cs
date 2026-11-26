using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ボタンに付ける。カーソルを重ねているあいだ、文章をバルーンで出す。
/// 説明は ButtonBalloonHelp.Summary。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("UI/Button Balloon Help")]
public class ButtonBalloonHelp : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public const string Summary =
        "ボタンの Image と同じオブジェクトに付けます。ゲーム実行中、カーソルを重ねてから指定の秒が過ぎると、文章をボタンの上へ出します。\n" +
        "秒が 0 ならすぐ出します。離れると、待ちの途中でも閉じます。文章が空のときは出しません。\n" +
        "出すまでの秒、フォント、色、余白、位置、最大幅はこのコンポーネントに書きます。\n" +
        "画面に出るバルーンは1つです。地は実行時の白い画像で、Unity の内蔵スプライトは使いません。\n" +
        "バルーンはクリックを受け取りません。";

    [TextArea(2, 4)]
    [Tooltip("カーソルを重ねたときに出す文章。空なら出さない")]
    [SerializeField] private string message = "";

    [Tooltip("カーソルを重ねてから出すまでの秒。0 ならすぐ出す。時間停止中も進む")]
    [SerializeField] private float showDelay;

    [Tooltip("空なら TextMeshPro の標準フォント")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int fontSize = 18;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color background = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Vector2 padding = new Vector2(16f, 10f);
    [Tooltip("ボタン上端から、バルーンの下端までのずれ")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 12f);
    [Tooltip("余白を含めたバルーンの幅の上限")]
    [SerializeField] private float maxWidth = 280f;

    private bool inside;
    private float hoverStartTime;

    private static RectTransform balloonBody;
    private static TextMeshProUGUI balloonText;
    private static Image balloonBackgroundImage;
    private static RectTransform balloonArrow;
    private static Image balloonArrowImage;
    private static ButtonBalloonHelp shownBy;
    private static Sprite whiteSprite;

    /// <summary>
    /// カーソルを重ねたときに出す文章。空ならバルーンは出ない。
    /// </summary>
    public string Message
    {
        get => message ?? "";
        set
        {
            message = value ?? "";
            if (inside)
                Refresh();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inside = true;
        hoverStartTime = Time.unscaledTime;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inside = false;
        HideIfMine();
    }

    void OnDisable()
    {
        inside = false;
        HideIfMine();
    }

    void LateUpdate()
    {
        if (!inside) return;
        Refresh();
    }

    private void Refresh()
    {
        if (!Application.isPlaying) return;
        if (string.IsNullOrWhiteSpace(message) || !DelayElapsed())
        {
            HideIfMine();
            return;
        }

        RectTransform target = transform as RectTransform;
        if (target == null || !EnsureBalloon(target))
        {
            HideIfMine();
            return;
        }

        shownBy = this;
        ApplyStyle(message);
        if (!balloonBody.gameObject.activeSelf)
            balloonBody.gameObject.SetActive(true);
        balloonBody.SetAsLastSibling();
        PlaceBalloon(target);
    }

    private bool DelayElapsed()
    {
        return Time.unscaledTime - hoverStartTime >= Mathf.Max(0f, showDelay);
    }

    private void HideIfMine()
    {
        if (shownBy != this) return;
        shownBy = null;
        if (balloonBody != null)
            balloonBody.gameObject.SetActive(false);
    }

    private bool EnsureBalloon(RectTransform target)
    {
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null) return false;
        canvas = canvas.rootCanvas;
        if (canvas == null) return false;

        if (balloonBody != null && balloonText != null && balloonArrow != null)
        {
            if (balloonBody.parent != canvas.transform)
                balloonBody.SetParent(canvas.transform, false);
            return true;
        }

        if (balloonBody != null)
            Destroy(balloonBody.gameObject);

        GameObject body = new GameObject("ButtonBalloonHelp", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        body.transform.SetParent(canvas.transform, false);
        body.hideFlags = HideFlags.HideAndDontSave;
        LayoutElement layout = body.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;

        balloonBody = body.GetComponent<RectTransform>();
        balloonBody.anchorMin = new Vector2(0.5f, 0.5f);
        balloonBody.anchorMax = new Vector2(0.5f, 0.5f);
        balloonBody.pivot = new Vector2(0.5f, 0f);
        balloonBody.localScale = Vector3.one;
        balloonBody.localRotation = Quaternion.identity;

        balloonBackgroundImage = body.GetComponent<Image>();
        balloonBackgroundImage.raycastTarget = false;
        balloonBackgroundImage.sprite = WhiteSprite();
        balloonBackgroundImage.color = background;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(balloonBody, false);
        textObject.hideFlags = HideFlags.HideAndDontSave;
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = padding;
        textRect.offsetMax = -padding;
        balloonText = textObject.GetComponent<TextMeshProUGUI>();
        balloonText.raycastTarget = false;
        balloonText.alignment = TextAlignmentOptions.TopLeft;
        balloonText.enableWordWrapping = true;
        balloonText.overflowMode = TextOverflowModes.Overflow;

        GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowObject.transform.SetParent(balloonBody, false);
        arrowObject.hideFlags = HideFlags.HideAndDontSave;
        balloonArrow = arrowObject.GetComponent<RectTransform>();
        balloonArrow.anchorMin = new Vector2(0.5f, 0f);
        balloonArrow.anchorMax = new Vector2(0.5f, 0f);
        balloonArrow.pivot = new Vector2(0.5f, 1f);
        balloonArrow.sizeDelta = new Vector2(16f, 10f);
        balloonArrow.anchoredPosition = Vector2.zero;
        balloonArrowImage = arrowObject.GetComponent<Image>();
        balloonArrowImage.raycastTarget = false;
        balloonArrowImage.sprite = WhiteSprite();
        balloonArrowImage.color = background;

        body.SetActive(false);
        return true;
    }

    /// <summary>
    /// バルーン用の無地スプライト。Unity の内蔵 UI 画像は使わない。
    /// </summary>
    private static Sprite WhiteSprite()
    {
        if (whiteSprite != null) return whiteSprite;
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        whiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return whiteSprite;
    }

    private void ApplyStyle(string shown)
    {
        if (balloonText == null || balloonBody == null) return;
        if (font != null)
            balloonText.font = font;
        balloonText.fontSize = Mathf.Max(1, fontSize);
        balloonText.color = textColor;
        balloonText.text = shown;
        if (balloonBackgroundImage != null)
            balloonBackgroundImage.color = background;
        if (balloonArrowImage != null)
            balloonArrowImage.color = background;

        float innerWidth = Mathf.Max(1f, maxWidth - padding.x * 2f);
        balloonText.rectTransform.offsetMin = new Vector2(padding.x, padding.y);
        balloonText.rectTransform.offsetMax = new Vector2(-padding.x, -padding.y);
        Vector2 preferred = balloonText.GetPreferredValues(shown, innerWidth, 0f);
        float width = Mathf.Clamp(preferred.x, 1f, innerWidth) + padding.x * 2f;
        float height = preferred.y + padding.y * 2f;
        balloonBody.sizeDelta = new Vector2(width, height);
    }

    private void PlaceBalloon(RectTransform target)
    {
        Canvas canvas = balloonBody.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 top = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMax, 0f));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, top);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out Vector2 local))
            return;

        balloonBody.anchorMin = canvasRect.pivot;
        balloonBody.anchorMax = canvasRect.pivot;
        Vector2 pos = local + offset;
        Rect area = canvasRect.rect;
        float halfW = balloonBody.sizeDelta.x * 0.5f;
        float height = balloonBody.sizeDelta.y;
        pos.x = Mathf.Clamp(pos.x, area.xMin + halfW, area.xMax - halfW);
        pos.y = Mathf.Clamp(pos.y, area.yMin, area.yMax - height);
        balloonBody.anchoredPosition = pos;

        if (balloonArrow == null) return;
        float arrowX = local.x - pos.x;
        float limit = Mathf.Max(0f, halfW - balloonArrow.sizeDelta.x * 0.5f);
        balloonArrow.anchoredPosition = new Vector2(Mathf.Clamp(arrowX, -limit, limit), 0f);
    }
}

#if UNITY_EDITOR
/// <summary>
/// ButtonBalloonHelp のインスペクタ。付け方と、文章・見た目を出す。
/// </summary>
[CustomEditor(typeof(ButtonBalloonHelp))]
public class ButtonBalloonHelpEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(ButtonBalloonHelp.Summary, MessageType.Info);

        EditorGUILayout.LabelField("表示", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("message"), new GUIContent("文章", "カーソルを重ねたときに出す文章。空なら出さない"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showDelay"), new GUIContent("出すまでの秒", "カーソルを重ねてから出すまでの秒。0 ならすぐ出す。時間停止中も進む"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("見た目", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("font"), new GUIContent("フォント", "空なら TextMeshPro の標準フォント"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fontSize"), new GUIContent("フォントサイズ"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("textColor"), new GUIContent("文字色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("background"), new GUIContent("背景色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("padding"), new GUIContent("余白"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("offset"), new GUIContent("位置のずれ", "ボタン上端から、バルーンの下端までのずれ"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxWidth"), new GUIContent("最大幅", "余白を含めたバルーンの幅の上限"));
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
