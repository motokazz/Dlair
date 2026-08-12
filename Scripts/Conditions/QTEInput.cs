using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class QTEInput : MonoBehaviour
{
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionName;
    [SerializeField] private QTEGuide qTEGuide;
    //
    [SerializeField] VideoNodePlayerDouble videoPlayerDouble;

    private InputAction inputAction;
    private bool success = false; //成功フラグ
    [Header("入力開始時間")]
    public float inputStart = 0f;
    [Header("入力受付時間 -1で無限受付")]
    public float inputLimit = 1.0f;//制限時間

    Coroutine coroutine;

    private void Awake()
    {
        inputAction = inputActions.FindAction(actionName);
    }

    private void Update()
    {
       // if (inputAction.WasReleasedThisDynamicUpdate()) QTEStart();
    }

    public void QTEStart()
    {
        if (coroutine != null) StopCoroutine(coroutine);
        coroutine = StartCoroutine(WatchInput());
    }

    private IEnumerator WatchInput()
    {
        // 入力待ち開始
        qTEGuide.NewQTE(inputStart, inputLimit);
        yield return new WaitForSeconds(inputStart);

        float timer = 0f;

        if (inputLimit >= 0f) // inputLimitが０以上だったら通常待機
        {
            while (timer < inputLimit)
            {
                if (CheckConditions())
                {
                    Success();//成功処理
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }
        else //inputLimitが０以下だったら無限待ち受け
        {
            yield return new WaitUntil(() => CheckConditions());
            Success();//成功処理
            yield break;
        }

        Failure();//失敗処理
        yield return new WaitForSeconds(0.5f);

        yield return null;
    }

    public void Success()
    {
        Debug.Log("Success");
        success = true;
        qTEGuide.ShowSuccess();
        qTEGuide.Vanish();
        videoPlayerDouble.playContinue = true;
    }

    public void Failure()
    {
        success = false;
        qTEGuide.Vanish();
        qTEGuide.ShowFail();
    }

    public bool CheckConditions()
    {
        if(inputAction.triggered)return true;
        return false;
    }


}
