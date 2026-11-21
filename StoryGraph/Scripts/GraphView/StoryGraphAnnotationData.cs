using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// グラフ上のグループの位置、タイトル、折りたたみ、中に入っているノードと付箋。
/// </summary>
[Serializable]
public class StoryGroupData
{
    public string guid;
    public string title = "Group";
    public Rect position;
    public List<string> nodeGuids = new List<string>();
    public List<string> stickyNoteGuids = new List<string>();

    // 古いデータにこの項目が無いときは false になり、グループは展開したまま読み込まれる。
    public bool collapsed;
}

/// <summary>
/// グラフ上の付箋の位置、タイトル、本文、文字サイズ、色。
/// </summary>
[Serializable]
public class StoryStickyNoteData
{
    public string guid;
    public string title = "Memo";
    public string contents = "";
    public Rect position;
    public int fontSize = 1;
    public int theme;
}
