using System;

public static class StoryGraphEditorHooks
{
    public static bool IgnoreGraphViewChange;
    public static Action<string> BeginGraphEdit;
    public static Action EndGraphEdit;
}
