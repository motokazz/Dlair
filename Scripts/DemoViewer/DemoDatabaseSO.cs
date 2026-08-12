using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// 1つのデモが持つ情報
[System.Serializable]
public class DemoData
{
    public string demoID;          // 例: "demo_001"
    public string title;
    public Sprite thumbnail;
    public VideoClip videoClip;
}

// プロジェクトウィンドウから作成できるアセットの定義
[CreateAssetMenu(fileName = "NewDemoDatabase", menuName = "Custom/Demo Database")]
public class DemoDatabaseSO : ScriptableObject
{
    public List<DemoData> demoList = new List<DemoData>();
}