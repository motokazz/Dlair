using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 実行時点の変数値を UI の Text に出す。
/// 変数名はデータベースか変数宣言ノードから選ぶ。
/// UI は AutoRelease の秒数で破棄し、Next へ進む秒数はそれとは別に待つ。
/// </summary>
public class VariableTextNode : BaseNode
{
    [Header("表示するUIプレハブ")]
    public GameObject textUIPrefab;

    [Header("変数")]
    [Tooltip("実行した瞬間の値を Text に書きます")]
    public string variableName = "A";

    [Tooltip("オン: StoryPlayer の UI Parent 配下 / オフ: Canvas 直下。Parent 未指定時はどちらも Canvas 直下")]
    public bool parentToUiParent = true;

    [Tooltip("UI を出してから破棄するまでの秒。AutoRelease の時間経過破棄。Next の待ちとは別です")]
    public float lifetime = 1.5f;

    [Tooltip("Next に進むまでの秒。表示・破棄の時間とは別に測ります")]
    public float nextDelay = 0f;

    [System.NonSerialized]
    private Coroutine routine;

    [System.NonSerialized]
    private int runId;

    public override void Execute(StoryPlayer player)
    {
        if (player == null) return;

        StopNextWait(player);
        ShowVariable(player);
        ScheduleNext(player);
    }

    /// <summary>
    /// 進行中の Next 待ちを打ち切る。この待ちは UI の破棄タイマーとは別です。
    /// </summary>
    public void StopNextWait(StoryPlayer player)
    {
        runId++;
        if (routine == null) return;

        if (player != null)
        {
            player.StopCoroutine(routine);
        }

        routine = null;
    }

    public string GetDisplayTitle()
    {
        return string.IsNullOrWhiteSpace(variableName) ? "Variable" : variableName.Trim();
    }

    private void ShowVariable(StoryPlayer player)
    {
        if (textUIPrefab == null)
        {
            Debug.LogError("【Variable Text】UIプレハブが設定されていません。");
            return;
        }

        Transform parent = player.GetUIParent(parentToUiParent);
        if (parent == null)
        {
            Debug.LogError("【Variable Text】UIを表示するための親 / Canvas がシーン内に見つかりません。");
            return;
        }

        if (!GameManager.TryGetDisplayText(variableName, out string value))
        {
            Debug.LogWarning($"【Variable Text】変数 '{variableName}' が見つかりません。");
            value = "";
        }

        GameObject instance = PrefabPool.Spawn(textUIPrefab, parent);
        if (instance == null) return;

        if (!ApplyText(instance, value))
        {
            Debug.LogWarning("【Variable Text】プレハブ内に TextMeshPro / UI Text が見つかりません。");
        }

        AutoRelease release = instance.GetComponent<AutoRelease>();
        if (release == null)
        {
            release = instance.AddComponent<AutoRelease>();
        }

        release.BeginLifetime(lifetime);
        Debug.Log($"【Variable Text】{variableName} = {value}（破棄 {Mathf.Max(0f, lifetime)} 秒 / Next {Mathf.Max(0f, nextDelay)} 秒）");
    }

    private void ScheduleNext(StoryPlayer player)
    {
        float duration = Mathf.Max(0f, nextDelay);
        if (duration <= 0f)
        {
            player.ContinueTo(this, "Next");
            return;
        }

        int id = ++runId;
        routine = player.StartCoroutine(WaitThenContinue(player, duration, id));
    }

    private IEnumerator WaitThenContinue(StoryPlayer player, float duration, int id)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsCurrentRun(player, id))
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                break;
            }

            yield return null;
        }

        if (routine != null && id == runId)
        {
            routine = null;
        }

        if (!IsCurrentRun(player, id))
        {
            yield break;
        }

        player.ContinueTo(this, "Next");
    }

    private bool IsCurrentRun(StoryPlayer player, int id)
    {
        return id == runId && player != null && player.CurrentNode == this;
    }

    private static bool ApplyText(GameObject root, string value)
    {
        TMP_Text tmp = root.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = value ?? "";
            return true;
        }

        Text uiText = root.GetComponentInChildren<Text>(true);
        if (uiText != null)
        {
            uiText.text = value ?? "";
            return true;
        }

        return false;
    }
}
