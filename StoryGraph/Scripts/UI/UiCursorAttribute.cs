using UnityEngine;

/// <summary>
/// ボタンに1つ付ける。名前が UiCursorController の属性と一致したとき、そのカーソル画像になる。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("UI/Ui Cursor Attribute")]
public class UiCursorAttribute : MonoBehaviour
{
    [Tooltip("UiCursorController に登録した属性名。空、または一覧に無いときは共通のボタンオーバー画像")]
    public string attributeName;
}
