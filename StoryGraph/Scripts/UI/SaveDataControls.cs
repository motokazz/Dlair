using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// オプションメニューのボタンへ、進行・設定・全セーブの消去をつなぐ。
/// </summary>
public class SaveDataControls : MonoBehaviour
{
    [Tooltip("進行データの初期値。空なら、すでに読み込まれている StoryVariableDatabase を使います。")]
    public StoryVariableDatabase variableDatabase;

    [Header("ボタン")]
    public Button clearStoryButton;
    public Button clearOptionButton;
    public Button deleteAllButton;
    public Button logSaveButton;

    void OnEnable()
    {
        Bind(clearStoryButton, ClearStoryData);
        Bind(clearOptionButton, ClearOptionData);
        Bind(deleteAllButton, DeleteAllSaveData);
        Bind(logSaveButton, LogSaveData);
    }

    void OnDisable()
    {
        Unbind(clearStoryButton, ClearStoryData);
        Unbind(clearOptionButton, ClearOptionData);
        Unbind(deleteAllButton, DeleteAllSaveData);
        Unbind(logSaveButton, LogSaveData);
    }

    [ContextMenu("進行データをクリア")]
    public void ClearStoryData()
    {
        GameSave.ClearStory(variableDatabase);
    }

    [ContextMenu("オプションデータをクリア")]
    public void ClearOptionData()
    {
        GameSave.ClearOptions();
    }

    [ContextMenu("セーブデータを完全に消去")]
    public void DeleteAllSaveData()
    {
        GameSave.DeleteAll(variableDatabase);
    }

    [ContextMenu("セーブデータをコンソールに表示")]
    public void LogSaveData()
    {
        GameSave.LogContents();
    }

    static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    static void Unbind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
    }
}
