using System;

/// <summary>
/// グラフ編集の Undo 開始と終了を、ノード側からエディタへ伝える窓口。
/// </summary>
public static class StoryGraphEditorHooks
{
    public static bool IgnoreGraphViewChange;
    public static Action<string> BeginGraphEdit;
    public static Action EndGraphEdit;
}
