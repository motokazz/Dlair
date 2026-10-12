using UnityEngine;
using UnityEngine.UI;

// オプションメニューのボタンへ、セーブデータの消去を AddListener でつなぐ。
public class SaveDataControls : MonoBehaviour
{
    [Tooltip("進行データの初期値。空なら、すでに読み込まれている StoryVariableDatabase を使います。")]
    public StoryVariableDatabase variableDatabase;

    [Header("ボタン")]
    public Button clearStoryButton;
    public Button clearOptionButton;
    public Button deleteAllButton;

    void OnEnable()
    {
        Bind(clearStoryButton, ClearStoryData);
        Bind(clearOptionButton, ClearOptionData);
        Bind(deleteAllButton, DeleteAllSaveData);
    }

    void OnDisable()
    {
        Unbind(clearStoryButton, ClearStoryData);
        Unbind(clearOptionButton, ClearOptionData);
        Unbind(deleteAllButton, DeleteAllSaveData);
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
