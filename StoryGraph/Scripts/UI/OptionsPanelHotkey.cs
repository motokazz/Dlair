using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 割り当てたキーやマウスボタンでオプション画面を開閉する。
/// 説明は OptionsPanelHotkey.Summary。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Options Panel Hotkey")]
public class OptionsPanelHotkey : MonoBehaviour
{
    public const string Summary =
        "割り当てた入力でオプション画面を開閉します。StoryPlayer は止めません。\n" +
        "開く入力と閉じる入力は別々に設定します。同じ入力を両方に入れると、その入力で開いて閉じます。\n" +
        "初期値は右クリックと Escape です。\n" +
        "別のオブジェクトを指定したときは、その表示だけを切り替えます。\n" +
        "空、自分自身、または自分の親を指定したときは、オブジェクトを無効にせず CanvasGroup で隠します。\n" +
        "文字入力中はキーを無視します。マウスの割り当ては有効です。";

    [Tooltip("開閉する対象。空ならこのオブジェクト")]
    public GameObject panel;

    [Tooltip("開始時に閉じた状態にする")]
    public bool startClosed = true;

    [Tooltip("パネルが閉じているときに押すと開く")]
    public List<OptionsPanelHotkeyBinding> openBindings = DefaultBindings();

    [Tooltip("パネルが開いているときに押すと閉じる")]
    public List<OptionsPanelHotkeyBinding> closeBindings = DefaultBindings();

    CanvasGroup canvasGroup;
    bool hideWithGroup;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Summary と同じ説明を返します。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    void Awake()
    {
        GameObject target = Target;
        hideWithGroup = target == gameObject || transform.IsChildOf(target.transform);
        if (hideWithGroup)
        {
            canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = target.AddComponent<CanvasGroup>();
        }

        SetOpen(!startClosed);
    }

    void Update()
    {
        List<OptionsPanelHotkeyBinding> bindings = IsOpen ? closeBindings : openBindings;
        if (!WasAnyPressed(bindings))
            return;

        SetOpen(!IsOpen);
    }

    public void Open()
    {
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        GameObject target = Target;

        if (hideWithGroup)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = open ? 1f : 0f;
            canvasGroup.interactable = open;
            canvasGroup.blocksRaycasts = open;
            return;
        }

        if (target != null && target.activeSelf != open)
            target.SetActive(open);
    }

    GameObject Target => panel != null ? panel : gameObject;

    bool WasAnyPressed(List<OptionsPanelHotkeyBinding> bindings)
    {
        if (bindings == null)
            return false;

        for (int i = 0; i < bindings.Count; i++)
        {
            if (WasPressed(bindings[i]))
                return true;
        }

        return false;
    }

    static bool WasPressed(OptionsPanelHotkeyBinding binding)
    {
        if (binding == null)
            return false;

        if (binding.kind == OptionsPanelInputKind.Key)
        {
            if (IsTextInputFocused() || binding.key == Key.None)
                return false;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[binding.key].wasPressedThisFrame;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;

        switch (binding.mouseButton)
        {
            case OptionsPanelMouseButton.Left:
                return mouse.leftButton.wasPressedThisFrame;
            case OptionsPanelMouseButton.Right:
                return mouse.rightButton.wasPressedThisFrame;
            default:
                return mouse.middleButton.wasPressedThisFrame;
        }
    }

    static bool IsTextInputFocused()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;

        InputField field = selected.GetComponent<InputField>();
        if (field != null && field.isFocused)
            return true;

        TMP_InputField tmpField = selected.GetComponent<TMP_InputField>();
        return tmpField != null && tmpField.isFocused;
    }

    static List<OptionsPanelHotkeyBinding> DefaultBindings()
    {
        return new List<OptionsPanelHotkeyBinding>
        {
            new OptionsPanelHotkeyBinding
            {
                kind = OptionsPanelInputKind.Mouse,
                mouseButton = OptionsPanelMouseButton.Right
            },
            new OptionsPanelHotkeyBinding
            {
                kind = OptionsPanelInputKind.Key,
                key = Key.Escape
            }
        };
    }
}

public enum OptionsPanelInputKind
{
    [InspectorName("キー")]
    Key,
    [InspectorName("マウス")]
    Mouse
}

public enum OptionsPanelMouseButton
{
    [InspectorName("左")]
    Left,
    [InspectorName("右")]
    Right,
    [InspectorName("中")]
    Middle
}

[Serializable]
public class OptionsPanelHotkeyBinding
{
    [Tooltip("この入力の種類")]
    public OptionsPanelInputKind kind = OptionsPanelInputKind.Key;

    [Tooltip("種類がキーのときに使うキー")]
    public Key key = Key.Escape;

    [Tooltip("種類がマウスのときに使うボタン")]
    public OptionsPanelMouseButton mouseButton = OptionsPanelMouseButton.Right;
}
