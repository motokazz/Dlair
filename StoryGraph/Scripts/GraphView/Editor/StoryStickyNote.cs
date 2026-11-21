using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

/// <summary>
/// グラフ上のメモ。グループへ入れられ、内容の変更を保存側へ知らせる。
/// </summary>
public class StoryStickyNote : StickyNote
{
    public string guid;
    public Action Changed;

    public StoryStickyNote()
    {
        // 標準の付箋には Groupable が無く、グループへドラッグしても入れられない。
        capabilities |= Capabilities.Groupable;
        RegisterCallback<ChangeEvent<string>>(_ => Changed?.Invoke());
        RegisterCallback<FocusOutEvent>(_ => Changed?.Invoke(), TrickleDown.TrickleDown);
    }

    public override void OnResized()
    {
        base.OnResized();
        Changed?.Invoke();
    }
}
