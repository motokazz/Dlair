using UnityEngine;

public class DemoViewerManager : MonoBehaviour
{
    [Header("Master Data")]
    public DemoDatabaseSO databaseSO;       // 作成したScriptableObjectをアタッチ

    [Header("UI References")]
    public GameObject thumbnailPrefab;
    public Transform contentTransform;
    public VideoNodePlayerDouble player;

    void Start()
    {
        GenerateThumbnails();
    }

    public void GenerateThumbnails()
    {
        // 1. JSONからセーブデータをロード
        DemoSaveData saveData = DemoSaveManager.Load();

        // Contentの中身をクリア
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }

        // 2. ScriptableObjectのリストを元にUIを生成
        foreach (var data in databaseSO.demoList)
        {
            GameObject nodeObj = Instantiate(thumbnailPrefab, contentTransform);
            ThumbnailNode node = nodeObj.GetComponent<ThumbnailNode>();

            // 3. セーブデータ(JSON)の中に、このデモIDが含まれているかチェック
            bool isUnlocked = saveData.unlockedDemoIDs.Contains(data.demoID);

            // ノードを初期化
            node.Setup(data, isUnlocked, OnThumbnailClicked);
            Debug.Log(data.videoClip.name);
        }
    }

    void OnThumbnailClicked(DemoData data)
    {
        Debug.Log("再生開始: " + data.title);
        // 1. dataそのものが空か？
        if (data == null)
        {
            Debug.LogError("【原因】data自体が届いていません。");
            return;
        }

        // 2. videoClipが空か？
        if (data.videoClip == null)
        {
            Debug.LogError($"【原因】{data.title} の videoClip 欄がインスペクターで空っぽです。");
            return;
        }

        // 3. player(再生機)が空か？
        if (player == null)
        {
            Debug.LogError("【原因】DemoViewerManagerにある 'player' 変数に、再生用コンポーネントがアタッチされていません。");
            return;
        }



        Debug.Log(data.videoClip.name);
        // 再生処理をここに記述
        player.PlayClipPure(data.videoClip);
    }
}