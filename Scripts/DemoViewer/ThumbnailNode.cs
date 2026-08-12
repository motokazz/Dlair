using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
public class ThumbnailNode : MonoBehaviour
{
    public Image thumbnailImage;
    public TMP_Text titleText;
    public Button playButton;
    public GameObject lockIcon;    // 未開放の時に表示する鍵アイコンなど

    public void Setup(DemoData data, bool isUnlocked, UnityAction<DemoData> onClickAction)
    {
        titleText.text = isUnlocked ? data.title : "???";
        thumbnailImage.sprite = data.thumbnail;

        // 未開放なら画像を暗くしてボタンを押せなくする
        thumbnailImage.color = isUnlocked ? Color.white : Color.black;
        lockIcon.SetActive(!isUnlocked);
        playButton.interactable = isUnlocked;

        // ボタンが押された時の処理を登録
        playButton.onClick.RemoveAllListeners();
        playButton.onClick.AddListener(() => onClickAction(data));

        Debug.Log(data.demoID);
        Debug.Log(data.title);
        Debug.Log(data.thumbnail.name);
        Debug.Log(data.videoClip.name);


    }
}