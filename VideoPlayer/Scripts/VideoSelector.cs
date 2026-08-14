using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

[System.Serializable]
public class GallerySaveData
{
    public List<string> unlockedIds = new List<string>();
}

public class VideoSelector : MonoBehaviour
{
    public enum PlaybackMode
    {
        Gallery,
        Interactive
    }

    [Header("Player Reference")]
    public DualVideoPlayer dualPlayer;

    [Header("Playlist Asset")]
    public MediaPlaylist playlistAsset;

    [Header("Warm Up")]
    public VideoClip dummyClip;

    [Header("Playback Settings")]
    public PlaybackMode currentMode = PlaybackMode.Gallery;
    public bool autoPlayNext = true;
    public bool loopPlaylist = true;
    public float overlapTime = 1.0f;

    [Header("Gallery UI")]
    public GameObject buttonPrefab;
    public Transform buttonContainer;

    [Header("Interactive Branch UI")]
    public GameObject branchPanel;
    public GameObject branchButtonPrefab;
    public Transform branchButtonContainer;

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
        if (branchPanel != null) branchPanel.SetActive(false);
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
        currentIndex = firstIndex;
        MediaPlaylist.MediaData firstData = playlistAsset.items[currentIndex];
        dualPlayer.RequestPlayNextMedia(firstData.isStaticImage, firstData.clip, firstData.image, firstData.isLooping, firstData.audioClip);
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

    private void LoadSaveData()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                saveData = JsonUtility.FromJson<GallerySaveData>(json);
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

    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= playlistAsset.items.Length) return false;
        var data = playlistAsset.items[index];
        if (data.isUnlockedByDefault) return true;
        string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId;
        return saveData.unlockedIds.Contains(id);
    }

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

    public void ResetAllUnlocks()
    {
        saveData.unlockedIds.Clear();
        SaveDataToFile();
        RefreshButtons();
    }

    private int GetFirstUnlockedIndex()
    {
        for (int i = 0; i < playlistAsset.items.Length; i++)
        {
            if (IsUnlocked(i)) return i;
        }
        return -1;
    }

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

    public void OnVideoSelectButtonClicked(int index)
    {
        if (index < 0 || index >= playlistAsset.items.Length) return;
        if (index == currentIndex) return;
        if (!IsUnlocked(index)) return;

        if (branchPanel != null) branchPanel.SetActive(false);

        if (dualPlayer.IsTransitioning)
        {
            pendingIndex = index;
            return;
        }
        ExecutePlayVideo(index);
    }

    private void ExecutePlayVideo(int index)
    {
        pendingIndex = -1;
        StopPlaybackMonitor();

        currentIndex = index;
        MediaPlaylist.MediaData selectedData = playlistAsset.items[currentIndex];

        dualPlayer.RequestPlayNextMedia(selectedData.isStaticImage, selectedData.clip, selectedData.image, selectedData.isLooping, selectedData.audioClip);
        StartPlaybackMonitor();
    }

    private void StartPlaybackMonitor()
    {
        StopPlaybackMonitor();
        if (autoPlayNext)
        {
            playbackMonitorCoroutine = StartCoroutine(PlaybackMonitorRoutine());
        }
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

        if (currentData.isStaticImage)
        {
            float timer = 0f;
            while (true)
            {
                if (dualPlayer.IsTransitioning) yield break;

                timer += Time.deltaTime;
                float timeRemaining = currentData.imageDuration - timer;

                if (timeRemaining <= overlapTime)
                {
                    CheckBranchesOrPlayNext(currentData);
                    yield break;
                }
                yield return null;
            }
        }
        else
        {
            VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
            if (activePlayer == null) yield break;

            while (activePlayer.length <= 0) yield return null;
            double totalTime = activePlayer.length;

            while (true)
            {
                if (dualPlayer.IsTransitioning) yield break;

                double currentTime = activePlayer.time;
                if (currentTime > 0.1f && currentTime <= totalTime)
                {
                    double timeRemaining = totalTime - currentTime;
                    if (timeRemaining <= overlapTime)
                    {
                        CheckBranchesOrPlayNext(currentData);
                        yield break;
                    }
                }
                yield return null;
            }
        }
    }

    private void CheckBranchesOrPlayNext(MediaPlaylist.MediaData currentData)
    {
        if (currentMode == PlaybackMode.Interactive && currentData.choices != null && currentData.choices.Length > 0)
        {
            // ★ 分岐待ちに入るタイミングで、DualVideoPlayer側のループ設定を強制上書きする！
            dualPlayer.SetWaitMode(currentData.loopWhileWaiting);

            ShowBranchChoices(currentData.choices);
        }
        else
        {
            PlayNextInPlaylist();
        }
    }

    private void ShowBranchChoices(MediaPlaylist.BranchChoice[] choices)
    {
        if (branchPanel == null) return;

        branchPanel.SetActive(true);

        foreach (Transform child in branchButtonContainer) Destroy(child.gameObject);

        foreach (var choice in choices)
        {
            GameObject btnObj = Instantiate(branchButtonPrefab, branchButtonContainer);

            // ★従来のTextを使っている場合
            Text txt = btnObj.GetComponentInChildren<Text>();
            if (txt != null) txt.text = choice.buttonText;

            // ★TextMeshProを使っている場合
            TMP_Text tmpTxt = btnObj.GetComponentInChildren<TMP_Text>();
            if (tmpTxt != null) tmpTxt.text = choice.buttonText;

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                string targetId = choice.targetId;
                btn.onClick.AddListener(() => OnBranchSelected(targetId));
            }
        }
    }
    private void OnBranchSelected(string targetId)
    {
        if (branchPanel != null) branchPanel.SetActive(false);

        int targetIndex = -1;
        for (int i = 0; i < playlistAsset.items.Length; i++)
        {
            var data = playlistAsset.items[i];
            string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId;
            if (id == targetId)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex != -1)
        {
            UnlockMedia(targetIndex);

            if (dualPlayer.IsTransitioning) pendingIndex = targetIndex;
            else ExecutePlayVideo(targetIndex);
        }
        else
        {
            Debug.LogError("分岐先の動画が見つかりません。Target ID: " + targetId);
        }
    }

    private void PlayNextInPlaylist()
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