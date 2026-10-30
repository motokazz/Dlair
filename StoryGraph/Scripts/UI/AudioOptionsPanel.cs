using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音量カタログからスライダーを並べ、変更を AudioOptions へ渡す。
/// </summary>
[DisallowMultipleComponent]
public class AudioOptionsPanel : MonoBehaviour
{
    public AudioOptionsCatalog catalog;
    public string title = "オーディオ";

    RectTransform contentRoot;

    void OnEnable()
    {
        if (catalog != null)
            AudioOptions.SetCatalog(catalog);

        GameSettings.Cleared += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        GameSettings.Cleared -= Rebuild;
    }

    [ContextMenu("スライダーを再生成")]
    public void Rebuild()
    {
        if (catalog == null)
            catalog = AudioOptions.Catalog;

        EnsureChrome();
        ClearRows();

        if (catalog == null || catalog.options == null)
            return;

        for (int i = 0; i < catalog.options.Count; i++)
        {
            AudioOptionEntry entry = catalog.options[i];
            if (entry == null || entry.kind != AudioOptionKind.VolumeSlider)
                continue;

            CreateVolumeRow(entry);
        }

        if (Application.isPlaying)
            AudioOptions.ApplyAll();
    }

    void EnsureChrome()
    {
        Image background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();

        background.sprite = WhiteSprite;
        background.type = Image.Type.Simple;
        background.color = new Color(0.05f, 0.05f, 0.07f, 0.86f);
        background.raycastTarget = true;

        VerticalLayoutGroup layout = GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(18, 18, 14, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Transform titleTransform = transform.Find("Title");
        TextMeshProUGUI titleLabel;
        if (titleTransform == null)
        {
            GameObject titleObject = CreateUiObject("Title", transform);
            LayoutElement titleLayout = titleObject.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 32f;
            titleLabel = titleObject.AddComponent<TextMeshProUGUI>();
            titleLabel.font = TMP_Settings.defaultFontAsset;
            titleLabel.fontSize = 24f;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            titleLabel.color = Color.white;
            titleLabel.raycastTarget = false;
        }
        else
        {
            titleLabel = titleTransform.GetComponent<TextMeshProUGUI>();
        }

        if (titleLabel != null)
            titleLabel.text = title;

        Transform contentTransform = transform.Find("Content");
        if (contentTransform == null)
        {
            GameObject contentObject = CreateUiObject("Content", transform);
            contentRoot = contentObject.GetComponent<RectTransform>();
            VerticalLayoutGroup rows = contentObject.AddComponent<VerticalLayoutGroup>();
            rows.spacing = 8f;
            rows.childAlignment = TextAnchor.UpperLeft;
            rows.childControlWidth = true;
            rows.childControlHeight = true;
            rows.childForceExpandWidth = true;
            rows.childForceExpandHeight = false;
            ContentSizeFitter contentFitter = contentObject.AddComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            contentRoot = contentTransform.GetComponent<RectTransform>();
        }
    }

    void ClearRows()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    void CreateVolumeRow(AudioOptionEntry entry)
    {
        GameObject row = CreateUiObject(entry.id + " Row", contentRoot);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 36f;
        rowLayout.minHeight = 36f;

        HorizontalLayoutGroup rowGroup = row.AddComponent<HorizontalLayoutGroup>();
        rowGroup.spacing = 12f;
        rowGroup.childAlignment = TextAnchor.MiddleLeft;
        rowGroup.childControlWidth = true;
        rowGroup.childControlHeight = true;
        rowGroup.childForceExpandWidth = false;
        rowGroup.childForceExpandHeight = true;

        TextMeshProUGUI nameLabel = CreateLabel(row.transform, "Name", entry.label, 88f, TextAlignmentOptions.MidlineLeft);
        nameLabel.fontSize = 20f;

        Slider slider = CreateSlider(row.transform);
        float volume = AudioOptions.GetVolume(entry);
        slider.SetValueWithoutNotify(volume);

        TextMeshProUGUI valueLabel = CreateLabel(row.transform, "Value", FormatPercent(volume), 58f, TextAlignmentOptions.MidlineRight);
        valueLabel.fontSize = 18f;

        AudioOptionEntry bound = entry;
        slider.onValueChanged.AddListener(value =>
        {
            AudioOptions.SetVolume(bound, value);
            valueLabel.text = FormatPercent(value);
        });
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float width, TextAlignmentOptions alignment)
    {
        GameObject labelObject = CreateUiObject(name, parent);
        LayoutElement layout = labelObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = width;
        layout.flexibleWidth = 0f;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.alignment = alignment;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    static Slider CreateSlider(Transform parent)
    {
        GameObject sliderObject = CreateUiObject("Slider", parent);
        LayoutElement layout = sliderObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 120f;
        layout.preferredHeight = 24f;

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.direction = Slider.Direction.LeftToRight;

        RectTransform background = CreateStretch("Background", sliderObject.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -4f), new Vector2(0f, 4f));
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.sprite = WhiteSprite;
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.color = new Color(1f, 1f, 1f, 0.18f);
        backgroundImage.raycastTarget = false;

        RectTransform fillArea = CreateStretch("Fill Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(5f, 0f), new Vector2(-15f, 0f));
        RectTransform fill = CreateStretch("Fill", fillArea, Vector2.zero, Vector2.one, new Vector2(-5f, 0f), new Vector2(5f, 0f));
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.sprite = WhiteSprite;
        fillImage.type = Image.Type.Simple;
        fillImage.color = new Color(0.95f, 0.95f, 0.95f, 0.9f);
        fillImage.raycastTarget = false;

        RectTransform handleArea = CreateStretch("Handle Slide Area", sliderObject.transform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        RectTransform handle = CreateStretch("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        handle.sizeDelta = new Vector2(18f, 18f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = WhiteSprite;
        handleImage.color = Color.white;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        return slider;
    }

    static RectTransform CreateStretch(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject item = CreateUiObject(name, parent);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject item = new GameObject(name, typeof(RectTransform));
        item.layer = parent.gameObject.layer;
        item.transform.SetParent(parent, false);
        return item;
    }

    static string FormatPercent(float volume)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(volume) * 100f) + "%";
    }

    static Sprite whiteSprite;

    static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite != null) return whiteSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return whiteSprite;
        }
    }
}
