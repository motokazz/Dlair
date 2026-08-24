using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "NewMediaPlaylist", menuName = "VideoPlayer/Media Playlist (Gallery Only)")]
public class MediaPlaylist : ScriptableObject
{
    [Header("Default Settings")]
    public float defaultCrossfadeDuration = 1.0f;

    [System.Serializable]
    public class MediaData
    {
        [Header("Basic Info")]
        public string title = "New Media";
        public Sprite thumbnail;

        [Header("Unlock Settings")]
        [Tooltip("最初からギャラリーで解放されているか")]
        public bool isUnlockedByDefault = true;
        [Tooltip("解放キー（空欄の場合はTitleがキーになります）")]
        public string unlockId = "";

        [Header("Media Content")]
        public bool isStaticImage;
        public VideoClip clip;
        public Texture2D image;
        public float imageDuration = 5.0f;
        public AudioClip audioClip;
        public bool isLooping = true;

        [Header("Crossfade")]
        public bool overrideCrossfade = false;
        public float customCrossfadeDuration = 1.0f;
    }

    public MediaData[] items;
}