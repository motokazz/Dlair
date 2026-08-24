using System.Collections.Generic;
using UnityEngine;

public class MiniGameManager : MonoBehaviour
{
    public VideoSelector videoSelector;
    private Dictionary<string, IMiniGame> miniGames = new Dictionary<string, IMiniGame>();

    private void Awake()
    {
        IMiniGame[] games = GetComponentsInChildren<IMiniGame>(true);
        foreach (var game in games)
        {
            if (!miniGames.ContainsKey(game.MiniGameComponentId))
            {
                miniGames.Add(game.MiniGameComponentId, game);
            }
        }
    }

    private void OnEnable()
    {
        if (videoSelector != null)
        {
            videoSelector.onCustomEventTriggered.AddListener(ReceiveCustomEvent);
        }
    }

    private void OnDisable()
    {
        if (videoSelector != null)
        {
            videoSelector.onCustomEventTriggered.RemoveListener(ReceiveCustomEvent);
        }
    }

    // ★受け取るデータが MediaNode になりました！
    public void ReceiveCustomEvent(MediaNode data)
    {
        string targetEventId = data.eventId;

        if (miniGames.TryGetValue(targetEventId, out IMiniGame targetGame))
        {
            targetGame.StartGame(videoSelector, data);
        }
        else
        {
            Debug.LogWarning($"MiniGame '{targetEventId}' が見つかりませんでした。");
            videoSelector.PlayNextNode();
        }
    }
}