using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

public class VideoBranchControllerDouble : MonoBehaviour
{
    [SerializeField] private VideoNodePlayerDouble player;
    [SerializeField] private VideoNode startNode;

    [SerializeField] ConditionManager conditionManager;

    [NonSerialized] public SuccessBranch successBranche = new SuccessBranch();
    private List<SuccessBranch> successBranches;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<VideoNodePlayerDouble>() ?? FindObjectOfType<VideoNodePlayerDouble>();

    }

    private void Start()
    {
        if (startNode != null)
            PlayNode(startNode);
    }

    public void PlayNode(VideoNode node)
    {
        Debug.Log("VBC_PlayNode");
        if (node == null) return;
        player.PlayClip(node);
        successBranche = node.branches[0];
        conditionManager.ProcessConditions(node.branches);
    }

    public async Task Conditions(VideoNode node)
    {
        var res = await conditionManager.ProcessConditions(node.branches);
    }

    private void HandleVideoEnd()
    {
        if (successBranche.successNode != null)
        {
            PlayNode(successBranche.successNode);
        }
        else
        {
            Debug.Log("終端ノード");
        }
    }

    private void HandleFailure()
    {
        Debug.Log("HandleFallure");

        if (successBranche.failNode != null)
            PlayNode(successBranche.failNode);
        else
        {
            PlayNode(startNode);  // リトライ
        }

    }
}