using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

public class StoryStickyNote : StickyNote
{
    public string guid;
    public Action Changed;

    public StoryStickyNote()
    {
        RegisterCallback<ChangeEvent<string>>(_ => Changed?.Invoke());
        RegisterCallback<FocusOutEvent>(_ => Changed?.Invoke(), TrickleDown.TrickleDown);
    }

    public override void OnResized()
    {
        base.OnResized();
        Changed?.Invoke();
    }
}
