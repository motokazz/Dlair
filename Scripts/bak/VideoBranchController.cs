using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class VideoBranchController : MonoBehaviour
{
    [SerializeField] private VideoNodePlayer player;
    [SerializeField] private VideoNode startNode;
    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private QTEGuide qte;
    [SerializeField] private QTEGuide qte2;

    private VideoNode currentNode;
    private Coroutine inputRoutine;
    private int successBranchID;
    private SuccessBranch succesBranch;

    [SerializeField] private int lives = 3;


    InputAction buttonAttack;
    InputAction buttonJump;

    private void Awake()
    {
        if (player == null)
            player = GetComponent<VideoNodePlayer>() ?? FindObjectOfType<VideoNodePlayer>();

        //player.OnVideoEnded += HandleVideoEnd;
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

        currentNode = node;

        //コルーチンチェック
        if (inputRoutine != null)
        {
            StopCoroutine(inputRoutine);
            inputRoutine = null;
        }

        player.PlayClip(node.videoClip);

        // 入力が必要なら監視開始
        if (inputActions != null)
        {
            inputRoutine = StartCoroutine(WatchInput(node));
        }
    }

    private IEnumerator WatchInput(VideoNode node)
    {

        float timer = 0f;
        bool success = false;

        var limit = node.videoClip.length;

        if (node.branches.Count>0) qte.NewQTE(node.branches[0].inputStart, node.branches[0].inputWindow); // QTE
        if (node.branches.Count>1) qte2.NewQTE(node.branches[1].inputStart, node.branches[1].inputWindow); // QTE

        while (timer < limit)
        {
            foreach (var branch in node.branches)
            {
                if (timer > branch.inputStart && !success) {
                    // 成功判定
                    if (branch.actionName == "Player/Attack" && buttonAttack.triggered) {
                        success = true;
                        succesBranch = branch;
                        qte.ShowSuccess();
                        qte2.ShowFail();
                        if(branch.skipVideo)yield break;
                    }
                    // 成功判定
                    if (branch.actionName == "Player/Jump" && buttonJump.triggered)
                    {
                        success = true;
                        succesBranch = branch;
                        qte.ShowFail();
                        qte2.ShowSuccess();
                        if (branch.skipVideo) yield break;
                    }
                }
                /*
                if (timer > (branch.inputStart + branch.inputWindow))
                {
                    success=false;
                    yield break;
                }
                */
            }
            timer += Time.deltaTime;
            yield return null;
        }

        if (!success)
        {
            qte.ShowFail();
            qte2.ShowFail();
            HandleFailure();
        }

        HandleVideoEnd();
    }

    private void HandleVideoEnd()
    {
        if (currentNode == null) return;

        // 成功入力があったかで分岐（簡易版：入力成功フラグを別途持つのが理想）
        // 今はとりあえず successNode を優先
        if (succesBranch.successNode != null)
        {
            PlayNode(succesBranch.successNode);
        }
        else if (succesBranch.failNode != null)
        {
            PlayNode(succesBranch.failNode);
        }
        else
        {
            Debug.Log("終端ノード");
        }
    }

    private void HandleFailure()
    {
        Debug.Log("HandleFallure");


        if (currentNode.failNode != null)
            PlayNode(currentNode.failNode);
        else
        {
            PlayNode(startNode);  // リトライ
            qte.ShowFail();
        }

    }
}