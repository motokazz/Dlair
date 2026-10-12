#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

public static class AudioOptionsMixerSync
{
    public static string Sync(AudioOptionsCatalog catalog)
    {
        if (catalog == null)
            return "カタログがありません。";
        if (catalog.mixer == null)
            return "カタログに AudioMixer が割り当てられていません。";
        if (catalog.options == null || catalog.options.Count == 0)
            return "音量項目がありません。";

        var log = new StringBuilder();
        AudioMixer mixer = catalog.mixer;
        Type controllerType = mixer.GetType();
        MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo containsExposed = controllerType.GetMethod("ContainsExposedParameter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo listGroups = controllerType.GetMethod("GetAllAudioGroupsSlow", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo exposedProperty = controllerType.GetProperty("exposedParameters", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Type pathType = controllerType.Assembly.GetType("UnityEditor.Audio.AudioGroupParameterPath");

        if (createGroup == null || addExposed == null || containsExposed == null || listGroups == null || exposedProperty == null || pathType == null)
            return "この Unity では AudioMixer の内部 API を見つけられませんでした。";

        Undo.RecordObject(catalog, "Sync Audio Options");
        Undo.RecordObject(mixer, "Sync Audio Options");
        RemoveCatalogParameters(mixer, exposedProperty, catalog);

        for (int i = 0; i < catalog.options.Count; i++)
        {
            AudioOptionEntry entry = catalog.options[i];
            if (entry == null || entry.kind != AudioOptionKind.VolumeSlider)
                continue;
            if (string.IsNullOrEmpty(entry.mixerGroup))
            {
                log.AppendLine(entry.id + ": グループ名が空です。");
                continue;
            }

            object group = FindOrCreateGroup(mixer, controllerType, createGroup, listGroups, entry.mixerGroup, log);
            if (group == null)
                continue;

            EnsureParented(mixer, controllerType, group, log);

            MethodInfo getVolumeGuid = group.GetType().GetMethod("GetGUIDForVolume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo getVolumeValue = group.GetType().GetMethod("GetValueForVolume", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getVolumeGuid == null)
            {
                log.AppendLine(entry.mixerGroup + ": Volume の GUID を取得できません。");
                continue;
            }

            object volumeGuid = getVolumeGuid.Invoke(group, null);
            bool alreadyExposed = (bool)containsExposed.Invoke(mixer, new[] { volumeGuid });
            if (!alreadyExposed)
            {
                object path = Activator.CreateInstance(pathType, group, volumeGuid);
                addExposed.Invoke(mixer, new[] { path });
                log.AppendLine(entry.mixerGroup + ": " + entry.ParameterName + " を公開しました。");
            }
            else
            {
                log.AppendLine(entry.mixerGroup + ": 公開済みです。名前を " + entry.ParameterName + " に合わせます。");
            }

            RenameExposedParameter(mixer, exposedProperty, volumeGuid, entry.ParameterName);

            if (getVolumeValue != null)
            {
                object snapshot = mixer.FindSnapshot("Snapshot");
                if (snapshot != null)
                {
                    entry.authoredDb = (float)getVolumeValue.Invoke(group, new[] { mixer, snapshot });
                    log.AppendLine(entry.mixerGroup + ": 基準音量 " + entry.authoredDb.ToString("0.00") + " dB");
                }
            }
        }

        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static object FindOrCreateGroup(AudioMixer mixer, Type controllerType, MethodInfo createGroup, MethodInfo listGroups, string groupName, StringBuilder log)
    {
        object existing = FindGroupObject(mixer, listGroups, groupName);
        if (existing != null)
            return existing;

        object created = createGroup.Invoke(mixer, new object[] { groupName, true });
        log.AppendLine(groupName + ": グループを作成しました。");
        return created;
    }

    static object FindGroupObject(AudioMixer mixer, MethodInfo listGroups, string groupName)
    {
        IList groups = (IList)listGroups.Invoke(mixer, null);
        if (groups != null)
        {
            foreach (object group in groups)
            {
                var unityObject = group as UnityEngine.Object;
                if (unityObject != null && unityObject.name == groupName)
                    return group;
            }
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mixer));
        for (int i = 0; i < assets.Length; i++)
        {
            UnityEngine.Object asset = assets[i];
            if (asset != null && asset.name == groupName && asset.GetType().Name == "AudioMixerGroupController")
                return asset;
        }

        return null;
    }

    static void EnsureParented(AudioMixer mixer, Type controllerType, object group, StringBuilder log)
    {
        object master = controllerType.GetProperty("masterGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(mixer, null);
        PropertyInfo childrenProperty = master.GetType().GetProperty("children", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Array children = childrenProperty.GetValue(master, null) as Array;
        string groupName = ((UnityEngine.Object)group).name;
        if (children != null)
        {
            for (int i = 0; i < children.Length; i++)
            {
                var child = children.GetValue(i) as UnityEngine.Object;
                if (child != null && child.name == groupName)
                    return;
            }
        }

        Type elementType = childrenProperty.PropertyType.GetElementType();
        int oldLength = children == null ? 0 : children.Length;
        Array updated = Array.CreateInstance(elementType, oldLength + 1);
        for (int i = 0; i < oldLength; i++)
            updated.SetValue(children.GetValue(i), i);
        updated.SetValue(group, oldLength);
        childrenProperty.SetValue(master, updated, null);

        MethodInfo addToView = controllerType.GetMethod("AddGroupToCurrentView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        MethodInfo viewContains = controllerType.GetMethod("CurrentViewContainsGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        PropertyInfo groupId = group.GetType().GetProperty("groupID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        bool alreadyInView = false;
        if (viewContains != null && groupId != null)
            alreadyInView = (bool)viewContains.Invoke(mixer, new[] { groupId.GetValue(group, null) });
        if (addToView != null && !alreadyInView)
            addToView.Invoke(mixer, new[] { group });

        log.AppendLine(groupName + ": Master の子にしました。");
    }

    static void RemoveCatalogParameters(AudioMixer mixer, PropertyInfo exposedProperty, AudioOptionsCatalog catalog)
    {
        Array exposed = exposedProperty.GetValue(mixer, null) as Array;
        if (exposed == null || exposed.Length == 0)
            return;

        Type elementType = exposed.GetType().GetElementType();
        FieldInfo nameField = elementType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var kept = new System.Collections.Generic.List<object>();
        for (int i = 0; i < exposed.Length; i++)
        {
            object item = exposed.GetValue(i);
            string name = nameField.GetValue(item) as string;
            if (!IsCatalogParameter(catalog, name))
                kept.Add(item);
        }

        if (kept.Count == exposed.Length)
            return;

        Array updated = Array.CreateInstance(elementType, kept.Count);
        for (int i = 0; i < kept.Count; i++)
            updated.SetValue(kept[i], i);
        exposedProperty.SetValue(mixer, updated, null);
    }

    static bool IsCatalogParameter(AudioOptionsCatalog catalog, string parameterName)
    {
        if (catalog.options == null || string.IsNullOrEmpty(parameterName))
            return false;

        for (int i = 0; i < catalog.options.Count; i++)
        {
            AudioOptionEntry entry = catalog.options[i];
            if (entry != null && entry.ParameterName == parameterName)
                return true;
        }

        return false;
    }

    static void RenameExposedParameter(AudioMixer mixer, PropertyInfo exposedProperty, object volumeGuid, string parameterName)
    {
        Array exposed = (Array)exposedProperty.GetValue(mixer, null);
        if (exposed == null)
            return;

        Type elementType = exposed.GetType().GetElementType();
        FieldInfo guidField = elementType.GetField("guid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        FieldInfo nameField = elementType.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var updated = Array.CreateInstance(elementType, exposed.Length);
        for (int i = 0; i < exposed.Length; i++)
        {
            object item = exposed.GetValue(i);
            if (guidField.GetValue(item).Equals(volumeGuid))
                nameField.SetValue(item, parameterName);
            updated.SetValue(item, i);
        }

        exposedProperty.SetValue(mixer, updated, null);
    }
}

[CustomEditor(typeof(AudioOptionsCatalog))]
public class AudioOptionsCatalogEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("音量項目を増やすときはリストに行を追加し、下のボタンでミキサーグループと公開パラメータを作ってください。スライダーは再生時にこのリストから並びます。", MessageType.Info);
        if (GUILayout.Button("ミキサーグループと公開パラメータを同期"))
            Debug.Log(AudioOptionsMixerSync.Sync((AudioOptionsCatalog)target));
    }
}
#endif
