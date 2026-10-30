using UnityEngine;

/// <summary>
/// 説明は ExitGameNode.Summary。
/// </summary>
public class ExitGameNode : BaseNode
{
    public const string Summary =
        "到達するとゲーム（アプリケーション）を終了します。サブグラフから戻る Exit とは別で、出力はありません。\n" +
        "エディタの再生中は再生を止めます。ビルドしたゲームではアプリケーションを終了します。\n" +
        "タイトル右のボタンでノード全体、設定ボタンで項目だけ、折りたたみと展開ができます。";

    [Tooltip("オンでノード全体を展開、オフで折りたたみ。")]
    public bool nodeExpanded = true;

    [Tooltip("オンで設定項目を展開、オフで折りたたみ。")]
    public bool settingsExpanded = true;

    /// <summary>
    /// グラフ上の説明文。Summary と同じです。
    /// </summary>
    public string GetHelpText()
    {
        return Summary;
    }

    /// <summary>
    /// アプリケーションを終了する。次のノードへは進まない。
    /// </summary>
    public override void Execute(StoryPlayer player)
    {
        Debug.Log("【Exit Game】アプリケーションを終了します。");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
