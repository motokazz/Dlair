using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;

public class VideoNodePlayerDouble : MonoBehaviour
{
    public VideoPlayer playerA;
    public VideoPlayer playerB;

    [SerializeField] VideoClip dummyClip;

    //[SerializeField] ConditionManager conditionManager;

    private VideoPlayer currentPlayer;
    private VideoPlayer nextPlayer;
    private VideoNode currentNode;
    
    private VideoClip currentClip;
    private VideoClip nextClip;

    private bool prepared = false;
    public bool playContinue = true;// ビデオ終端で止めるか。入力待ち用。

    void Awake()
    {
        currentPlayer = playerA;
        nextPlayer = playerB;

        Setup(playerA);
        Setup(playerB);
    }

    IEnumerator Start()
    {
        playerA.clip = dummyClip;
        playerA.Prepare();
        while (!playerA.isPrepared)
            yield return null;
        playerA.Stop();
    }
  
    void Setup(VideoPlayer p)
    {
        p.playOnAwake = false;
        p.waitForFirstFrame = true;
        p.skipOnDrop = false;
        p.prepareCompleted += (vp) => { prepared = true; };
        p.loopPointReached +=_=> OnVideoEnded();
    }

    public void PlayClip(VideoNode node)
    {
        //CurrentNode差し替え
        currentNode = node;

        //NextClip準備
        nextPlayer.Stop();
        nextPlayer.clip = node.videoClip;
        nextPlayer.isLooping = node.isLoop;
        nextPlayer.Prepare();

        StartCoroutine(PlayClipC());

    }

    public void PlayClipPure(VideoClip clip)
    {
        nextPlayer.Stop();
        nextPlayer.clip = clip;
        nextPlayer.Prepare();
        StartCoroutine(PlayClipC());
    }



    IEnumerator PlayClipC()
    {
        //NextClip準備待ち
        while (!nextPlayer.isPrepared) yield return null;

        // 再開指示待ち
        while (!playContinue) yield return null;

        //Player交換
        SwapPlayers();
        yield return new WaitForEndOfFrame();

        currentPlayer.Play();
        playContinue = currentNode.playContinue;//次回再生用にプレイ終了設定
    }

    void SwapPlayers()
    {
        currentPlayer.Stop();
        var temp = currentPlayer;
        currentPlayer = nextPlayer;
        nextPlayer = temp;
        currentPlayer.Prepare();
    }

    void OnVideoEnded()
    {
        PlayClip(currentNode.branches[0].successNode);

        //Condition
        //conditionManager.ProcessCondition(currentNode.branches[0]);
    }
}