using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.Video;
//using System.Diagnostics;

public class VideoBranchController2 : MonoBehaviour
{
    [SerializeField] private VideoNodePlayer player;
    [SerializeField] private VideoNode startNode;
    [SerializeField] private InputActionAsset inputActions;

    private VideoNode currentNode;
    private Coroutine inputRoutine;
    private Coroutine inputRoutine2;
    private SuccessBranch succesBranch = new SuccessBranch();

    [SerializeField] private int lives = 3;

    bool success = false; //成功フラグ

    InputAction buttonAttack;
    InputAction buttonJump;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<VideoNodePlayer>() ?? FindObjectOfType<VideoNodePlayer>();

        player.OnVideoEnded += HandleVideoEnd;

    }

    private void OnEnable()
    {
        buttonAttack = inputActions.FindAction("Player/Attack");
        buttonJump = inputActions.FindAction("Player/Jump");
    }

    private void Start()
    {
        if (startNode != null)
            PlayNode(startNode);
    }

    public void PlayNode(VideoNode node)
    {
        if (node == null) return;
        player.PlayClip(node.videoClip);
        succesBranch = node.branches[0];
    }

    private void HandleVideoEnd()
    {
        if (succesBranch.successNode != null)
        {
            PlayNode(succesBranch.successNode);
        }
        else
        {
            Debug.Log("終端ノード");
        }
    }

    private void HandleFailure()
    {
        Debug.Log("HandleFallure");

        if (succesBranch.failNode != null)
            PlayNode(succesBranch.failNode);
        else
        {
            PlayNode(startNode);  // リトライ
        }

    }
}