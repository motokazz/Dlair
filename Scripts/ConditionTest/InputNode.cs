using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 特定の時間枠内でのボタン入力を管理するクラス
/// </summary>
public class InputNode : MonoBehaviour
{
    [Header("設定パラメータ")]
    public float activeDelay = 0f; // Setupが呼ばれてから、実際にボタンが表示されるまでの待ち時間
    public float duration = 2f;    // ボタンが表示されてから、消えるまでの制限時間
    public Button button;          // 制御対象のUIボタン

    private bool isProcessed = false;   // 二重処理防止フラグ（クリック済、またはタイムアウト済か）
    private Action<bool> onComplete;    // 結果（成功/失敗）を報告するためのコールバック

    /// <summary>
    /// ノードの初期化と実行開始
    /// </summary>
    /// <param name="callback">入力成功(true)か失敗(false)を受け取るメソッド</param>
    public void Setup(Action<bool> callback)
    {
        isProcessed = false;
        onComplete = callback;

        // ボタンのイベントリスナーをリセット（重複登録を避けるため重要）
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnButtonClicked);

        // 一旦非表示にしてから、遅延実行でActivateを呼ぶ
        gameObject.SetActive(false);
        Invoke(nameof(Activate), activeDelay);
    }

    /// <summary>
    /// ボタンを画面に出現させ、制限時間のカウントを開始する
    /// </summary>
    void Activate()
    {
        if (isProcessed) return; // すでにForceStopなどで終了していたら何もしない

        gameObject.SetActive(true);
        // duration秒後にタイムアウト処理を実行するように予約
        Invoke(nameof(TimeOut), duration);
    }

    /// <summary>
    /// ボタンが押された時の処理（成功）
    /// </summary>
    void OnButtonClicked()
    {
        if (isProcessed) return;
        Finish(true); // 成功として終了
    }

    /// <summary>
    /// 制限時間内に押せなかった時の処理（失敗）
    /// </summary>
    void TimeOut()
    {
        if (isProcessed) return;
        Finish(false); // 失敗として終了
    }

    /// <summary>
    /// 外部（管理クラス等）から強制的にこのノードを停止させる
    /// 例：ゲームオーバー時や、他の条件でシーケンスが中断された場合
    /// </summary>
    public void ForceStop()
    {
        // silent: true にすることで、onCompleteを呼ばずに終了させる
        Finish(false, true);
    }

    /// <summary>
    /// 共通の終了処理
    /// </summary>
    /// <param name="success">成功したかどうか</param>
    /// <param name="silent">trueの場合、コールバックをスキップする</param>
    void Finish(bool success, bool silent = false)
    {
        isProcessed = true;

        // Invokeで予約していたActivateやTimeOutをすべてキャンセルする（メモリリーク・誤作動防止）
        CancelInvoke();

        if (!silent)
        {
            onComplete?.Invoke(success);
        }

        gameObject.SetActive(false);
    }
}