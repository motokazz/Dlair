using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 連続した入力イベントを制御し、結果を表示・保存するマネージャークラス
/// </summary>
public class InputManager : MonoBehaviour
{
    [Header("参照設定")]
    public InputGroup targetGroup;     // 実行する入力グループ（複数のInputNodeをまとめたものと推測）
    public TMP_Text resultText;        // 画面に「SUCCESS!」などを表示するテキストUI

    [Header("システム連携")]
    public DemoViewerManager demoViewerManager; // 成功時にサムネイルを更新するための参照

    private bool flagSucccess = false; // 次のステップへ進むためのフラグ（コルーチンの待機用）

    void Start()
    {
        // 初期状態ではテキストを空にする
        resultText.text = "";

        // 指定した回数（ここでは10回）だけ入力グループを繰り返す処理を開始
        StartCoroutine(RepeatStartGroup(10));
    }

    /// <summary>
    /// 指定された回数分、入力シーケンスを繰り返すコルーチン
    /// </summary>
    /// <param name="count">繰り返す回数</param>
    IEnumerator RepeatStartGroup(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // 入力グループを開始。終わったら ShowResult メソッドが呼ばれるように設定
            targetGroup.StartGroup(ShowResult);

            // 成功フラグ(flagSucccess)が true になるまで、ここで処理を一時停止する
            // ※失敗した場合はフラグが立たないので、ここでループが止まる仕様になっている点に注意
            yield return new WaitUntil(() => flagSucccess);

            // フラグをリセットして次のループへ備える
            flagSucccess = false;
        }

        yield return null;
    }

    /// <summary>
    /// 入力グループからの結果を受け取り、UI更新やデータ処理を行うコールバック関数
    /// </summary>
    /// <param name="success">入力が成功したかどうか</param>
    void ShowResult(bool success)
    {
        if (success)
        {
            // --- 成功時の処理 ---
            resultText.text = "SUCCESS!";
            resultText.color = Color.yellow;

            // ランダムなID（1〜10）のデモコンテンツをアンロックする
            DemoSaveManager.UnlockDemo(Random.Range(1, 11).ToString());

            // ビューワーのサムネイル一覧を再生成して更新を反映させる
            demoViewerManager?.GenerateThumbnails();

            // コルーチンの待機を解除するためにフラグを立てる
            flagSucccess = true;
        }
        else
        {
            // --- 失敗時の処理 ---
            resultText.text = "FAILED...";
            resultText.color = Color.red;

            // ※失敗時は flagSucccess が true にならないため、
            // RepeatStartGroup のループはここで実質的にストップする（リトライ待機状態）
        }
    }
}