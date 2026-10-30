using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

/// <summary>
/// ギャラリーの解放状態。解放済みメディアの ID を保存する。
/// </summary>
[System.Serializable]
public class GallerySaveData
{
    public List<string> unlockedIds = new List<string>();
}

/// <summary>
/// プレイリストのメディアを選び、DualVideoPlayer で再生する。
/// 解放状態を保存し、ギャラリーのボタン表示と自動進行を行う。
/// </summary>
public class PlaylistVideoPlayer : MonoBehaviour
{
    /// <summary>
    /// 再生の進め方。手動選択か、終了後に次の解放済みメディアへ進むか。
    /// </summary>
    public enum PlaybackMode { Manual, GalleryAuto }

    [Header("Player Reference")]
    public DualVideoPlayer dualPlayer;
    [Tooltip("ギャラリーとして表示・再生する MediaPlaylist をセットします")]
    public MediaPlaylist playlistAsset;
    public VideoClip dummyClip;

    [Header("Playback Settings")]
    [Tooltip("Manual: タップした動画のみ再生 / GalleryAuto: 終わったら次の動画へ自動進行")]
    public PlaybackMode currentMode = PlaybackMode.GalleryAuto;
    public bool loopPlaylist = true;

    [Header("Gallery UI")]
    public GameObject buttonPrefab;
    public Transform buttonContainer;

    private int currentIndex = -1;
    private int pendingIndex = -1;
    private Coroutine playbackMonitorCoroutine;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private GallerySaveData saveData = new GallerySaveData();
    private string saveFilePath;

    private void Awake()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "GallerySave.json");
        LoadSaveData();
    }

    private void Start()
    {
        if (playlistAsset == null || playlistAsset.items.Length == 0) return;

        GenerateButtons();

        int firstIndex = GetFirstUnlockedIndex();
        if (firstIndex == -1) return;

        if (dummyClip != null) StartCoroutine(StartupSequence(firstIndex));
        else
        {
            currentIndex = firstIndex;
            MediaPlaylist.MediaData firstData = playlistAsset.items[currentIndex];
            dualPlayer.PlayFirstMedia(firstData.isStaticImage, firstData.clip, firstData.image, firstData.isLooping, firstData.audioClip);
            StartPlaybackMonitor();
        }
    }

    private IEnumerator StartupSequence(int firstIndex)
    {
        yield return dualPlayer.WarmUpDecoder(dummyClip);
        float fadeDuration = playlistAsset.defaultCrossfadeDuration;
        currentIndex = firstIndex;
        MediaPlaylist.MediaData firstData = playlistAsset.items[currentIndex];

        dualPlayer.RequestPlayNextMedia(firstData.isStaticImage, firstData.clip, firstData.image, firstData.isLooping, firstData.audioClip, fadeDuration);
        StartPlaybackMonitor();
    }

    private void Update()
    {
        if (!dualPlayer.IsTransitioning && pendingIndex != -1)
        {
            int indexToPlay = pendingIndex;
            pendingIndex = -1;
            ExecutePlayVideo(indexToPlay);
        }
    }

    // ==========================================
    // セーブ＆ロード・アンロック管理
    // ==========================================
    private void LoadSaveData()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                saveData = JsonUtility.FromJson<GallerySaveData>(File.ReadAllText(saveFilePath));
                if (saveData == null) saveData = new GallerySaveData();
            }
            else saveData = new GallerySaveData();
        }
        catch { saveData = new GallerySaveData(); }
    }

    private void SaveDataToFile()
    {
        try { File.WriteAllText(saveFilePath, JsonUtility.ToJson(saveData, true)); }
        catch { }
    }

    /// <summary>
    /// 指定インデックスのメディアが再生できるか。初期解放、またはセーブ済み ID に含まれるとき true。
    /// </summary>
    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= playlistAsset.items.Length) return false;
        var data = playlistAsset.items[index];
        if (data.isUnlockedByDefault) return true;
        string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId;
        return saveData.unlockedIds.Contains(id);
    }

    /// <summary>
    /// 指定インデックスのメディアを解放し、セーブしてボタン表示を更新する。
    /// </summary>
    public void UnlockMedia(int index)
    {
        if (index < 0 || index >= playlistAsset.items.Length) return;
        var data = playlistAsset.items[index];
        string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId;

        if (!saveData.unlockedIds.Contains(id))
        {
            saveData.unlockedIds.Add(id);
            SaveDataToFile();
        }
        RefreshButtons();
    }

    /// <summary>
    /// 追加解放をすべて消し、初期解放だけの状態に戻す。
    /// </summary>
    public void ResetAllUnlocks()
    {
        saveData.unlockedIds.Clear();
        SaveDataToFile();
        RefreshButtons();
    }

    private int GetFirstUnlockedIndex()
    {
        for (int i = 0; i < playlistAsset.items.Length; i++) { if (IsUnlocked(i)) return i; }
        return -1;
    }

    // ==========================================
    // UI（ボタン）生成と更新
    // ==========================================
    private void GenerateButtons()
    {
        foreach (var b in spawnedButtons) Destroy(b);
        spawnedButtons.Clear();

        for (int i = 0; i < playlistAsset.items.Length; i++)
        {
            int index = i;
            GameObject btnObj = Instantiate(buttonPrefab, buttonContainer);
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => OnVideoSelectButtonClicked(index));
            spawnedButtons.Add(btnObj);
        }
        RefreshButtons();
    }

    /// <summary>
    /// 解放状態に合わせて、ボタンのタイトル・サムネイル・操作可否を更新する。
    /// </summary>
    public void RefreshButtons()
    {
        for (int i = 0; i < playlistAsset.items.Length; i++)
        {
            if (i >= spawnedButtons.Count) break;
            GameObject btnObj = spawnedButtons[i];
            MediaPlaylist.MediaData data = playlistAsset.items[i];
            bool isUnlocked = IsUnlocked(i);

            btnObj.name = $"VideoBtn_{i:00}_{data.title}";

            Text titleText = btnObj.GetComponentInChildren<Text>();
            if (titleText != null) titleText.text = isUnlocked ? data.title : "???";

            TMP_Text tmpTitleText = btnObj.GetComponentInChildren<TMP_Text>();
            if (tmpTitleText != null) tmpTitleText.text = isUnlocked ? data.title : "???";

            Image thumbImage = btnObj.GetComponent<Image>();
            if (thumbImage != null && data.thumbnail != null)
            {
                thumbImage.sprite = data.thumbnail;
                thumbImage.color = isUnlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);
            }

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null) btn.interactable = isUnlocked;
        }
    }

    /// <summary>
    /// ギャラリーのボタンからメディアを選ぶ。遷移中なら、終了後に再生する。
    /// </summary>
    public void OnVideoSelectButtonClicked(int index)
    {
        if (index < 0 || index >= playlistAsset.items.Length) return;
        if (index == currentIndex) return;
        if (!IsUnlocked(index)) return;

        if (dualPlayer.IsTransitioning)
        {
            pendingIndex = index;
            return;
        }
        ExecutePlayVideo(index);
    }

    // ==========================================
    // 再生と自動進行ロジック
    // ==========================================
    private void ExecutePlayVideo(int index)
    {
        pendingIndex = -1;
        StopPlaybackMonitor();

        float fadeDuration = playlistAsset.defaultCrossfadeDuration;
        if (currentIndex >= 0 && currentIndex < playlistAsset.items.Length)
        {
            MediaPlaylist.MediaData previousData = playlistAsset.items[currentIndex];
            fadeDuration = previousData.overrideCrossfade ? previousData.customCrossfadeDuration : playlistAsset.defaultCrossfadeDuration;
        }

        currentIndex = index;
        MediaPlaylist.MediaData selectedData = playlistAsset.items[currentIndex];

        dualPlayer.RequestPlayNextMedia(selectedData.isStaticImage, selectedData.clip, selectedData.image, selectedData.isLooping, selectedData.audioClip, fadeDuration);

        StartPlaybackMonitor();
    }

    private void StartPlaybackMonitor()
    {
        StopPlaybackMonitor();
        playbackMonitorCoroutine = StartCoroutine(PlaybackMonitorRoutine());
    }

    private void StopPlaybackMonitor()
    {
        if (playbackMonitorCoroutine != null)
        {
            StopCoroutine(playbackMonitorCoroutine);
            playbackMonitorCoroutine = null;
        }
    }

    private IEnumerator PlaybackMonitorRoutine()
    {
        while (dualPlayer.IsTransitioning) yield return null;
        MediaPlaylist.MediaData currentData = playlistAsset.items[currentIndex];

        float currentOverlap = currentData.overrideCrossfade ? currentData.customCrossfadeDuration : playlistAsset.defaultCrossfadeDuration;
        float triggerTime = Mathf.Max(currentOverlap, 0.1f);

        if (currentData.isStaticImage)
        {
            float waitTime = Mathf.Max(0, currentData.imageDuration - triggerTime);
            yield return new WaitForSeconds(waitTime);
            OnMediaFinished();
        }
        else
        {
            VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
            if (activePlayer == null) yield break;
            while (activePlayer.length <= 0) yield return null;

            double totalTime = activePlayer.length;
            bool hasTriggered = false;

            while (!hasTriggered)
            {
                if (dualPlayer.IsTransitioning) yield break;

                double currentTime = activePlayer.time;

                // 動画終端での次遷移
                if (currentTime > 0.1f && (totalTime - currentTime) <= triggerTime)
                {
                    hasTriggered = true;
                    OnMediaFinished();
                    yield break;
                }

                // 再生が止まった場合の保険
                if (currentTime > (totalTime / 2.0) && !activePlayer.isPlaying)
                {
                    hasTriggered = true;
                    OnMediaFinished();
                    yield break;
                }

                yield return null;
            }
        }
    }

    private void OnMediaFinished()
    {
        if (currentMode == PlaybackMode.Manual) return;
        if (currentMode == PlaybackMode.GalleryAuto)
        {
            PlayNextInPlaylist();
        }
    }

    /// <summary>
    /// 次の解放済みメディアを再生する。末尾のあとは loopPlaylist に従う。
    /// </summary>
    public void PlayNextInPlaylist()
    {
        int nextIndex = currentIndex;
        int attempts = 0;

        while (attempts < playlistAsset.items.Length)
        {
            nextIndex++;
            if (nextIndex >= playlistAsset.items.Length)
            {
                if (loopPlaylist) nextIndex = 0;
                else return;
            }

            if (IsUnlocked(nextIndex))
            {
                ExecutePlayVideo(nextIndex);
                return;
            }
            attempts++;
        }
    }
}