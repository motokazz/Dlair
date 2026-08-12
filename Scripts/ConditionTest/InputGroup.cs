using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 複数のInputNodeをグループとして管理し、全体の成否を判定するクラス
/// </summary>
public class InputGroup : MonoBehaviour
{
    [Header("管理対象のノードリスト")]
    public List<InputNode> nodes;

    private int processedCount = 0; // 終了（タイムアウトまたはクリック）したノードの累計数
    private bool groupFinished = false; // グループ全体としての判定が完了したかどうかのフラグ

    /// <summary>
    /// グループ内の全ノードを起動し、入力待ちを開始する
    /// </summary>
    /// <param name="onGroupResult">グループ全体の成否を返すコールバック</param>
    public void StartGroup(System.Action<bool> onGroupResult)
    {
        processedCount = 0;
        groupFinished = false;

        // リストに登録された全ノードに対して初期設定を行う
        foreach (var node in nodes)
        {
            // 各ノードが終了した時の処理をラムダ式で登録
            node.Setup((isSuccess) => {

                // すでに誰かが成功してグループが終了している場合は、後から来た通知を無視する
                if (groupFinished) return;

                if (isSuccess)
                {
                    // 【成功判定】
                    // 誰か一人がボタンを押せれば、グループ全体として「成功」とみなす
                    groupFinished = true;

                    // 他のノードが動いている可能性があるので、すべて強制停止させる
                    StopAllNodes();

                    // InputManager側に成功を通知
                    onGroupResult?.Invoke(true);
                }
                else
                {
                    // 【失敗（タイムアウト）時】
                    // 終わったノードの数をカウントアップ
                    processedCount++;

                    // リストにある全てのノードがタイムアウト（失敗）した場合のみ、
                    // グループ全体として「失敗」と判定する
                    if (processedCount >= nodes.Count)
                    {
                        groupFinished = true;
                        // InputManager側に失敗を通知
                        onGroupResult?.Invoke(false);
                    }
                }
            });
        }
    }

    /// <summary>
    /// 全ノードを強制停止して非表示にする。
    /// 誰かが成功した時や、ゲームが中断された時に呼び出される。
    /// </summary>
    private void StopAllNodes()
    {
        foreach (var node in nodes)
        {
            // InputNode側のForceStopを呼び、Invokeのキャンセルや非表示化を行う
            node.ForceStop();
        }
    }
}