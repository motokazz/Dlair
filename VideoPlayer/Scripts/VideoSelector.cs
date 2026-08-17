using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.Events;
using TMPro;

[System.Serializable]
public class GallerySaveData
{
    public List<string> unlockedIds = new List<string>();
}

[System.Serializable]
public class CustomEventTrigger : UnityEvent<MediaPlaylist.MediaData> { }

public class VideoSelector : MonoBehaviour
{
    public enum PlaybackMode { Manual, GalleryAuto, Interactive }

    [Header("Player Reference")]
    public DualVideoPlayer dualPlayer;
    public MediaPlaylist playlistAsset;
    public VideoClip dummyClip;

    [Header("Playback Settings")]
    public PlaybackMode currentMode = PlaybackMode.Manual;
    public bool loopPlaylist = true;

    [Header("Gallery UI")]
    public GameObject buttonPrefab;
    public Transform buttonContainer;

    [Header("Events")]
    public CustomEventTrigger onCustomEventTriggered;

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

            // ★修正：DetermineInitialLoopを削除し、直接 firstData.isLooping を渡す！
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

        // ★修正：純粋に firstData.isLooping を渡すだけ！
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

    private void LoadSaveData() { try { if (File.Exists(saveFilePath)) { saveData = JsonUtility.FromJson<GallerySaveData>(File.ReadAllText(saveFilePath)); if (saveData == null) saveData = new GallerySaveData(); } else saveData = new GallerySaveData(); } catch { saveData = new GallerySaveData(); } }
    private void SaveDataToFile() { try { File.WriteAllText(saveFilePath, JsonUtility.ToJson(saveData, true)); } catch { } }

    public bool IsUnlocked(int index) { if (index < 0 || index >= playlistAsset.items.Length) return false; var data = playlistAsset.items[index]; if (data.isUnlockedByDefault) return true; string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId; return saveData.unlockedIds.Contains(id); }
    public void UnlockMedia(int index) { if (index < 0 || index >= playlistAsset.items.Length) return; var data = playlistAsset.items[index]; string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId; if (!saveData.unlockedIds.Contains(id)) { saveData.unlockedIds.Add(id); SaveDataToFile(); } RefreshButtons(); }
    public void ResetAllUnlocks() { saveData.unlockedIds.Clear(); SaveDataToFile(); RefreshButtons(); }

    private int GetFirstUnlockedIndex() { for (int i = 0; i < playlistAsset.items.Length; i++) { if (IsUnlocked(i)) return i; } return -1; }

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
            TMP_Text tmpTitleText = btnObj.GetComponentInChildren<TMP_Text>();
            if (tmpTitleText != null) tmpTitleText.text = isUnlocked ? data.title : "???";
            Image thumbImage = btnObj.GetComponent<Image>();
            if (thumbImage != null && data.thumbnail != null) { thumbImage.sprite = data.thumbnail; thumbImage.color = isUnlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f); }
            Button btn = btnObj.GetComponent<Button>();
            if (btn != null) btn.interactable = isUnlocked;
        }
    }

    public void OnVideoSelectButtonClicked(int index) { if (index < 0 || index >= playlistAsset.items.Length) return; if (index == currentIndex) return; if (!IsUnlocked(index)) return; if (dualPlayer.IsTransitioning) { pendingIndex = index; return; } ExecutePlayVideo(index); }

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

        // ★修正：純粋に selectedData.isLooping を渡すだけ！
        dualPlayer.RequestPlayNextMedia(selectedData.isStaticImage, selectedData.clip, selectedData.image, selectedData.isLooping, selectedData.audioClip, fadeDuration);

        StartPlaybackMonitor();
    }

    private void StartPlaybackMonitor() { StopPlaybackMonitor(); playbackMonitorCoroutine = StartCoroutine(PlaybackMonitorRoutine()); }
    private void StopPlaybackMonitor() { if (playbackMonitorCoroutine != null) { StopCoroutine(playbackMonitorCoroutine); playbackMonitorCoroutine = null; } }

    private IEnumerator PlaybackMonitorRoutine()
    {
        while (dualPlayer.IsTransitioning) yield return null;
        MediaPlaylist.MediaData currentData = playlistAsset.items[currentIndex];

        float currentOverlap = currentData.overrideCrossfade ? currentData.customCrossfadeDuration : playlistAsset.defaultCrossfadeDuration;
        float triggerTime = Mathf.Max(currentOverlap, 0.1f);

        // ★ 0より大きい数字が設定されているかチェック
        bool useCustomTriggerTime = currentData.eventTriggerTime > 0f;

        if (currentData.isStaticImage)
        {
            // カスタム時間が設定されていればそれを使い、0なら従来の終端計算を使う
            float waitTime = useCustomTriggerTime ? currentData.eventTriggerTime : Mathf.Max(0, currentData.imageDuration - triggerTime);
            yield return new WaitForSeconds(waitTime);
            CheckBranchesOrPlayNext(currentData);
        }
        else
        {
            VideoPlayer activePlayer = dualPlayer.GetActivePlayer();
            if (activePlayer == null) yield break;
            while (activePlayer.length <= 0) yield return null;

            double totalTime = activePlayer.length;
            bool hasTriggered = false;
            float customTimer = 0f; // ★動画開始からの実時間を測るタイマー

            while (!hasTriggered)
            {
                if (dualPlayer.IsTransitioning) yield break;

                double currentTime = activePlayer.time;

                // ==========================================
                // 【A】指定された時間（秒）で発火させるモード
                // ==========================================
                if (useCustomTriggerTime)
                {
                    customTimer += Time.deltaTime; // 動画がループしようが関係なく秒数を数える
                    if (customTimer >= currentData.eventTriggerTime)
                    {
                        hasTriggered = true;
                        CheckBranchesOrPlayNext(currentData);
                        yield break;
                    }
                }
                // ==========================================
                // 【B】今まで通り、動画の終端（フェード開始タイミング）で発火させるモード
                // ==========================================
                else
                {
                    if (currentTime > 0.1f && (totalTime - currentTime) <= triggerTime)
                    {
                        hasTriggered = true;
                        CheckBranchesOrPlayNext(currentData);
                        yield break;
                    }

                    if (currentTime > (totalTime / 2.0) && !activePlayer.isPlaying)
                    {
                        hasTriggered = true;
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
        if (currentMode == PlaybackMode.Manual) return;

        if (currentMode == PlaybackMode.Interactive)
        {
            if (currentData.eventId != "None")
            {
                if (currentData.eventId == "AutoBranchController") // ★ついでに名前を合わせました
                {
                    if (currentData.choices != null && currentData.choices.Length > 0) ProceedToNextTarget(currentData.choices[0].targetId);
                    else PlayNextInPlaylist();
                    return;
                }

                // ★削除: dualPlayer.SetWaitMode(currentData.loopWhileWaiting); を消去（もう既にループ設定で回っているため不要！）

                if (onCustomEventTriggered != null)
                {
                    onCustomEventTriggered.Invoke(currentData);
                }
                return;
            }
        }
        PlayNextInPlaylist();
    }

    public void ProceedToNextTarget(string targetId)
    {
        int targetIndex = -1;
        for (int i = 0; i < playlistAsset.items.Length; i++)
        {
            var data = playlistAsset.items[i];
            string id = string.IsNullOrEmpty(data.unlockId) ? data.title : data.unlockId;
            if (id == targetId) { targetIndex = i; break; }
        }
        if (targetIndex != -1) { UnlockMedia(targetIndex); if (dualPlayer.IsTransitioning) pendingIndex = targetIndex; else ExecutePlayVideo(targetIndex); }
        else Debug.LogWarning($"分岐先の動画が見つかりません。Target ID: {targetId}");
    }

    public void PlayNextInPlaylist()
    {
        int nextIndex = currentIndex;
        int attempts = 0;
        while (attempts < playlistAsset.items.Length)
        {
            nextIndex++;
            if (nextIndex >= playlistAsset.items.Length) { if (loopPlaylist) nextIndex = 0; else return; }
            if (IsUnlocked(nextIndex)) { ExecutePlayVideo(nextIndex); return; }
            attempts++;
        }
    }
}