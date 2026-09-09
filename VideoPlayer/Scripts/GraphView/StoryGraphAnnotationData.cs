using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryGroupData
{
    public string guid;
    public string title = "Group";
    public Rect position;
    public List<string> nodeGuids = new List<string>();
    public List<string> stickyNoteGuids = new List<string>();
}

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
