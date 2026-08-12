using System.Collections.Generic;
using System.IO;
using UnityEngine;

// JSONに変換するデータ構造
[System.Serializable]
public class DemoSaveData
{
    // 解放済みのデモIDのリスト
    public List<string> unlockedDemoIDs = new List<string>();
}

// セーブ・ロードを行う静的クラス
public static class DemoSaveManager
{
    // 保存先のパス (各OSの適切なセーブデータ保存領域が自動で選ばれます)
    private static string SavePath => Application.persistentDataPath + "/demo_save.json";

    // ロード処理
    public static DemoSaveData Load()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            return JsonUtility.FromJson<DemoSaveData>(json);
        }
        // ファイルがない場合（初回プレイ時など）は新規作成
        return new DemoSaveData();
    }

    // セーブ処理
    public static void Save(DemoSaveData data)
    {
        // trueにするとJSONが改行・インデントされ人間が読みやすくなります（デバッグに便利）
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }

    // 本編から呼び出す用の便利メソッド：「指定したIDを解放して保存」
    public static void UnlockDemo(string demoID)
    {
        DemoSaveData data = Load();

        // まだリストに無ければ追加してセーブ
        if (!data.unlockedDemoIDs.Contains(demoID))
        {
            data.unlockedDemoIDs.Add(demoID);
            Save(data);
            Debug.Log($"デモ解放＆セーブ完了: {demoID}\n保存先: {SavePath}");
        }
    }

    public static void ClearData()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        Debug.Log("セーブデータを完全にリセットしました。");
    }

}

