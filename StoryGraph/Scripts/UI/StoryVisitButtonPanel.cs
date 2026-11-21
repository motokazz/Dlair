using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 1件の再生記録。生成ボタンの1段階目、バルーンの文章、押したときの行先になる。
/// </summary>
[System.Serializable]
public class StoryVisitButtonEntry
{
    public StoryGraph visitGraph;
    public string visitLabelGuid;
    [TextArea(2, 4)] public string message;
}

/// <summary>
/// 再生記録のリストから、Panel の子ボタンを揃える。
/// 1段階目だけが行ごとに違い、それ以外は共通設定を書く。
/// 説明は StoryVisitButtonPanel.Summary。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Story Visit Button Panel")]
public class StoryVisitButtonPanel : MonoBehaviour
{
    public const string Summary =
        "再生記録のリストを登録すると、このオブジェクトの子にボタンを揃えます。\n" +
        "1件がボタン1つです。その再生記録が、ボタンの1段階目です。StoryGraph と Label が空の行はボタンにしません。\n" +
        "段階の働きは固定です。0段目は今の Lock のままです。1段目以降は、カーソルを重ねるとリストの文章をバルーンで出します。2段目（全部 Unlock）で Goto します。バルーンは 2段目でも残します。\n" +
        "文章は行ごとに書きます。バルーンのフォント、色、余白、位置はすべてのボタンで共通です。\n" +
        "追加の段階、画像、マテリアル、アニメーション、条件の反転も共通です。\n" +
        "子ボタンはゲームの実行中だけ作ります。プレハブ資産には追加しません。\n" +
        "リストと一致する子ボタンは残します。足りなければ Spawn し、多ければ余分を消します。内容が違えば、そのボタンへ書き直します。\n" +
        "StoryVariableLockButton を持たない子は残します。";

    [Header("1段階目")]
    [SerializeField] private List<StoryVisitButtonEntry> visits = new List<StoryVisitButtonEntry>();

    [Header("共通設定")]
    [SerializeField] private bool invertCondition;
    [SerializeField] private List<StoryLockStage> extraStages = new List<StoryLockStage>();
    [SerializeField] private Material material;
    [SerializeField] private Sprite lockSprite;
    [SerializeField] private Sprite unlockSprite;
    [SerializeField] private float animationDuration = 0.6f;
    [SerializeField] private Vector3 lockScale = new Vector3(0.9f, 0.9f, 1f);
    [SerializeField] private Vector3 unlockScale = Vector3.one;

    [Header("バルーン")]
    [SerializeField] private TMP_FontAsset balloonFont;
    [SerializeField] private int balloonFontSize = 18;
    [SerializeField] private Color balloonTextColor = Color.black;
    [SerializeField] private Color balloonBackground = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Vector2 balloonPadding = new Vector2(16f, 10f);
    [SerializeField] private Vector2 balloonOffset = new Vector2(0f, 12f);
    [SerializeField] private float balloonMaxWidth = 280f;

    private RectTransform balloonBody;
    private TextMeshProUGUI balloonText;
    private StoryVisitHoverBalloon balloonSource;
    private string balloonMessage;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        Sync();
    }

    void OnDisable()
    {
        ReleaseClicks();
        HideBalloon(null);
    }

    void OnDestroy()
    {
        if (balloonBody != null)
            Destroy(balloonBody.gameObject);
    }

    /// <summary>
    /// 実行中の子ボタンを、リストと共通設定に揃える。プレハブ資産には書かない。
    /// </summary>
    public void Sync()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;

        ReleaseClicks();

        List<StoryVisitButtonEntry> entries = ReadyEntries();
        List<StoryVariableLockButton> locks = OwnedLocks();

        while (locks.Count > entries.Count)
        {
            int last = locks.Count - 1;
            DestroyOwned(locks[last].gameObject);
            locks.RemoveAt(last);
        }

        while (locks.Count < entries.Count)
        {
            StoryVariableLockButton created = CreateButton(entries[locks.Count]);
            if (created == null) break;
            locks.Add(created);
        }

        for (int i = 0; i < entries.Count && i < locks.Count; i++)
            ApplyEntry(locks[i], entries[i]);

        BindClicks(locks);
    }

    private List<StoryVisitButtonEntry> ReadyEntries()
    {
        List<StoryVisitButtonEntry> ready = new List<StoryVisitButtonEntry>();
        if (visits == null) return ready;

        for (int i = 0; i < visits.Count; i++)
        {
            StoryVisitButtonEntry entry = visits[i];
            if (entry == null || entry.visitGraph == null || string.IsNullOrEmpty(entry.visitLabelGuid))
                continue;
            ready.Add(entry);
        }

        return ready;
    }

    private List<StoryVariableLockButton> OwnedLocks()
    {
        List<StoryVariableLockButton> locks = new List<StoryVariableLockButton>();
        for (int i = 0; i < transform.childCount; i++)
        {
            StoryVariableLockButton lockButton = transform.GetChild(i).GetComponent<StoryVariableLockButton>();
            if (lockButton != null)
                locks.Add(lockButton);
        }

        return locks;
    }

    private void ApplyEntry(StoryVariableLockButton lockButton, StoryVisitButtonEntry entry)
    {
        if (lockButton == null || entry == null) return;

        StoryGotoButton gotoButton = lockButton.GetComponent<StoryGotoButton>();
        if (gotoButton == null)
            gotoButton = AddGoto(lockButton.gameObject);

        bool same = lockButton.MatchesGeneratedVisit(
            entry.visitGraph,
            entry.visitLabelGuid,
            invertCondition,
            extraStages,
            material,
            lockSprite,
            unlockSprite,
            animationDuration,
            lockScale,
            unlockScale) && gotoButton.MatchesTarget(entry.visitGraph, entry.visitLabelGuid);
        EnsureHover(lockButton, entry.message);
        if (same)
        {
            EnsureClick(lockButton.GetComponent<Button>(), gotoButton);
            return;
        }

        lockButton.ApplyGeneratedVisit(
            entry.visitGraph,
            entry.visitLabelGuid,
            invertCondition,
            extraStages,
            material,
            lockSprite,
            unlockSprite,
            animationDuration,
            lockScale,
            unlockScale);
        gotoButton.SetTarget(entry.visitGraph, entry.visitLabelGuid);
        EnsureClick(lockButton.GetComponent<Button>(), gotoButton);
    }

    private StoryVariableLockButton CreateButton(StoryVisitButtonEntry entry)
    {
        string buttonName = NameFor(entry);
        GameObject go = new GameObject(buttonName, typeof(RectTransform));
        go.layer = gameObject.layer;
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = go.AddComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;
        image.sprite = lockSprite;

        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f);
        colors.pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = colors.pressedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.targetGraphic = image;

        StoryVariableLockButton lockButton = go.AddComponent<StoryVariableLockButton>();
        StoryGotoButton gotoButton = go.AddComponent<StoryGotoButton>();
        lockButton.ApplyGeneratedVisit(
            entry.visitGraph,
            entry.visitLabelGuid,
            invertCondition,
            extraStages,
            material,
            lockSprite,
            unlockSprite,
            animationDuration,
            lockScale,
            unlockScale);
        gotoButton.SetTarget(entry.visitGraph, entry.visitLabelGuid);
        EnsureClick(button, gotoButton);

        go.SetActive(true);
        return lockButton;
    }

    private static StoryGotoButton AddGoto(GameObject go)
    {
        return go.AddComponent<StoryGotoButton>();
    }

    private string NameFor(StoryVisitButtonEntry entry)
    {
        string labelName = "Visit";
        if (entry != null && entry.visitGraph != null)
        {
            LabelNode label = entry.visitGraph.FindLabel(entry.visitLabelGuid);
            if (label != null && !string.IsNullOrWhiteSpace(label.GetDisplayName()))
                labelName = label.GetDisplayName().Trim();
        }

        labelName = labelName.Replace("/", "").Replace("\\", "");
        string candidate = labelName;
        int number = 2;
        while (HasChildNamed(candidate))
        {
            candidate = labelName + " " + number;
            number++;
        }

        return candidate;
    }

    private bool HasChildNamed(string childName)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).name == childName)
                return true;
        }

        return false;
    }

    private void EnsureClick(Button button, StoryGotoButton gotoButton)
    {
        if (button == null || gotoButton == null) return;
        if (HasPersistentGoto(button, gotoButton)) return;
        button.onClick.RemoveListener(gotoButton.Goto);
        button.onClick.AddListener(gotoButton.Goto);
    }

    private void BindClicks(List<StoryVariableLockButton> locks)
    {
        if (locks == null) return;
        for (int i = 0; i < locks.Count; i++)
        {
            if (locks[i] == null) continue;
            StoryGotoButton gotoButton = locks[i].GetComponent<StoryGotoButton>();
            EnsureClick(locks[i].GetComponent<Button>(), gotoButton);
        }
    }

    private void ReleaseClicks()
    {
        if (!Application.isPlaying) return;

        List<StoryVariableLockButton> locks = OwnedLocks();
        for (int i = 0; i < locks.Count; i++)
        {
            Button button = locks[i].GetComponent<Button>();
            StoryGotoButton gotoButton = locks[i].GetComponent<StoryGotoButton>();
            if (button == null || gotoButton == null) continue;
            button.onClick.RemoveListener(gotoButton.Goto);
        }
    }

    private static bool HasPersistentGoto(Button button, StoryGotoButton gotoButton)
    {
        int count = button.onClick.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (button.onClick.GetPersistentMethodName(i) != "Goto") continue;
            UnityEngine.Object persistentTarget = button.onClick.GetPersistentTarget(i);
            if (persistentTarget == null || persistentTarget == gotoButton)
                return true;
        }

        return false;
    }

    private static void DestroyOwned(GameObject go)
    {
        if (go == null) return;
        DestroyImmediate(go);
    }

    private void EnsureHover(StoryVariableLockButton lockButton, string message)
    {
        if (lockButton == null) return;
        StoryVisitHoverBalloon hover = lockButton.GetComponent<StoryVisitHoverBalloon>();
        if (hover == null)
            hover = AddHover(lockButton.gameObject);
        if (hover.Owner == this && hover.Message == (message ?? ""))
            return;

        hover.Bind(this, message);
    }

    private static StoryVisitHoverBalloon AddHover(GameObject go)
    {
        return go.AddComponent<StoryVisitHoverBalloon>();
    }

    /// <summary>
    /// 1段目以降のカーソルオーバーで、共通のバルーンに文章を出す。
    /// </summary>
    public void ShowBalloon(StoryVisitHoverBalloon source, string message, RectTransform target)
    {
        if (!Application.isPlaying || source == null || target == null) return;
        if (string.IsNullOrEmpty(message))
        {
            HideBalloon(source);
            return;
        }

        EnsureBalloon();
        balloonSource = source;
        if (balloonMessage != message)
        {
            balloonMessage = message;
            ResizeBalloon(message);
        }

        if (!balloonBody.gameObject.activeSelf)
            balloonBody.gameObject.SetActive(true);
        balloonBody.SetAsLastSibling();
        PlaceBalloon(target);
    }

    /// <summary>
    /// 表示中のバルーンを閉じる。source が null のときは、誰が出していても閉じる。
    /// </summary>
    public void HideBalloon(StoryVisitHoverBalloon source)
    {
        if (source != null && balloonSource != source) return;
        balloonSource = null;
        balloonMessage = null;
        if (balloonBody != null)
            balloonBody.gameObject.SetActive(false);
    }

    private void EnsureBalloon()
    {
        if (balloonBody != null) return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        GameObject body = new GameObject("VisitBalloon", typeof(RectTransform), typeof(Image));
        body.transform.SetParent(canvas.transform, false);
        balloonBody = body.GetComponent<RectTransform>();
        balloonBody.anchorMin = new Vector2(0.5f, 0.5f);
        balloonBody.anchorMax = new Vector2(0.5f, 0.5f);
        balloonBody.pivot = new Vector2(0.5f, 0f);
        Image background = body.GetComponent<Image>();
        background.raycastTarget = false;
        background.sprite = WhiteSprite();
        background.color = balloonBackground;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(balloonBody, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = balloonPadding;
        textRect.offsetMax = -balloonPadding;
        balloonText = textObject.GetComponent<TextMeshProUGUI>();
        balloonText.raycastTarget = false;
        balloonText.alignment = TextAlignmentOptions.TopLeft;
        balloonText.enableWordWrapping = true;
        balloonText.overflowMode = TextOverflowModes.Overflow;

        GameObject arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowObject.transform.SetParent(balloonBody, false);
        RectTransform arrow = arrowObject.GetComponent<RectTransform>();
        arrow.anchorMin = new Vector2(0.5f, 0f);
        arrow.anchorMax = new Vector2(0.5f, 0f);
        arrow.pivot = new Vector2(0.5f, 1f);
        arrow.sizeDelta = new Vector2(16f, 10f);
        arrow.anchoredPosition = Vector2.zero;
        Image arrowImage = arrowObject.GetComponent<Image>();
        arrowImage.raycastTarget = false;
        arrowImage.sprite = WhiteSprite();
        arrowImage.color = balloonBackground;

        body.SetActive(false);
    }

    private static Sprite whiteSprite;

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

    private void ResizeBalloon(string message)
    {
        if (balloonText == null) return;
        if (balloonFont != null)
            balloonText.font = balloonFont;
        balloonText.fontSize = Mathf.Max(1, balloonFontSize);
        balloonText.color = balloonTextColor;
        balloonText.text = message;
        Image background = balloonBody.GetComponent<Image>();
        if (background != null)
            background.color = balloonBackground;
        Transform arrow = balloonBody.Find("Arrow");
        if (arrow != null)
        {
            Image arrowImage = arrow.GetComponent<Image>();
            if (arrowImage != null)
                arrowImage.color = balloonBackground;
        }

        float innerWidth = Mathf.Max(1f, balloonMaxWidth - balloonPadding.x * 2f);
        balloonText.rectTransform.offsetMin = new Vector2(balloonPadding.x, balloonPadding.y);
        balloonText.rectTransform.offsetMax = new Vector2(-balloonPadding.x, -balloonPadding.y);
        Vector2 preferred = balloonText.GetPreferredValues(message, innerWidth, 0f);
        float width = Mathf.Clamp(preferred.x, 1f, innerWidth) + balloonPadding.x * 2f;
        float height = preferred.y + balloonPadding.y * 2f;
        balloonBody.sizeDelta = new Vector2(width, height);
    }

    private void PlaceBalloon(RectTransform target)
    {
        Canvas canvas = balloonBody.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3 top = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMax, 0f));
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, top);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, cam, out Vector2 local))
            return;

        balloonBody.anchorMin = canvasRect.pivot;
        balloonBody.anchorMax = canvasRect.pivot;
        Vector2 pos = local + balloonOffset;
        Rect area = canvasRect.rect;
        float halfW = balloonBody.sizeDelta.x * 0.5f;
        float height = balloonBody.sizeDelta.y;
        pos.x = Mathf.Clamp(pos.x, area.xMin + halfW, area.xMax - halfW);
        pos.y = Mathf.Clamp(pos.y, area.yMin, area.yMax - height);
        balloonBody.anchoredPosition = pos;
    }
}

/// <summary>
/// 1段目以降、カーソルを重ねているあいだバルーンを出す。0段目では出さない。
/// </summary>
public class StoryVisitHoverBalloon : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private StoryVisitButtonPanel owner;
    private string message = "";
    private bool inside;

    public StoryVisitButtonPanel Owner => owner;
    public string Message => message ?? "";

    public void Bind(StoryVisitButtonPanel panel, string nextMessage)
    {
        owner = panel;
        message = nextMessage ?? "";
        if (inside)
            Refresh();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        inside = true;
        Refresh();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inside = false;
        if (owner != null)
            owner.HideBalloon(this);
    }

    void OnDisable()
    {
        inside = false;
        if (owner != null)
            owner.HideBalloon(this);
    }

    void LateUpdate()
    {
        if (!inside) return;
        Refresh();
    }

    private void Refresh()
    {
        if (owner == null) return;
        StoryVariableLockButton lockButton = GetComponent<StoryVariableLockButton>();
        if (lockButton == null || lockButton.ReachedStageCount() < 1 || string.IsNullOrEmpty(Message))
        {
            owner.HideBalloon(this);
            return;
        }

        owner.ShowBalloon(this, Message, transform as RectTransform);
    }
}

#if UNITY_EDITOR
/// <summary>
/// StoryVisitButtonPanel のインスペクタ。1段階目の再生記録と、共通設定を出す。
/// </summary>
[CustomEditor(typeof(StoryVisitButtonPanel))]
public class StoryVisitButtonPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(StoryVisitButtonPanel.Summary, MessageType.Info);
        EditorGUILayout.HelpBox("子ボタンはゲームの実行中にだけ作られます。ここでの編集はリストと共通設定だけで、プレハブ資産にボタンは追加しません。", MessageType.None);

        DrawVisits();
        EditorGUILayout.Space(8f);
        DrawCommon();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawVisits()
    {
        SerializedProperty visits = serializedObject.FindProperty("visits");
        EditorGUILayout.LabelField("1段階目（再生記録）", EditorStyles.boldLabel);

        for (int i = 0; i < visits.arraySize; i++)
        {
            SerializedProperty entry = visits.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((i + 1) + "件目", EditorStyles.boldLabel);
            if (GUILayout.Button("上へ", GUILayout.Width(48f)) && i > 0)
            {
                visits.MoveArrayElement(i, i - 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            if (GUILayout.Button("下へ", GUILayout.Width(48f)) && i < visits.arraySize - 1)
            {
                visits.MoveArrayElement(i, i + 1);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            if (GUILayout.Button("削除", GUILayout.Width(48f)))
            {
                int size = visits.arraySize;
                visits.DeleteArrayElementAtIndex(i);
                if (visits.arraySize == size)
                    visits.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();

            SerializedProperty graph = entry.FindPropertyRelative("visitGraph");
            EditorGUILayout.PropertyField(graph, new GUIContent("StoryGraph", "このグラフのアセット名と Label の組で、再生記録と Goto の行先を決めます。"));
            DrawVisitLabelPopup(graph, entry.FindPropertyRelative("visitLabelGuid"));
            SerializedProperty message = entry.FindPropertyRelative("message");
            EditorGUILayout.LabelField(new GUIContent("メッセージ", "1段目以降、カーソルを重ねたときに出す文章。"));
            string currentMessage = message.stringValue ?? "";
            string nextMessage = EditorGUILayout.TextArea(currentMessage, GUILayout.MinHeight(40f));
            if (nextMessage != currentMessage)
                message.stringValue = nextMessage;
            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("再生記録を追加"))
            visits.arraySize++;
    }

    private void DrawCommon()
    {
        EditorGUILayout.LabelField("共通設定", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("invertCondition"), new GUIContent("条件を反転", "1段階目の Unlock と Lock を、すべてのボタンで入れ替えます。"));
        DrawExtraStages(serializedObject.FindProperty("extraStages"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("material"), new GUIContent("マテリアル"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lockSprite"), new GUIContent("Lock 画像"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("unlockSprite"), new GUIContent("Unlock 画像"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationDuration"), new GUIContent("アニメーション時間"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lockScale"), new GUIContent("Lock スケール"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("unlockScale"), new GUIContent("Unlock スケール"));
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("バルーン", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonFont"), new GUIContent("フォント"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonFontSize"), new GUIContent("フォントサイズ"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonTextColor"), new GUIContent("文字色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonBackground"), new GUIContent("背景色"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonPadding"), new GUIContent("余白"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonOffset"), new GUIContent("位置のずれ", "ボタン上端から、バルーンの下端までのずれ。"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("balloonMaxWidth"), new GUIContent("最大幅"));
    }

    private void DrawExtraStages(SerializedProperty listProperty)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("追加の段階", EditorStyles.boldLabel);
        int next = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("追加の段階数"), listProperty.arraySize));
        if (next != listProperty.arraySize)
        {
            listProperty.arraySize = next;
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
        EditorGUILayout.PropertyField(sourceProperty, new GUIContent("判定", "変数の値か、再生記録か。"));
        bool visit = sourceProperty != null && sourceProperty.enumValueIndex == (int)StoryLockSource.Visit;
        if (!visit)
        {
            SerializedProperty database = stage.FindPropertyRelative("variableDatabase");
            EditorGUILayout.PropertyField(database, new GUIContent("データベース"));
            DrawVariablePopup(database, stage.FindPropertyRelative("targetKey"));
        }
        else
        {
            SerializedProperty graph = stage.FindPropertyRelative("visitGraph");
            EditorGUILayout.PropertyField(graph, new GUIContent("StoryGraph"));
            DrawVisitLabelPopup(graph, stage.FindPropertyRelative("visitLabelGuid"));
        }

        EditorGUILayout.PropertyField(stage.FindPropertyRelative("invertCondition"), new GUIContent("条件を反転"));
        EditorGUILayout.PropertyField(stage.FindPropertyRelative("reachedSprite"), new GUIContent((index + 1) + "段階目までの画像"));
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

        if (keys.Count == 0)
        {
            EditorGUILayout.PropertyField(keyProperty, new GUIContent("対象変数"));
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

        DrawChoicePopup(new GUIContent("対象変数"), keyProperty, keys, labels);
    }

    private void DrawVisitLabelPopup(SerializedProperty graphProperty, SerializedProperty guidProperty)
    {
        StoryGraph graph = graphProperty != null ? graphProperty.objectReferenceValue as StoryGraph : null;
        string current = guidProperty.stringValue ?? "";
        List<string> guids = new List<string>();
        List<string> labels = new List<string>();
        if (graph != null && graph.nodes != null)
        {
            HashSet<string> used = new HashSet<string>();
            for (int i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] is not LabelNode label) continue;
                if (string.IsNullOrEmpty(label.guid) || guids.Contains(label.guid)) continue;
                string display = label.GetDisplayName();
                if (!used.Add(display))
                {
                    string suffix = label.guid.Length > 4 ? label.guid.Substring(0, 4) : label.guid;
                    display = display + " " + suffix;
                    used.Add(display);
                }

                guids.Add(label.guid);
                labels.Add(display);
            }
        }

        if (guids.Count == 0)
        {
            EditorGUILayout.HelpBox("StoryGraph を指定すると、その Label から選べます。", MessageType.Info);
            return;
        }

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

        DrawChoicePopup(new GUIContent("対象ラベル"), guidProperty, guids, labels);
    }

    private static void DrawChoicePopup(GUIContent label, SerializedProperty valueProperty, List<string> values, List<string> labels)
    {
        string current = valueProperty.stringValue ?? "";
        int index = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == current)
            {
                index = i;
                break;
            }
        }

        int next = EditorGUILayout.Popup(label, index, labels.ToArray());
        if (next >= 0 && next < values.Count && values[next] != current)
            valueProperty.stringValue = values[next];
    }
}
#endif
