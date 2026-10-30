using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Video;
using System;
using System.Collections.Generic;

/// <summary>
/// グラフ上のノード表示。ポート、設定、ヘルプをノードの種類ごとに出す。
/// </summary>
public class StoryNodeUI : Node
{
    public string guid;
    public BaseNode data;
    private bool isExecuting;
    private Image mediaThumbnail;
    private IVisualElementScheduledItem mediaPreviewRetry;
    private Button playClipFoldButton;
    private VisualElement playClipSettingsBody;
    private bool syncingPlayClipCollapse;
    private Button choiceFoldButton;
    private VisualElement choiceSettingsBody;
    private bool syncingChoiceCollapse;
    private Button exitGameFoldButton;
    private VisualElement exitGameSettingsBody;
    private bool syncingExitGameCollapse;
    private const float ChoiceNodeWidth = 248f;

    public StoryNodeUI(BaseNode nodeData)
    {
        this.guid = nodeData.guid;
        this.data = nodeData;

        this.title = StoryGraphNodeNaming.Build(nodeData);
        this.SetPosition(new Rect(nodeData.position, Vector2.zero));

        Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Enter";
        inputContainer.Add(inputPort);

        if (nodeData is ExitNode || nodeData is ExitGameNode || nodeData is GotoNode)
        {
            // Exit / Exit Game / Goto は入力のみ。Exit Game は終了、Goto の行先はドロップダウンで指定する
        }
        else if (nodeData is ConditionNode)
        {
            Port truePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            truePort.portName = "True";
            outputContainer.Add(truePort);

            Port falsePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            falsePort.portName = "False";
            outputContainer.Add(falsePort);
        }
        else if (nodeData is ChoiceNode choiceNode)
        {
            BuildChoicePorts(choiceNode);
        }
        else if (nodeData is RandomBranchNode randomBranchNode)
        {
            BuildRandomBranchPorts(randomBranchNode);
        }
        else if (nodeData is ConditionBranchNode conditionBranchNode)
        {
            BuildConditionBranchPorts(conditionBranchNode);
        }
        else
        {
            Port outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            outputPort.portName = "Next";
            outputContainer.Add(outputPort);
        }

        DrawNodeSettings();
        if (data is PlayClipNode playClipNode)
            expanded = playClipNode.nodeExpanded;
        else if (data is ChoiceNode expandedChoice)
            expanded = expandedChoice.nodeExpanded;
        else if (data is ExitGameNode expandedExitGame)
            expanded = expandedExitGame.nodeExpanded;
        BindPlayClipCollapse();
        BindChoiceCollapse();
        BindExitGameCollapse();
        RefreshExpandedState();
        RefreshPorts();
        ApplyNodeStyle();
        RefreshDynamicTitle();
        AttachMediaThumbnail();
    }

    public bool TryGetLayoutPosition(out Vector2 position)
    {
        Rect rect = GetPosition();
        position = rect.position;

        if (float.IsNaN(position.x) || float.IsNaN(position.y) ||
            float.IsInfinity(position.x) || float.IsInfinity(position.y))
        {
            position = data != null ? data.position : Vector2.zero;
            return false;
        }

        if (panel == null)
        {
            return false;
        }

        // レイアウト前・破棄中はサイズ0で (0,0) を返し、保存すると全ノードが重なる
        if (rect.width <= 1f && rect.height <= 1f)
        {
            return false;
        }

        GraphView graphView = GetFirstAncestorOfType<GraphView>();
        if (graphView != null && GetFirstAncestorOfType<Scope>() != null)
        {
            Vector2 graphPos = graphView.contentViewContainer.WorldToLocal(worldBound.position);
            if (!float.IsNaN(graphPos.x) && !float.IsNaN(graphPos.y) &&
                !float.IsInfinity(graphPos.x) && !float.IsInfinity(graphPos.y))
            {
                position = graphPos;
            }
        }

        return true;
    }

    private void RecordNodeUndo(string undoName = "ノードを編集")
    {
        if (data == null) return;
        Undo.RecordObject(data, undoName);
    }

    private void BeginGraphEdit(string undoName)
    {
        StoryGraphEditorHooks.BeginGraphEdit?.Invoke(undoName);
    }

    private void EndGraphEdit()
    {
        StoryGraphEditorHooks.EndGraphEdit?.Invoke();
    }

    public void SetExecuting(bool executing)
    {
        if (isExecuting == executing) return;
        isExecuting = executing;

        if (executing)
        {
            Color glow = new Color(1f, 0.82f, 0.15f);
            style.borderTopWidth = 3;
            style.borderBottomWidth = 3;
            style.borderLeftWidth = 3;
            style.borderRightWidth = 3;
            style.borderTopColor = glow;
            style.borderBottomColor = glow;
            style.borderLeftColor = glow;
            style.borderRightColor = glow;
        }
        else
        {
            style.borderTopWidth = StyleKeyword.Null;
            style.borderBottomWidth = StyleKeyword.Null;
            style.borderLeftWidth = StyleKeyword.Null;
            style.borderRightWidth = StyleKeyword.Null;
            style.borderTopColor = StyleKeyword.Null;
            style.borderBottomColor = StyleKeyword.Null;
            style.borderLeftColor = StyleKeyword.Null;
            style.borderRightColor = StyleKeyword.Null;
        }
    }

    private void RefreshDynamicTitle()
    {
        if (data == null || data is RedirectNode) return;
        title = StoryGraphNodeNaming.Build(data);
        if (data is ChoiceNode)
            ApplyChoiceNodeChrome();
    }

    private void ApplyNodeStyle()
    {
        Color titleColor;
        if (data is StartNode)
        {
            titleColor = new Color(0.20f, 0.55f, 0.25f);
        }
        else if (data is ConditionNode || data is VariableOperationNode || data is BoolOperationNode || data is VariableDeclareNode)
        {
            titleColor = new Color(0.48f, 0.22f, 0.62f);
            if (data is VariableDeclareNode) style.minWidth = 300;
        }
        else if (data is PlaybackSpeedNode)
        {
            titleColor = new Color(0.18f, 0.42f, 0.58f);
            style.minWidth = 180;
        }
        else if (data is RandomBranchNode)
        {
            titleColor = new Color(0.70f, 0.40f, 0.10f);
            style.minWidth = 180;
        }
        else if (data is ConditionBranchNode)
        {
            titleColor = new Color(0.50f, 0.25f, 0.40f);
            style.minWidth = 180;
        }
        else if (data is RedirectNode)
        {
            ApplyRedirectNodeStyle();
            return;
        }
        else if (data is SubGraphNode)
        {
            titleColor = new Color(0.18f, 0.40f, 0.62f);
        }
        else if (data is LabelNode)
        {
            titleColor = new Color(0.10f, 0.48f, 0.48f);
            style.minWidth = 180;
        }
        else if (data is WaitNode)
        {
            titleColor = new Color(0.22f, 0.34f, 0.52f);
            style.minWidth = 160;
        }
        else if (data is GotoNode)
        {
            titleColor = new Color(0.58f, 0.42f, 0.12f);
            style.minWidth = 240;
        }
        else if (data is TextNode)
        {
            titleColor = new Color(0.18f, 0.40f, 0.52f);
            style.minWidth = 220;
        }
        else if (data is VariableTextNode)
        {
            titleColor = new Color(0.12f, 0.46f, 0.40f);
            style.minWidth = 220;
        }
        else if (data is SpawnPrefabNode)
        {
            titleColor = new Color(0.62f, 0.32f, 0.18f);
            style.minWidth = 200;
        }
        else if (data is ExitGameNode)
        {
            titleColor = new Color(0.72f, 0.16f, 0.20f);
            style.minWidth = 220;
        }
        else if (data is ExitNode)
        {
            titleColor = new Color(0.55f, 0.22f, 0.22f);
            titleButtonContainer.style.display = DisplayStyle.None;
            extensionContainer.style.display = DisplayStyle.None;
        }
        else if (data is ChoiceNode)
        {
            ApplyChoiceNodeChrome();
            return;
        }
        else
        {
            return;
        }

        titleContainer.style.backgroundColor = new StyleColor(titleColor);

        if (data is ConditionNode || data is VariableOperationNode || data is BoolOperationNode || data is VariableDeclareNode || data is PlaybackSpeedNode || data is RandomBranchNode || data is ConditionBranchNode || data is LabelNode || data is GotoNode || data is WaitNode || data is TextNode || data is VariableTextNode || data is SpawnPrefabNode || data is ExitGameNode)
        {
            ApplyProminentTitleStyle(12);
        }
    }

    private void ApplyProminentTitleStyle(float fontSize)
    {
        Label titleLabel = this.Q<Label>("title-label");
        if (titleLabel == null) return;

        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = fontSize;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.overflow = Overflow.Visible;
    }

    private Label AddHelpButton(string helpText)
    {
        if (string.IsNullOrEmpty(helpText)) return null;

        Label helpLabel = new Label(helpText);
        helpLabel.style.display = DisplayStyle.None;
        helpLabel.style.whiteSpace = WhiteSpace.Normal;
        helpLabel.style.maxWidth = 168;
        helpLabel.style.color = new StyleColor(new Color(0.82f, 0.82f, 0.82f));
        helpLabel.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.28f));
        helpLabel.style.marginTop = 2;
        helpLabel.style.marginBottom = 4;
        helpLabel.style.paddingLeft = 6;
        helpLabel.style.paddingRight = 6;
        helpLabel.style.paddingTop = 4;
        helpLabel.style.paddingBottom = 4;
        helpLabel.style.borderTopLeftRadius = 4;
        helpLabel.style.borderTopRightRadius = 4;
        helpLabel.style.borderBottomLeftRadius = 4;
        helpLabel.style.borderBottomRightRadius = 4;

        Button helpBtn = new Button(() =>
        {
            bool shown = helpLabel.style.display == DisplayStyle.Flex;
            if (shown)
            {
                helpLabel.style.display = DisplayStyle.None;
                return;
            }

            expanded = true;
            if (data is PlayClipNode shownClip && !shownClip.nodeExpanded)
            {
                shownClip.nodeExpanded = true;
                UnityEditor.EditorUtility.SetDirty(shownClip);
            }
            if (data is ChoiceNode shownChoice && !shownChoice.nodeExpanded)
            {
                shownChoice.nodeExpanded = true;
                UnityEditor.EditorUtility.SetDirty(shownChoice);
            }
            if (data is ExitGameNode shownExitGame && !shownExitGame.nodeExpanded)
            {
                shownExitGame.nodeExpanded = true;
                UnityEditor.EditorUtility.SetDirty(shownExitGame);
            }
            RefreshExpandedState();
            helpLabel.style.display = DisplayStyle.Flex;
        })
        {
            text = "？",
            tooltip = "説明を表示"
        };
        helpBtn.style.width = 16;
        helpBtn.style.minWidth = 16;
        helpBtn.style.maxWidth = 16;
        helpBtn.style.height = 16;
        helpBtn.style.marginLeft = 2;
        helpBtn.style.marginRight = 2;
        helpBtn.style.marginTop = 1;
        helpBtn.style.marginBottom = 1;
        helpBtn.style.paddingLeft = 0;
        helpBtn.style.paddingRight = 0;
        helpBtn.style.paddingTop = 0;
        helpBtn.style.paddingBottom = 0;
        helpBtn.style.fontSize = 10;
        helpBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
        helpBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
        helpBtn.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.14f));
        helpBtn.style.borderTopWidth = 0;
        helpBtn.style.borderBottomWidth = 0;
        helpBtn.style.borderLeftWidth = 0;
        helpBtn.style.borderRightWidth = 0;
        helpBtn.style.borderTopLeftRadius = 8;
        helpBtn.style.borderTopRightRadius = 8;
        helpBtn.style.borderBottomLeftRadius = 8;
        helpBtn.style.borderBottomRightRadius = 8;

        titleButtonContainer.Insert(0, helpBtn);
        extensionContainer.Insert(0, helpLabel);
        return helpLabel;
    }

    private void BindPlayClipCollapse()
    {
        if (data is not PlayClipNode playClipNode) return;

        Toggle collapse = this.Q<Toggle>("collapse-button");
        if (collapse != null)
        {
            collapse.tooltip = "ノード全体の折りたたみと展開";
            collapse.RegisterValueChangedCallback(_ =>
            {
                if (syncingPlayClipCollapse) return;
                RecordNodeUndo("ノードを折りたたむ");
                playClipNode.nodeExpanded = expanded;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
        }

        Undo.undoRedoPerformed += SyncPlayClipCollapse;
        RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= SyncPlayClipCollapse);
    }

    private void SyncPlayClipCollapse()
    {
        if (syncingPlayClipCollapse || data is not PlayClipNode playClipNode) return;

        syncingPlayClipCollapse = true;
        try
        {
            if (expanded != playClipNode.nodeExpanded)
                expanded = playClipNode.nodeExpanded;
            if (playClipFoldButton != null && playClipSettingsBody != null)
                ApplySettingsFold(playClipFoldButton, playClipSettingsBody, playClipNode.settingsExpanded);
        }
        finally
        {
            syncingPlayClipCollapse = false;
        }
    }

    private void BindChoiceCollapse()
    {
        if (data is not ChoiceNode choiceNode) return;

        Toggle collapse = this.Q<Toggle>("collapse-button");
        if (collapse != null)
        {
            collapse.tooltip = "ノード全体の折りたたみと展開";
            collapse.RegisterValueChangedCallback(_ =>
            {
                if (syncingChoiceCollapse) return;
                RecordNodeUndo("ノードを折りたたむ");
                choiceNode.nodeExpanded = expanded;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
            });
        }

        Undo.undoRedoPerformed += SyncChoiceCollapse;
        RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= SyncChoiceCollapse);
    }

    private void SyncChoiceCollapse()
    {
        if (syncingChoiceCollapse || data is not ChoiceNode choiceNode) return;

        syncingChoiceCollapse = true;
        try
        {
            if (expanded != choiceNode.nodeExpanded)
                expanded = choiceNode.nodeExpanded;
            if (choiceFoldButton != null && choiceSettingsBody != null)
                ApplySettingsFold(choiceFoldButton, choiceSettingsBody, choiceNode.settingsExpanded);
            ApplyChoiceNodeChrome();
        }
        finally
        {
            syncingChoiceCollapse = false;
        }
    }

    private void BindExitGameCollapse()
    {
        if (data is not ExitGameNode exitGameNode) return;

        Toggle collapse = this.Q<Toggle>("collapse-button");
        if (collapse != null)
        {
            collapse.tooltip = "ノード全体の折りたたみと展開";
            collapse.RegisterValueChangedCallback(_ =>
            {
                if (syncingExitGameCollapse) return;
                RecordNodeUndo("ノードを折りたたむ");
                exitGameNode.nodeExpanded = expanded;
                UnityEditor.EditorUtility.SetDirty(exitGameNode);
            });
        }

        Undo.undoRedoPerformed += SyncExitGameCollapse;
        RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= SyncExitGameCollapse);
    }

    private void SyncExitGameCollapse()
    {
        if (syncingExitGameCollapse || data is not ExitGameNode exitGameNode) return;

        syncingExitGameCollapse = true;
        try
        {
            if (expanded != exitGameNode.nodeExpanded)
                expanded = exitGameNode.nodeExpanded;
            if (exitGameFoldButton != null && exitGameSettingsBody != null)
                ApplySettingsFold(exitGameFoldButton, exitGameSettingsBody, exitGameNode.settingsExpanded);
        }
        finally
        {
            syncingExitGameCollapse = false;
        }
    }

    private static void ApplySettingsFold(Button button, VisualElement body, bool open)
    {
        if (button != null) button.text = open ? "▼ 設定" : "▶ 設定";
        if (body != null) body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ApplyChoiceNodeChrome()
    {
        style.width = ChoiceNodeWidth;
        style.minWidth = ChoiceNodeWidth;
        style.maxWidth = ChoiceNodeWidth;

        Label titleLabel = this.Q<Label>("title-label");
        if (titleLabel != null)
        {
            titleLabel.style.flexGrow = 1;
            titleLabel.style.flexShrink = 1;
            titleLabel.style.minWidth = 0;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.maxWidth = ChoiceNodeWidth - 48f;
            titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            titleLabel.tooltip = title;
        }

        titleContainer.style.overflow = Overflow.Hidden;
        inputContainer.style.flexShrink = 1;
        inputContainer.style.minWidth = 0;
        outputContainer.style.flexShrink = 1;
        outputContainer.style.minWidth = 0;
        CompactChoicePorts(inputContainer);
        CompactChoicePorts(outputContainer);
    }

    private void CompactChoicePorts(VisualElement container)
    {
        if (container == null) return;

        foreach (VisualElement child in container.Children())
        {
            if (child is Port port)
                CompactChoicePort(port);
        }
    }

    private static void CompactChoicePort(Port port)
    {
        if (port == null) return;

        port.style.flexShrink = 1;
        port.style.minWidth = 0;
        port.style.maxWidth = 168;
        port.tooltip = port.portName;

        foreach (VisualElement child in port.Children())
        {
            if (child is not Label label) continue;

            label.style.flexShrink = 1;
            label.style.minWidth = 0;
            label.style.maxWidth = 132;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.tooltip = port.portName;
        }
    }

    private Toggle AddUiParentToggle(BaseNode node, bool current, Action<bool> setValue, VisualElement parent = null)
    {
        Toggle toggle = new Toggle("UI Parent 配下に出す")
        {
            value = current,
            tooltip = "オン: StoryPlayer の UI Parent の子として生成します。オフ: Canvas 直下。Parent 未指定時はどちらも Canvas 直下です"
        };
        toggle.RegisterValueChangedCallback(evt =>
        {
            RecordNodeUndo();
            setValue(evt.newValue);
            UnityEditor.EditorUtility.SetDirty(node);
        });
        (parent ?? extensionContainer).Add(toggle);
        return toggle;
    }

    private void ApplyRedirectNodeStyle()
    {
        title = "";
        titleButtonContainer.style.display = DisplayStyle.None;
        extensionContainer.style.display = DisplayStyle.None;

        titleContainer.style.height = 0;
        titleContainer.style.minHeight = 0;
        titleContainer.style.paddingTop = 0;
        titleContainer.style.paddingBottom = 0;
        titleContainer.style.marginTop = 0;
        titleContainer.style.marginBottom = 0;
        titleContainer.style.backgroundColor = Color.clear;

        Color body = new Color(0.28f, 0.28f, 0.28f);
        style.width = 56;
        style.minWidth = 56;
        style.maxWidth = 56;
        style.minHeight = 24;
        style.paddingLeft = 0;
        style.paddingRight = 0;
        style.paddingTop = 0;
        style.paddingBottom = 0;
        style.borderTopLeftRadius = 8;
        style.borderTopRightRadius = 8;
        style.borderBottomLeftRadius = 8;
        style.borderBottomRightRadius = 8;
        mainContainer.style.backgroundColor = new StyleColor(body);
        mainContainer.style.borderTopLeftRadius = 8;
        mainContainer.style.borderTopRightRadius = 8;
        mainContainer.style.borderBottomLeftRadius = 8;
        mainContainer.style.borderBottomRightRadius = 8;

        VisualElement contents = mainContainer.Q("contents");
        VisualElement divider = contents?.Q("divider");
        if (divider != null)
        {
            divider.style.display = DisplayStyle.None;
        }

        VisualElement top = contents?.Q("top");
        if (top != null)
        {
            top.style.minHeight = 24;
            top.style.alignItems = Align.Center;
            top.style.paddingTop = 0;
            top.style.paddingBottom = 0;
        }

        inputContainer.style.width = Length.Percent(50);
        inputContainer.style.paddingLeft = 0;
        inputContainer.style.paddingRight = 0;
        outputContainer.style.width = Length.Percent(50);
        outputContainer.style.paddingLeft = 0;
        outputContainer.style.paddingRight = 0;

        HidePortText(inputContainer);
        HidePortText(outputContainer);
    }

    private static void HidePortText(VisualElement container)
    {
        foreach (VisualElement child in container.Children())
        {
            if (child is not Port port) continue;

            port.style.paddingLeft = 0;
            port.style.paddingRight = 0;
            port.style.marginLeft = 0;
            port.style.marginRight = 0;

            foreach (VisualElement portChild in port.Children())
            {
                if (portChild.name == "connector") continue;
                portChild.style.display = DisplayStyle.None;
                portChild.style.width = 0;
                portChild.style.minWidth = 0;
                portChild.style.marginLeft = 0;
                portChild.style.marginRight = 0;
                portChild.style.paddingLeft = 0;
                portChild.style.paddingRight = 0;
            }
        }
    }

    private void DrawNodeSettings()
    {
        if (data is PlayClipNode playClipNode)
        {
            Label helpLabel = AddHelpButton(PlayClipNode.Summary);
            if (helpLabel != null) helpLabel.style.maxWidth = 240;

            playClipSettingsBody = new VisualElement();
            playClipFoldButton = new Button(() =>
            {
                RecordNodeUndo("設定を折りたたむ");
                playClipNode.settingsExpanded = !playClipNode.settingsExpanded;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
                ApplySettingsFold(playClipFoldButton, playClipSettingsBody, playClipNode.settingsExpanded);
                RefreshExpandedState();
            })
            {
                tooltip = "設定項目の折りたたみと展開"
            };
            ApplySettingsFold(playClipFoldButton, playClipSettingsBody, playClipNode.settingsExpanded);
            extensionContainer.Add(playClipFoldButton);
            extensionContainer.Add(playClipSettingsBody);

            ObjectField clipField = new ObjectField("Video Clip")
            {
                objectType = typeof(VideoClip),
                value = playClipNode.clip
            };

            clipField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.clip = evt.newValue as VideoClip;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
                RefreshDynamicTitle();
                RefreshMediaThumbnail();
            });
            playClipSettingsBody.Add(clipField);

            ObjectField audioField = new ObjectField("Audio Clip")
            {
                objectType = typeof(AudioClip),
                value = playClipNode.audioClip
            };
            audioField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.audioClip = evt.newValue as AudioClip;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
                RefreshDynamicTitle();
            });
            playClipSettingsBody.Add(audioField);

            EnumField busField = new EnumField("出力", playClipNode.audioBus)
            {
                tooltip = "Audio Clip を BGM / Voice / SE のどのスライダーで鳴らすか"
            };
            busField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.audioBus = (StoryAudioBus)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(busField);

            FloatField fadeField = new FloatField("映像クロスフェード(秒)")
            {
                value = playClipNode.crossFadeDuration,
                tooltip = PlayClipNode.VideoFadeTooltip
            };
            fadeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.crossFadeDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(fadeField);

            FloatField audioFadeField = new FloatField("音声フェード(秒)")
            {
                value = playClipNode.audioFadeDuration,
                tooltip = PlayClipNode.AudioFadeTooltip
            };
            audioFadeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.audioFadeDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(audioFadeField);

            Toggle videoLoopToggle = new Toggle("動画ループ")
            {
                value = playClipNode.isLooping,
                tooltip = "Video Clip だけをループします"
            };
            videoLoopToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.isLooping = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(videoLoopToggle);

            Toggle audioLoopToggle = new Toggle("音声ループ")
            {
                value = playClipNode.audioLoop,
                tooltip = "Audio Clip だけをループします"
            };
            audioLoopToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.audioLoop = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(audioLoopToggle);

            FloatField outputTimeField = new FloatField("出力タイミング(秒)")
            {
                value = playClipNode.portOutputTime,
                tooltip = PlayClipNode.OutputTimingTooltip
            };
            outputTimeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playClipNode.portOutputTime = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            playClipSettingsBody.Add(outputTimeField);
        }
        else if (data is ExitGameNode exitGameNode)
        {
            Label helpLabel = AddHelpButton(ExitGameNode.Summary);
            if (helpLabel != null) helpLabel.style.maxWidth = 220;

            exitGameSettingsBody = new VisualElement();
            exitGameFoldButton = new Button(() =>
            {
                RecordNodeUndo("設定を折りたたむ");
                exitGameNode.settingsExpanded = !exitGameNode.settingsExpanded;
                UnityEditor.EditorUtility.SetDirty(exitGameNode);
                ApplySettingsFold(exitGameFoldButton, exitGameSettingsBody, exitGameNode.settingsExpanded);
                RefreshExpandedState();
            })
            {
                tooltip = "設定項目の折りたたみと展開"
            };
            ApplySettingsFold(exitGameFoldButton, exitGameSettingsBody, exitGameNode.settingsExpanded);
            extensionContainer.Add(exitGameFoldButton);
            extensionContainer.Add(exitGameSettingsBody);

            Label note = new Label("到達すると終了します。次のノードへは進みません。");
            note.style.whiteSpace = WhiteSpace.Normal;
            note.style.color = new StyleColor(new Color(0.82f, 0.82f, 0.82f));
            note.style.marginTop = 2;
            note.style.marginBottom = 4;
            note.style.marginLeft = 4;
            note.style.marginRight = 4;
            exitGameSettingsBody.Add(note);
        }
        else if (data is PlayImageNode playImageNode)
        {
            ObjectField imageField = new ObjectField("Image")
            {
                objectType = typeof(Texture2D),
                value = playImageNode.image
            };
            imageField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.image = evt.newValue as Texture2D;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
                RefreshDynamicTitle();
                RefreshMediaThumbnail();
            });
            extensionContainer.Add(imageField);

            FloatField durationField = new FloatField("表示秒数")
            {
                value = playImageNode.duration
            };
            durationField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.duration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(durationField);

            ObjectField audioField = new ObjectField("Audio Clip")
            {
                objectType = typeof(AudioClip),
                value = playImageNode.audioClip
            };
            audioField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.audioClip = evt.newValue as AudioClip;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(audioField);

            EnumField imageBusField = new EnumField("出力", playImageNode.audioBus)
            {
                tooltip = "Audio Clip を BGM / Voice / SE のどのスライダーで鳴らすか"
            };
            imageBusField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.audioBus = (StoryAudioBus)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(imageBusField);

            FloatField fadeField = new FloatField("クロスフェード(秒)")
            {
                value = playImageNode.crossFadeDuration
            };
            fadeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.crossFadeDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(fadeField);

            Toggle loopToggle = new Toggle("Loop")
            {
                value = playImageNode.isLooping
            };
            loopToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.isLooping = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(loopToggle);

            FloatField outputTimeField = new FloatField("出力タイミング(秒)")
            {
                value = playImageNode.portOutputTime,
                tooltip = "0: 開始と同時に Next へ信号 / 負の値: 表示終了時 / 表示秒数以上: 表示終了時"
            };
            outputTimeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                playImageNode.portOutputTime = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(outputTimeField);
        }
        else if (data is SubGraphNode subGraphNode)
        {
            StoryGraph ownerGraph = UnityEditor.AssetDatabase.LoadAssetAtPath<StoryGraph>(
                UnityEditor.AssetDatabase.GetAssetPath(subGraphNode));

            ObjectField graphField = new ObjectField("Sub Graph")
            {
                objectType = typeof(StoryGraph),
                value = subGraphNode.subGraph
            };

            Label cycleWarning = new Label("循環参照です。自分自身や、自分を含むグラフは指定できません。")
            {
                style =
                {
                    color = new StyleColor(new Color(1f, 0.55f, 0.35f)),
                    whiteSpace = WhiteSpace.Normal
                }
            };
            cycleWarning.style.display = StoryGraph.WouldCreateCycle(ownerGraph, subGraphNode.subGraph)
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            graphField.RegisterValueChangedCallback(evt =>
            {
                StoryGraph assigned = evt.newValue as StoryGraph;
                if (StoryGraph.WouldCreateCycle(ownerGraph, assigned))
                {
                    Debug.LogError("【SubGraph】循環参照になるため設定できません。");
                    graphField.SetValueWithoutNotify(subGraphNode.subGraph);
                    return;
                }

                RecordNodeUndo();
                subGraphNode.subGraph = assigned;
                UnityEditor.EditorUtility.SetDirty(subGraphNode);
                RefreshDynamicTitle();
                cycleWarning.style.display = DisplayStyle.None;
            });
            extensionContainer.Add(graphField);
            extensionContainer.Add(cycleWarning);

            Button openButton = new Button(() =>
            {
                if (subGraphNode.subGraph != null)
                {
                    UnityEditor.AssetDatabase.OpenAsset(subGraphNode.subGraph);
                }
            })
            {
                text = "サブグラフを開く",
                tooltip = "このサブグラフを開きます。上部の「戻る」やパンくずで元のグラフに戻れます。"
            };
            extensionContainer.Add(openButton);

            RefreshDynamicTitle();

            titleContainer.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount < 2 || subGraphNode.subGraph == null) return;
                UnityEditor.AssetDatabase.OpenAsset(subGraphNode.subGraph);
                evt.StopPropagation();
            });
        }
        else if (data is LabelNode labelNode)
        {
            TextField nameField = new TextField("名前")
            {
                value = labelNode.labelName,
                tooltip = "Goto ノードのセレクションに表示される行先名です。"
            };
            nameField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                labelNode.labelName = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(labelNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(nameField);

            Label hint = new Label("メイン進行に置くと通過します。Goto からのジャンプ先にもなります。")
            {
                style =
                {
                    color = new StyleColor(new Color(0.75f, 0.75f, 0.75f)),
                    whiteSpace = WhiteSpace.Normal,
                    marginTop = 4
                }
            };
            extensionContainer.Add(hint);
        }
        else if (data is GotoNode gotoNode)
        {
            DrawGotoSettings(gotoNode);
        }
        else if (data is WaitNode waitNode)
        {
            FloatField secField = new FloatField("Sec")
            {
                value = waitNode.sec,
                tooltip = "Next に進むまでの待ち時間（秒）。Time.deltaTime を積算して測るため、フレームレートには依存しない。"
            };
            secField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                waitNode.sec = Mathf.Max(0f, evt.newValue);
                secField.SetValueWithoutNotify(waitNode.sec);
                UnityEditor.EditorUtility.SetDirty(waitNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(secField);
        }
        else if (data is VariableDeclareNode declareNode)
        {
            DrawVariableDeclareSettings(declareNode);
        }
        // ==========================================
        // ★追加：もしデータが ConditionNode だったら…
        // ==========================================
        else if (data is ConditionNode conditionNode)
        {
            AddVariableDropdown("変数名", () => conditionNode.variableName, value => conditionNode.variableName = value, "比較する変数");

            EnumField opField = new EnumField("条件", conditionNode.comparison);
            opField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                conditionNode.comparison = (ConditionOperator)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(conditionNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(opField);

            IntegerField valField = new IntegerField("比較する値")
            {
                value = conditionNode.compareValue,
                tooltip = "Bool変数は Off=0 / On=1 として判定します"
            };
            valField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                conditionNode.compareValue = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(conditionNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(valField);
        }
        // ==========================================
        // ★追加：もしデータが VariableOperationNode だったら…
        // ==========================================
        else if (data is VariableOperationNode varOpNode)
        {
            AddVariableDropdown("対象変数", () => varOpNode.variableName, value => varOpNode.variableName = value, "演算する変数");

            EnumField opField = new EnumField("演算", varOpNode.operation);
            opField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                varOpNode.operation = (VariableOperation)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(opField);

            Toggle useVarToggle = new Toggle("他の変数値を使用")
            {
                value = varOpNode.useVariableOperand
            };

            IntegerField valField = new IntegerField("数値")
            {
                value = varOpNode.operandValue
            };
            valField.style.display = varOpNode.useVariableOperand ? DisplayStyle.None : DisplayStyle.Flex;
            valField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                varOpNode.operandValue = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });

            VisualElement operandVarField = CreateVariableDropdown(
                "参照変数",
                () => varOpNode.operandVariableName,
                value => varOpNode.operandVariableName = value,
                "右辺に使う変数");
            operandVarField.style.display = varOpNode.useVariableOperand ? DisplayStyle.Flex : DisplayStyle.None;

            useVarToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                varOpNode.useVariableOperand = evt.newValue;
                valField.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                operandVarField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });

            extensionContainer.Add(useVarToggle);
            extensionContainer.Add(valField);
            extensionContainer.Add(operandVarField);
        }
        else if (data is BoolOperationNode boolOpNode)
        {
            AddVariableDropdown("対象変数", () => boolOpNode.variableName, value => boolOpNode.variableName = value, "On / Off / Toggle する Bool 変数");

            EnumField opField = new EnumField("操作", boolOpNode.operation);
            opField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                boolOpNode.operation = (BoolOperation)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(boolOpNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(opField);
        }
        else if (data is PlaybackSpeedNode speedNode)
        {
            EnumField opField = new EnumField("演算", speedNode.operation);
            opField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                speedNode.operation = (VariableOperation)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(speedNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(opField);

            Toggle useVarToggle = new Toggle("他の変数値を使用")
            {
                value = speedNode.useVariableOperand
            };

            FloatField valField = new FloatField("数値")
            {
                value = speedNode.operandValue,
                tooltip = "SET: この値にする / ADD: 現在速度に足す（1が等速、範囲 0〜10）"
            };
            valField.style.display = speedNode.useVariableOperand ? DisplayStyle.None : DisplayStyle.Flex;
            valField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                speedNode.operandValue = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(speedNode);
                RefreshDynamicTitle();
            });

            VisualElement operandVarField = CreateVariableDropdown(
                "参照変数",
                () => speedNode.operandVariableName,
                value => speedNode.operandVariableName = value,
                "速度の右辺に使う変数");
            operandVarField.style.display = speedNode.useVariableOperand ? DisplayStyle.Flex : DisplayStyle.None;

            useVarToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                speedNode.useVariableOperand = evt.newValue;
                valField.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                operandVarField.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                UnityEditor.EditorUtility.SetDirty(speedNode);
                RefreshDynamicTitle();
            });

            extensionContainer.Add(useVarToggle);
            extensionContainer.Add(valField);
            extensionContainer.Add(operandVarField);
        }
        // ==========================================
        // ★追加：もしデータが ChoiceNode だったら…
        // ==========================================
        else if (data is ChoiceNode choiceNode)
        {
            Label helpLabel = AddHelpButton(ChoiceNode.Summary);
            if (helpLabel != null) helpLabel.style.maxWidth = 220;

            choiceSettingsBody = new VisualElement();
            choiceSettingsBody.style.minWidth = 0;
            choiceFoldButton = new Button(() =>
            {
                RecordNodeUndo("設定を折りたたむ");
                choiceNode.settingsExpanded = !choiceNode.settingsExpanded;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
                ApplySettingsFold(choiceFoldButton, choiceSettingsBody, choiceNode.settingsExpanded);
                RefreshExpandedState();
            })
            {
                tooltip = "設定項目の折りたたみと展開"
            };
            ApplySettingsFold(choiceFoldButton, choiceSettingsBody, choiceNode.settingsExpanded);
            extensionContainer.Add(choiceFoldButton);
            extensionContainer.Add(choiceSettingsBody);

            ObjectField prefabField = new ObjectField("UI Prefab")
            {
                objectType = typeof(GameObject),
                value = choiceNode.branchUIPrefab
            };
            prefabField.style.minWidth = 0;
            prefabField.style.flexShrink = 1;
            prefabField.labelElement.style.minWidth = 64;
            prefabField.labelElement.style.width = 64;
            prefabField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                choiceNode.branchUIPrefab = evt.newValue as GameObject;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
            });
            choiceSettingsBody.Add(prefabField);
            AddUiParentToggle(choiceNode, choiceNode.parentToUiParent, value => choiceNode.parentToUiParent = value, choiceSettingsBody);

            VisualElement choiceListContainer = new VisualElement();
            choiceListContainer.style.minWidth = 0;

            Button autoDetectBtn = new Button(() =>
            {
                if (choiceNode.branchUIPrefab != null)
                {
                    var buttons = choiceNode.branchUIPrefab.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    if (buttons.Length > 0)
                    {
                        BeginGraphEdit("選択肢を自動取得");
                        choiceNode.choices.Clear();
                        foreach (var b in buttons)
                        {
                            if (!choiceNode.choices.Contains(b.gameObject.name))
                            {
                                choiceNode.choices.Add(b.gameObject.name);
                            }
                        }
                        UnityEditor.EditorUtility.SetDirty(choiceNode);
                        RefreshDynamicTitle();
                        BuildChoicePorts(choiceNode);
                        RedrawChoiceList(choiceNode, choiceListContainer);
                        EndGraphEdit();
                    }
                }
            })
            {
                text = "🔍 プレハブからボタンを自動取得",
                tooltip = "UI Prefab 内の Button 名を選択肢にします。名前が一致したボタンだけ分岐します"
            };
            autoDetectBtn.style.whiteSpace = WhiteSpace.Normal;
            autoDetectBtn.style.minWidth = 0;
            choiceSettingsBody.Add(autoDetectBtn);

            choiceSettingsBody.Add(choiceListContainer);
            RedrawChoiceList(choiceNode, choiceListContainer);

            Button addChoiceBtn = new Button(() =>
            {
                BeginGraphEdit("選択肢を追加");
                string choiceName = $"Choice {choiceNode.choices.Count + 1}";
                choiceNode.choices.Add(choiceName);
                UnityEditor.EditorUtility.SetDirty(choiceNode);
                RefreshDynamicTitle();
                AddOutputPort(choiceName);
                RedrawChoiceList(choiceNode, choiceListContainer);
                EndGraphEdit();
            })
            {
                text = "＋ 選択肢(ポート)を追加"
            };
            addChoiceBtn.style.whiteSpace = WhiteSpace.Normal;
            addChoiceBtn.style.minWidth = 0;
            choiceSettingsBody.Add(addChoiceBtn);
        }
        else if (data is TextNode textNode)
        {
            ObjectField prefabField = new ObjectField("UI Prefab")
            {
                objectType = typeof(GameObject),
                value = textNode.textUIPrefab,
                tooltip = "TypewriterEffect 付きのプレハブを指定します"
            };
            prefabField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.textUIPrefab = evt.newValue as GameObject;
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(prefabField);
            AddUiParentToggle(textNode, textNode.parentToUiParent, value => textNode.parentToUiParent = value);

            TextField messageField = new TextField("本文")
            {
                value = textNode.message,
                multiline = true
            };
            messageField.style.minHeight = 64;
            messageField.style.whiteSpace = WhiteSpace.Normal;
            messageField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.message = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(textNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(messageField);

            Toggle waitToggle = new Toggle("Typewriter 終了待ち")
            {
                value = textNode.waitUntilComplete,
                tooltip = "オン: 全文表示後のクリックから Next までの待ちを開始 / オフ: Typewriter 終了後に待ちを開始"
            };
            Toggle skipToggle = new Toggle("入力中クリックでスキップ")
            {
                value = textNode.clickToSkip,
                tooltip = "入力中のクリックは全文表示のみ。進むのは全文が出てからのクリック"
            };
            skipToggle.style.display = textNode.waitUntilComplete ? DisplayStyle.Flex : DisplayStyle.None;

            waitToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.waitUntilComplete = evt.newValue;
                skipToggle.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(waitToggle);

            skipToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.clickToSkip = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(skipToggle);

            ColorField colorField = new ColorField("フォント色")
            {
                value = textNode.fontColor,
                showAlpha = true
            };
            colorField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.fontColor = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(colorField);

            FloatField fadeInField = new FloatField("フェードイン(秒)")
            {
                value = textNode.fadeInDuration
            };
            fadeInField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.fadeInDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(fadeInField);

            FloatField fadeOutField = new FloatField("フェードアウト(秒)")
            {
                value = textNode.fadeOutDuration,
                tooltip = "Next の時点から逆算して始まります。待ちより長い場合は途中からフェードします"
            };
            fadeOutField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.fadeOutDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(fadeOutField);

            Toggle destroyToggle = new Toggle("終了後にUIを破棄")
            {
                value = textNode.autoDestroyUI,
                tooltip = "Next に進むのと同時に破棄します。オフなら UI を残したまま Next へ進みます"
            };
            FloatField delayField = new FloatField("Nextまでの待ち(秒)")
            {
                value = textNode.destroyDelay,
                tooltip = "Next に進むまでの時間。フェードアウトはこの時点から逆算して始まります"
            };
            delayField.style.display = DisplayStyle.Flex;

            destroyToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.autoDestroyUI = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(destroyToggle);

            delayField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                textNode.destroyDelay = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(textNode);
            });
            extensionContainer.Add(delayField);
        }
        else if (data is VariableTextNode variableTextNode)
        {
            AddHelpButton("実行した瞬間の変数値を Text に書きます。UI は秒数で破棄し、Next へ進む秒数はそれとは別に指定します。");

            ObjectField prefabField = new ObjectField("UI Prefab")
            {
                objectType = typeof(GameObject),
                value = variableTextNode.textUIPrefab,
                tooltip = "子階層の TextMeshPro、無ければ UI Text に値を書きます"
            };
            prefabField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                variableTextNode.textUIPrefab = evt.newValue as GameObject;
                UnityEditor.EditorUtility.SetDirty(variableTextNode);
            });
            extensionContainer.Add(prefabField);
            AddUiParentToggle(variableTextNode, variableTextNode.parentToUiParent, value => variableTextNode.parentToUiParent = value);

            AddVariableDropdown("変数名", () => variableTextNode.variableName, value => variableTextNode.variableName = value, "実行した瞬間の値を表示します");

            FloatField lifetimeField = new FloatField("破棄までの秒")
            {
                value = variableTextNode.lifetime,
                tooltip = "UI を出してから AutoRelease で破棄するまでの秒。Next の待ちとは別です"
            };
            lifetimeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                variableTextNode.lifetime = Mathf.Max(0f, evt.newValue);
                lifetimeField.SetValueWithoutNotify(variableTextNode.lifetime);
                UnityEditor.EditorUtility.SetDirty(variableTextNode);
            });
            extensionContainer.Add(lifetimeField);

            FloatField nextField = new FloatField("Nextまでの待ち(秒)")
            {
                value = variableTextNode.nextDelay,
                tooltip = "Next に進むまでの秒。表示・破棄の時間とは別に測ります"
            };
            nextField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                variableTextNode.nextDelay = Mathf.Max(0f, evt.newValue);
                nextField.SetValueWithoutNotify(variableTextNode.nextDelay);
                UnityEditor.EditorUtility.SetDirty(variableTextNode);
            });
            extensionContainer.Add(nextField);
        }
        else if (data is SpawnPrefabNode spawnNode)
        {
            AddHelpButton("Choice などの出力からつなぐと、クリック位置にプレハブを出します。破棄はプレハブ側の AutoRelease が担当します。");

            ObjectField prefabField = new ObjectField("Prefab")
            {
                objectType = typeof(GameObject),
                value = spawnNode.prefab
            };
            prefabField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                spawnNode.prefab = evt.newValue as GameObject;
                UnityEditor.EditorUtility.SetDirty(spawnNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(prefabField);

            Toggle pointerToggle = new Toggle("ポインタ位置に出す")
            {
                value = spawnNode.spawnAtPointer,
                tooltip = "Choice / Text のクリック位置など、いまのポインタ座標に生成します"
            };
            pointerToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                spawnNode.spawnAtPointer = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(spawnNode);
            });
            extensionContainer.Add(pointerToggle);

            Toggle canvasToggle = new Toggle("Canvas 配下に出す")
            {
                value = spawnNode.parentToCanvas,
                tooltip = "オン: UI / オフ: ワールド座標"
            };
            canvasToggle.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                spawnNode.parentToCanvas = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(spawnNode);
            });
            extensionContainer.Add(canvasToggle);

            Toggle uiParentToggle = AddUiParentToggle(spawnNode, spawnNode.parentToUiParent, value => spawnNode.parentToUiParent = value);
            uiParentToggle.style.display = spawnNode.parentToCanvas ? DisplayStyle.Flex : DisplayStyle.None;
            canvasToggle.RegisterValueChangedCallback(evt =>
            {
                uiParentToggle.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            FloatField distanceField = new FloatField("ワールド距離")
            {
                value = spawnNode.worldDistance,
                tooltip = "Canvas 配下がオフのとき、カメラからこの距離に出します"
            };
            distanceField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                spawnNode.worldDistance = Mathf.Max(0.01f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(spawnNode);
            });
            extensionContainer.Add(distanceField);
        }
        else if (data is RandomBranchNode randomBranchNode)
        {
            AddHelpButton("比率は相対ウェイトです（合計が100でなくても可）");

            VisualElement branchListContainer = new VisualElement();
            extensionContainer.Add(branchListContainer);
            RedrawRandomBranchList(randomBranchNode, branchListContainer);

            Button addBranchBtn = new Button(() =>
            {
                BeginGraphEdit("分岐を追加");
                EnsureRandomBranches(randomBranchNode);
                RandomBranchEntry added = new RandomBranchEntry
                {
                    name = RandomBranchNode.NextDefaultName(randomBranchNode.branches),
                    weight = 1f
                };
                randomBranchNode.branches.Add(added);
                UnityEditor.EditorUtility.SetDirty(randomBranchNode);
                AddOutputPort(added.name);
                RedrawRandomBranchList(randomBranchNode, branchListContainer);
                EndGraphEdit();
            })
            {
                text = "＋ 分岐を追加"
            };
            extensionContainer.Add(addBranchBtn);
        }
        else if (data is ConditionBranchNode conditionBranchNode)
        {
            AddVariableDropdown("変数名", () => conditionBranchNode.variableName, value => conditionBranchNode.variableName = value, "判定する数値変数（Int / Float）");

            VisualElement branchListContainer = new VisualElement();
            DropdownField opField = new DropdownField("比較", ThresholdOperatorChoices, ClampOperatorSymbol(conditionBranchNode.comparison));
            opField.tooltip = "現在値と設定値の比較演算子";
            Label helpLabel = AddHelpButton(conditionBranchNode.GetHelpText());
            opField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                conditionBranchNode.comparison = OperatorFromSymbol(evt.newValue);
                UnityEditor.EditorUtility.SetDirty(conditionBranchNode);
                if (helpLabel != null) helpLabel.text = conditionBranchNode.GetHelpText();
                RefreshDynamicTitle();
                RedrawConditionBranchList(conditionBranchNode, branchListContainer);
            });
            extensionContainer.Add(opField);

            extensionContainer.Add(branchListContainer);
            RedrawConditionBranchList(conditionBranchNode, branchListContainer);

            Button addBranchBtn = new Button(() =>
            {
                BeginGraphEdit("分岐を追加");
                EnsureConditionBranches(conditionBranchNode);
                ConditionBranchEntry added = new ConditionBranchEntry
                {
                    name = ConditionBranchNode.NextDefaultName(conditionBranchNode.branches),
                    threshold = ConditionBranchNode.NextDefaultThreshold(conditionBranchNode.branches)
                };
                conditionBranchNode.branches.Add(added);
                UnityEditor.EditorUtility.SetDirty(conditionBranchNode);
                AddOutputPort(added.name);
                RedrawConditionBranchList(conditionBranchNode, branchListContainer);
                EndGraphEdit();
            })
            {
                text = "＋ 分岐を追加"
            };
            extensionContainer.Add(addBranchBtn);
        }
    }

    private Port CreateOutputPort(string portName)
    {
        Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        port.portName = portName;
        if (data is ChoiceNode)
            CompactChoicePort(port);
        return port;
    }

    private List<Port> GetOutputPorts()
    {
        List<Port> ports = new List<Port>();
        foreach (VisualElement child in outputContainer.Children())
        {
            if (child is Port port)
            {
                ports.Add(port);
            }
        }
        return ports;
    }

    private void AddOutputPort(string portName, int insertIndex = -1)
    {
        Port port = CreateOutputPort(portName);
        List<Port> current = GetOutputPorts();
        if (insertIndex < 0 || insertIndex >= current.Count)
        {
            outputContainer.Add(port);
        }
        else
        {
            outputContainer.Insert(outputContainer.IndexOf(current[insertIndex]), port);
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    private void RemoveOutputPortAt(int index)
    {
        List<Port> ports = GetOutputPorts();
        if (index < 0 || index >= ports.Count) return;

        Port toRemove = ports[index];
        GraphView view = GetFirstAncestorOfType<GraphView>();
        if (view != null)
        {
            List<GraphElement> edges = new List<GraphElement>();
            foreach (Edge edge in toRemove.connections)
            {
                if (edge != null) edges.Add(edge);
            }

            if (edges.Count > 0)
            {
                bool previousIgnore = StoryGraphEditorHooks.IgnoreGraphViewChange;
                StoryGraphEditorHooks.IgnoreGraphViewChange = true;
                try
                {
                    view.DeleteElements(edges);
                }
                finally
                {
                    StoryGraphEditorHooks.IgnoreGraphViewChange = previousIgnore;
                }
            }
        }

        outputContainer.Remove(toRemove);
        RefreshExpandedState();
        RefreshPorts();
    }

    private void RenameOutputPortAt(int index, string newName)
    {
        List<Port> ports = GetOutputPorts();
        if (index < 0 || index >= ports.Count) return;
        ports[index].portName = newName;
        if (data is ChoiceNode)
            CompactChoicePort(ports[index]);
        RefreshExpandedState();
        RefreshPorts();
    }

    private sealed class OutputConnectionSnap
    {
        public string portName;
        public List<string> targetGuids = new List<string>();
    }

    private void RebuildOutputPortsPreservingEdges(Action rebuild)
    {
        bool previousIgnore = StoryGraphEditorHooks.IgnoreGraphViewChange;
        StoryGraphEditorHooks.IgnoreGraphViewChange = true;
        try
        {
            List<OutputConnectionSnap> snaps = CaptureOutputConnections();
            GraphView view = GetFirstAncestorOfType<GraphView>();
            rebuild();
            RemoveOrphanedEdges(view);
            RestoreOutputConnections(view, snaps);
            RefreshExpandedState();
            RefreshPorts();
        }
        finally
        {
            StoryGraphEditorHooks.IgnoreGraphViewChange = previousIgnore;
        }
    }

    private List<OutputConnectionSnap> CaptureOutputConnections()
    {
        List<OutputConnectionSnap> snaps = new List<OutputConnectionSnap>();
        foreach (Port port in GetOutputPorts())
        {
            OutputConnectionSnap snap = new OutputConnectionSnap { portName = port.portName };
            foreach (Edge edge in port.connections)
            {
                if (edge?.input?.node is StoryNodeUI target && !string.IsNullOrEmpty(target.guid))
                {
                    snap.targetGuids.Add(target.guid);
                }
            }

            snaps.Add(snap);
        }

        return snaps;
    }

    private void RemoveOrphanedEdges(GraphView view)
    {
        if (view == null) return;

        List<GraphElement> orphans = new List<GraphElement>();
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not Edge graphEdge) continue;
            if (graphEdge.output == null || graphEdge.output.node == null || graphEdge.input == null || graphEdge.input.node == null)
            {
                orphans.Add(graphEdge);
            }
        }

        if (orphans.Count > 0)
        {
            bool previousIgnore = StoryGraphEditorHooks.IgnoreGraphViewChange;
            StoryGraphEditorHooks.IgnoreGraphViewChange = true;
            try
            {
                view.DeleteElements(orphans);
            }
            finally
            {
                StoryGraphEditorHooks.IgnoreGraphViewChange = previousIgnore;
            }
        }
    }

    private void RestoreOutputConnections(GraphView view, List<OutputConnectionSnap> snaps)
    {
        if (view == null || snaps == null || snaps.Count == 0) return;

        Dictionary<string, StoryNodeUI> nodes = new Dictionary<string, StoryNodeUI>();
        foreach (GraphElement element in view.graphElements)
        {
            if (element is not StoryNodeUI nodeUI || string.IsNullOrEmpty(nodeUI.guid) || nodes.ContainsKey(nodeUI.guid)) continue;
            nodes[nodeUI.guid] = nodeUI;
        }

        List<Port> newPorts = GetOutputPorts();
        bool[] snapConsumed = new bool[snaps.Count];
        bool[] portRestored = new bool[newPorts.Count];

        for (int i = 0; i < newPorts.Count; i++)
        {
            for (int s = 0; s < snaps.Count; s++)
            {
                if (snapConsumed[s] || snaps[s].portName != newPorts[i].portName) continue;
                ConnectPortToGuids(view, newPorts[i], snaps[s].targetGuids, nodes);
                snapConsumed[s] = true;
                portRestored[i] = true;
                break;
            }
        }

        for (int i = 0; i < newPorts.Count; i++)
        {
            if (portRestored[i] || i >= snaps.Count || snapConsumed[i]) continue;
            ConnectPortToGuids(view, newPorts[i], snaps[i].targetGuids, nodes);
            snapConsumed[i] = true;
        }
    }

    private static void ConnectPortToGuids(GraphView view, Port outputPort, List<string> targetGuids, Dictionary<string, StoryNodeUI> nodes)
    {
        if (view == null || outputPort == null || targetGuids == null) return;

        for (int i = 0; i < targetGuids.Count; i++)
        {
            if (!nodes.TryGetValue(targetGuids[i], out StoryNodeUI target)) continue;

            Port input = null;
            foreach (VisualElement child in target.inputContainer.Children())
            {
                if (child is Port port)
                {
                    input = port;
                    break;
                }
            }

            if (input == null) continue;

            bool alreadyConnected = false;
            foreach (Edge existing in outputPort.connections)
            {
                if (existing != null && existing.input == input)
                {
                    alreadyConnected = true;
                    break;
                }
            }

            if (alreadyConnected) continue;

            Edge created = outputPort.ConnectTo(input);
            if (created != null)
            {
                view.AddElement(created);
            }
        }
    }

    private void BuildChoicePorts(ChoiceNode choiceNode)
    {
        RebuildOutputPortsPreservingEdges(() =>
        {
            outputContainer.Clear();
            if (choiceNode.choices == null || choiceNode.choices.Count == 0)
            {
                choiceNode.choices = new List<string> { "Choice 1" };
            }

            foreach (var choice in choiceNode.choices)
            {
                outputContainer.Add(CreateOutputPort(choice));
            }
        });
    }

    private void RedrawChoiceList(ChoiceNode choiceNode, VisualElement container)
    {
        container.Clear();
        for (int i = 0; i < choiceNode.choices.Count; i++)
        {
            int index = i;
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minWidth = 0;
            row.style.marginTop = 1;
            row.style.marginBottom = 1;

            Label indexLabel = new Label((index + 1).ToString());
            indexLabel.style.width = 16;
            indexLabel.style.minWidth = 16;
            indexLabel.style.flexShrink = 0;
            indexLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            indexLabel.style.marginRight = 4;
            row.Add(indexLabel);

            TextField tf = new TextField
            {
                value = choiceNode.choices[index],
                tooltip = "Button の名前。一致したものだけ分岐します"
            };
            tf.style.flexGrow = 1;
            tf.style.flexShrink = 1;
            tf.style.minWidth = 0;
            tf.labelElement.style.display = DisplayStyle.None;
            tf.RegisterValueChangedCallback(evt =>
            {
                BeginGraphEdit("選択肢名を変更");
                choiceNode.choices[index] = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
                RefreshDynamicTitle();
                RenameOutputPortAt(index, evt.newValue);
                EndGraphEdit();
            });
            row.Add(tf);

            if (choiceNode.choices.Count > 1)
            {
                Button delBtn = new Button(() =>
                {
                    BeginGraphEdit("選択肢を削除");
                    choiceNode.choices.RemoveAt(index);
                    UnityEditor.EditorUtility.SetDirty(choiceNode);
                    RefreshDynamicTitle();
                    RemoveOutputPortAt(index);
                    RedrawChoiceList(choiceNode, container);
                    EndGraphEdit();
                })
                {
                    text = "✕"
                };
                delBtn.style.width = 22;
                delBtn.style.minWidth = 22;
                delBtn.style.flexShrink = 0;
                delBtn.style.marginLeft = 2;
                row.Add(delBtn);
            }

            container.Add(row);
        }
    }

    private void BuildRandomBranchPorts(RandomBranchNode node)
    {
        RebuildOutputPortsPreservingEdges(() =>
        {
            outputContainer.Clear();
            EnsureRandomBranches(node);

            for (int i = 0; i < node.branches.Count; i++)
            {
                RandomBranchEntry entry = node.branches[i];
                if (entry == null)
                {
                    entry = new RandomBranchEntry { name = RandomBranchNode.NextDefaultName(node.branches), weight = 1f };
                    node.branches[i] = entry;
                }

                if (string.IsNullOrEmpty(entry.name))
                {
                    entry.name = RandomBranchNode.NextDefaultName(node.branches);
                }

                Port branchPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                branchPort.portName = entry.name;
                outputContainer.Add(branchPort);
            }
        });
    }

    private void RedrawRandomBranchList(RandomBranchNode node, VisualElement container)
    {
        container.Clear();
        EnsureRandomBranches(node);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 2;

        Label nameHeader = new Label("名前");
        nameHeader.style.flexGrow = 1;
        nameHeader.style.minWidth = 60;
        nameHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        header.Add(nameHeader);

        Label weightHeader = new Label("比率");
        weightHeader.style.width = 56;
        weightHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        header.Add(weightHeader);

        Label percentHeader = new Label("%");
        percentHeader.style.width = 48;
        percentHeader.style.unityTextAlign = TextAnchor.MiddleRight;
        percentHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        header.Add(percentHeader);

        VisualElement headerSpacer = new VisualElement();
        headerSpacer.style.width = node.branches.Count > 1 ? 25 : 0;
        header.Add(headerSpacer);
        container.Add(header);

        Label[] percentLabels = new Label[node.branches.Count];

        void RefreshPercents()
        {
            float total = node.GetTotalWeight();
            for (int i = 0; i < node.branches.Count; i++)
            {
                if (percentLabels[i] == null) continue;
                float weight = node.branches[i] != null ? Mathf.Max(0f, node.branches[i].weight) : 0f;
                percentLabels[i].text = total > 0f && weight > 0f
                    ? $"{weight / total * 100f:0.#}%"
                    : "0%";
            }
            RefreshDynamicTitle();
        }

        for (int i = 0; i < node.branches.Count; i++)
        {
            int index = i;
            RandomBranchEntry entry = node.branches[index];
            if (entry == null)
            {
                entry = new RandomBranchEntry { name = RandomBranchNode.NextDefaultName(node.branches), weight = 1f };
                node.branches[index] = entry;
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            TextField nameField = new TextField
            {
                value = entry.name
            };
            nameField.style.flexGrow = 1;
            nameField.style.minWidth = 60;
            nameField.RegisterValueChangedCallback(evt =>
            {
                BeginGraphEdit("分岐名を変更");
                entry.name = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(node);
                RenameOutputPortAt(index, evt.newValue);
                EndGraphEdit();
            });
            row.Add(nameField);

            FloatField weightField = new FloatField
            {
                value = entry.weight,
                tooltip = "分岐する比率（相対ウェイト）"
            };
            weightField.style.width = 56;
            weightField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                entry.weight = Mathf.Max(0f, evt.newValue);
                if (!Mathf.Approximately(weightField.value, entry.weight))
                {
                    weightField.SetValueWithoutNotify(entry.weight);
                }
                UnityEditor.EditorUtility.SetDirty(node);
                RefreshPercents();
            });
            row.Add(weightField);

            Label percentLabel = new Label();
            percentLabel.style.width = 48;
            percentLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            percentLabel.style.color = new StyleColor(new Color(0.85f, 0.85f, 0.85f));
            percentLabels[index] = percentLabel;
            row.Add(percentLabel);

            if (node.branches.Count > 1)
            {
                Button delBtn = new Button(() =>
                {
                    BeginGraphEdit("分岐を削除");
                    node.branches.RemoveAt(index);
                    UnityEditor.EditorUtility.SetDirty(node);
                    RemoveOutputPortAt(index);
                    RedrawRandomBranchList(node, container);
                    EndGraphEdit();
                })
                {
                    text = "✕"
                };
                delBtn.style.width = 25;
                row.Add(delBtn);
            }

            container.Add(row);
        }

        RefreshPercents();
    }

    private static void EnsureRandomBranches(RandomBranchNode node)
    {
        if (node.branches == null || node.branches.Count == 0)
        {
            node.branches = new System.Collections.Generic.List<RandomBranchEntry>
            {
                new RandomBranchEntry { name = "A", weight = 1f },
                new RandomBranchEntry { name = "B", weight = 1f }
            };
        }
    }

    private void BuildConditionBranchPorts(ConditionBranchNode node)
    {
        RebuildOutputPortsPreservingEdges(() =>
        {
            outputContainer.Clear();
            EnsureConditionBranches(node);

            for (int i = 0; i < node.branches.Count; i++)
            {
                ConditionBranchEntry entry = node.branches[i];
                if (entry == null)
                {
                    entry = new ConditionBranchEntry
                    {
                        name = ConditionBranchNode.NextDefaultName(node.branches),
                        threshold = 0f
                    };
                    node.branches[i] = entry;
                }

                if (string.IsNullOrEmpty(entry.name))
                {
                    entry.name = ConditionBranchNode.NextDefaultName(node.branches);
                }

                outputContainer.Add(CreateOutputPort(entry.name));
            }
        });
    }

    private void RedrawConditionBranchList(ConditionBranchNode node, VisualElement container)
    {
        container.Clear();
        EnsureConditionBranches(node);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.marginBottom = 2;

        Label nameHeader = new Label("名前");
        nameHeader.style.flexGrow = 1;
        nameHeader.style.minWidth = 60;
        nameHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        header.Add(nameHeader);

        Label thresholdHeader = new Label("設定値");
        thresholdHeader.style.width = 72;
        thresholdHeader.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        header.Add(thresholdHeader);

        VisualElement headerSpacer = new VisualElement();
        headerSpacer.style.width = node.branches.Count > 1 ? 25 : 0;
        header.Add(headerSpacer);
        container.Add(header);

        for (int i = 0; i < node.branches.Count; i++)
        {
            int index = i;
            ConditionBranchEntry entry = node.branches[index];
            if (entry == null)
            {
                entry = new ConditionBranchEntry
                {
                    name = ConditionBranchNode.NextDefaultName(node.branches),
                    threshold = 0f
                };
                node.branches[index] = entry;
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            TextField nameField = new TextField
            {
                value = entry.name
            };
            nameField.style.flexGrow = 1;
            nameField.style.minWidth = 60;
            nameField.RegisterValueChangedCallback(evt =>
            {
                BeginGraphEdit("分岐名を変更");
                string nextName = evt.newValue;
                if (string.IsNullOrEmpty(nextName))
                {
                    nextName = ConditionBranchNode.NextDefaultName(node.branches);
                    nameField.SetValueWithoutNotify(nextName);
                }

                entry.name = nextName;
                UnityEditor.EditorUtility.SetDirty(node);
                RenameOutputPortAt(index, nextName);
                RefreshDynamicTitle();
                EndGraphEdit();
            });
            row.Add(nameField);

            FloatField thresholdField = new FloatField
            {
                value = entry.threshold,
                tooltip = node.GetThresholdTooltip()
            };
            thresholdField.style.width = 72;
            thresholdField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                entry.threshold = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(node);
                RefreshDynamicTitle();
            });
            row.Add(thresholdField);

            if (node.branches.Count > 1)
            {
                Button delBtn = new Button(() =>
                {
                    BeginGraphEdit("分岐を削除");
                    node.branches.RemoveAt(index);
                    UnityEditor.EditorUtility.SetDirty(node);
                    RemoveOutputPortAt(index);
                    RedrawConditionBranchList(node, container);
                    EndGraphEdit();
                })
                {
                    text = "✕"
                };
                delBtn.style.width = 25;
                row.Add(delBtn);
            }

            container.Add(row);
        }
    }

    private static readonly List<string> ThresholdOperatorChoices = new List<string> { "==", ">", ">=", "<=", "<" };

    private static string ClampOperatorSymbol(ConditionOperator op)
    {
        string symbol = ConditionNode.GetOperatorSymbol(op);
        return ThresholdOperatorChoices.Contains(symbol) ? symbol : ">";
    }

    private static ConditionOperator OperatorFromSymbol(string symbol)
    {
        switch (symbol)
        {
            case "==": return ConditionOperator.Equal;
            case ">=": return ConditionOperator.GreaterOrEqual;
            case "<=": return ConditionOperator.LessOrEqual;
            case "<": return ConditionOperator.LessThan;
            default: return ConditionOperator.GreaterThan;
        }
    }

    private static void EnsureConditionBranches(ConditionBranchNode node)
    {
        if (node.branches == null || node.branches.Count == 0)
        {
            node.branches = new System.Collections.Generic.List<ConditionBranchEntry>
            {
                new ConditionBranchEntry { name = "A", threshold = 0f },
                new ConditionBranchEntry { name = "B", threshold = 1f }
            };
        }
    }

    /// <summary>
    /// 変数宣言ノードの一覧。1ノードで複数の変数を宣言する。
    /// </summary>
    private void DrawVariableDeclareSettings(VariableDeclareNode node)
    {
        AddHelpButton(node.GetHelpText());

        VisualElement list = new VisualElement();
        extensionContainer.Add(list);
        RedrawVariableDeclarations(node, list);

        Button addButton = new Button(() =>
        {
            BeginGraphEdit("変数を追加");
            if (node.variables == null) node.variables = new List<VariableDeclaration>();
            node.variables.Add(new VariableDeclaration { key = VariableDeclareNode.NextDefaultKey(node.variables) });
            UnityEditor.EditorUtility.SetDirty(node);
            RedrawVariableDeclarations(node, list);
            EndGraphEdit();
        })
        {
            text = "＋ 変数を追加"
        };
        extensionContainer.Add(addButton);
    }

    private void RedrawVariableDeclarations(VariableDeclareNode node, VisualElement container)
    {
        container.Clear();
        if (node.variables == null) node.variables = new List<VariableDeclaration>();

        for (int i = 0; i < node.variables.Count; i++)
        {
            int index = i;
            VariableDeclaration entry = node.variables[index];
            if (entry == null)
            {
                entry = new VariableDeclaration { key = VariableDeclareNode.NextDefaultKey(node.variables) };
                node.variables[index] = entry;
            }

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            TextField keyField = new TextField
            {
                value = entry.key,
                tooltip = "変数名。他の変数ノードの一覧に出ます"
            };
            keyField.style.flexGrow = 1;
            keyField.style.minWidth = 80;
            keyField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                entry.key = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(node);
                RefreshDynamicTitle();
            });
            row.Add(keyField);

            EnumField typeField = new EnumField(entry.type)
            {
                tooltip = "型"
            };
            typeField.style.width = 70;
            typeField.RegisterValueChangedCallback(evt =>
            {
                RecordNodeUndo();
                entry.type = (StoryVariableType)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(node);
                RedrawVariableDeclarations(node, container);
            });
            row.Add(typeField);

            row.Add(CreateDeclarationValueField(node, entry));

            Button deleteButton = new Button(() =>
            {
                BeginGraphEdit("変数を削除");
                if (index >= 0 && index < node.variables.Count)
                {
                    node.variables.RemoveAt(index);
                }
                UnityEditor.EditorUtility.SetDirty(node);
                RedrawVariableDeclarations(node, container);
                EndGraphEdit();
            })
            {
                text = "✕"
            };
            deleteButton.style.width = 25;
            row.Add(deleteButton);
            container.Add(row);
        }

        RefreshDynamicTitle();
    }

    private VisualElement CreateDeclarationValueField(VariableDeclareNode node, VariableDeclaration entry)
    {
        switch (entry.type)
        {
            case StoryVariableType.Bool:
                Toggle boolField = new Toggle
                {
                    value = entry.defaultBoolValue,
                    tooltip = "初期値"
                };
                boolField.style.width = 48;
                boolField.RegisterValueChangedCallback(evt =>
                {
                    RecordNodeUndo();
                    entry.defaultBoolValue = evt.newValue;
                    UnityEditor.EditorUtility.SetDirty(node);
                });
                return boolField;
            case StoryVariableType.Float:
                FloatField floatField = new FloatField
                {
                    value = entry.defaultFloatValue,
                    tooltip = "初期値"
                };
                floatField.style.width = 64;
                floatField.RegisterValueChangedCallback(evt =>
                {
                    RecordNodeUndo();
                    entry.defaultFloatValue = evt.newValue;
                    UnityEditor.EditorUtility.SetDirty(node);
                });
                return floatField;
            case StoryVariableType.String:
                TextField stringField = new TextField
                {
                    value = entry.defaultStringValue ?? "",
                    tooltip = "初期値"
                };
                stringField.style.width = 80;
                stringField.RegisterValueChangedCallback(evt =>
                {
                    RecordNodeUndo();
                    entry.defaultStringValue = evt.newValue;
                    UnityEditor.EditorUtility.SetDirty(node);
                });
                return stringField;
            default:
                IntegerField intField = new IntegerField
                {
                    value = entry.defaultIntValue,
                    tooltip = "初期値"
                };
                intField.style.width = 64;
                intField.RegisterValueChangedCallback(evt =>
                {
                    RecordNodeUndo();
                    entry.defaultIntValue = evt.newValue;
                    UnityEditor.EditorUtility.SetDirty(node);
                });
                return intField;
        }
    }

    /// <summary>
    /// 既存変数の選択欄。手入力はしない。クリックで Goto と同じ検索窓を開く。
    /// 一覧に無い、いまの変数名はそのまま選択状態で残す。
    /// </summary>
    private void AddVariableDropdown(string label, Func<string> getValue, Action<string> setValue, string tooltip)
    {
        extensionContainer.Add(CreateVariableDropdown(label, getValue, setValue, tooltip));
    }

    private VisualElement CreateVariableDropdown(string label, Func<string> getValue, Action<string> setValue, string tooltip)
    {
        const string unset = "(未選択)";

        VisualElement row = new VisualElement();
        row.AddToClassList("unity-base-field");
        row.AddToClassList("unity-popup-field");
        row.AddToClassList("unity-base-popup-field");

        Label fieldLabel = new Label(label)
        {
            tooltip = tooltip
        };
        fieldLabel.AddToClassList("unity-base-field__label");
        fieldLabel.AddToClassList("unity-popup-field__label");
        fieldLabel.AddToClassList("unity-base-popup-field__label");

        VisualElement input = new VisualElement
        {
            tooltip = "クリックして変数を検索します。"
        };
        input.AddToClassList("unity-base-field__input");
        input.AddToClassList("unity-popup-field__input");
        input.AddToClassList("unity-base-popup-field__input");

        Label valueLabel = new Label(unset)
        {
            pickingMode = PickingMode.Ignore
        };
        valueLabel.AddToClassList("unity-base-popup-field__text");
        valueLabel.style.flexGrow = 1;
        valueLabel.style.overflow = Overflow.Hidden;
        valueLabel.style.textOverflow = TextOverflow.Ellipsis;
        valueLabel.style.whiteSpace = WhiteSpace.NoWrap;

        Color fieldText = new Color(0.85f, 0.85f, 0.85f);
        fieldLabel.style.color = new StyleColor(fieldText);
        valueLabel.style.color = new StyleColor(fieldText);

        VisualElement arrow = new VisualElement { pickingMode = PickingMode.Ignore };
        arrow.AddToClassList("unity-base-popup-field__arrow");
        input.Add(valueLabel);
        input.Add(arrow);
        row.Add(fieldLabel);
        row.Add(input);

        void RefreshLabel()
        {
            string current = getValue() ?? "";
            string shown = string.IsNullOrEmpty(current) ? unset : current;
            valueLabel.text = shown;
            input.tooltip = shown;
            RefreshDynamicTitle();
        }

        void OpenPopup()
        {
            List<string> choices = StoryVariableCatalog.Collect(this);
            string current = getValue() ?? "";
            string desired = string.IsNullOrEmpty(current) ? unset : current;
            if (!choices.Contains(desired))
            {
                choices.Insert(0, desired);
            }

            if (choices.Count == 0)
            {
                choices.Add(desired);
            }

            int selectedIndex = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i] == desired)
                {
                    selectedIndex = i;
                    break;
                }
            }

            var snapshot = new List<string>(choices);
            GotoDestinationPopup.Show(input, snapshot, selectedIndex, index =>
            {
                if (index < 0 || index >= snapshot.Count) return;

                string picked = snapshot[index];
                if (string.IsNullOrEmpty(picked) || picked == unset) return;
                if (picked == (getValue() ?? ""))
                {
                    RefreshLabel();
                    return;
                }

                RecordNodeUndo();
                setValue(picked);
                UnityEditor.EditorUtility.SetDirty(data);
                RefreshLabel();
            }, "Dlair.VariablePicker.Query", "変数名で絞り込みます。");
        }

        input.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        input.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0) return;
            OpenPopup();
            evt.StopPropagation();
        });
        row.RegisterCallback<MouseEnterEvent>(_ => RefreshLabel());
        RegisterCallback<AttachToPanelEvent>(_ => RefreshLabel());
        RegisterCallback<DetachFromPanelEvent>(_ => GotoDestinationPopup.Close());

        RefreshLabel();
        return row;
    }

    private void DrawGotoSettings(GotoNode gotoNode)
    {
        List<LabelOption> options = new List<LabelOption>();

        VisualElement row = new VisualElement();
        row.AddToClassList("unity-base-field");
        row.AddToClassList("unity-popup-field");
        row.AddToClassList("unity-base-popup-field");

        Label fieldLabel = new Label("行先")
        {
            tooltip = "先に作成した Label（行先）ノードから選びます。"
        };
        fieldLabel.AddToClassList("unity-base-field__label");
        fieldLabel.AddToClassList("unity-popup-field__label");
        fieldLabel.AddToClassList("unity-base-popup-field__label");

        VisualElement input = new VisualElement
        {
            tooltip = "クリックして行先を検索します。"
        };
        input.AddToClassList("unity-base-field__input");
        input.AddToClassList("unity-popup-field__input");
        input.AddToClassList("unity-base-popup-field__input");

        Label valueLabel = new Label("(未選択)")
        {
            pickingMode = PickingMode.Ignore
        };
        valueLabel.AddToClassList("unity-base-popup-field__text");
        valueLabel.style.flexGrow = 1;
        valueLabel.style.overflow = Overflow.Hidden;
        valueLabel.style.textOverflow = TextOverflow.Ellipsis;
        valueLabel.style.whiteSpace = WhiteSpace.NoWrap;

        Color fieldText = new Color(0.85f, 0.85f, 0.85f);
        fieldLabel.style.color = new StyleColor(fieldText);
        valueLabel.style.color = new StyleColor(fieldText);

        VisualElement arrow = new VisualElement { pickingMode = PickingMode.Ignore };
        arrow.AddToClassList("unity-base-popup-field__arrow");
        input.Add(valueLabel);
        input.Add(arrow);
        row.Add(fieldLabel);
        row.Add(input);

        void RefreshButtonLabel()
        {
            RebuildGotoOptions(gotoNode, options);
            string selected = "(未選択)";
            if (string.IsNullOrEmpty(gotoNode.targetLabelGuid))
            {
                selected = options.Count > 0 ? options[0].display : selected;
            }
            else
            {
                for (int i = 1; i < options.Count; i++)
                {
                    if (options[i].labelGuid == gotoNode.targetLabelGuid)
                    {
                        selected = options[i].display;
                        break;
                    }
                }
            }

            valueLabel.text = selected;
            input.tooltip = selected;
            RefreshDynamicTitle();
        }

        void OpenPopup()
        {
            RebuildGotoOptions(gotoNode, options);
            var snapshot = new List<LabelOption>(options.Count);
            var labels = new string[options.Count];
            int selectedIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                snapshot.Add(options[i]);
                labels[i] = options[i].display;
                if (i > 0 && options[i].labelGuid == gotoNode.targetLabelGuid)
                    selectedIndex = i;
            }

            GotoDestinationPopup.Show(input, labels, selectedIndex, index =>
            {
                if (index < 0 || index >= snapshot.Count) return;

                RecordNodeUndo();
                LabelOption picked = snapshot[index];
                if (picked == null || string.IsNullOrEmpty(picked.labelGuid))
                {
                    gotoNode.targetLabelGuid = "";
                    gotoNode.targetGraph = null;
                }
                else
                {
                    gotoNode.targetLabelGuid = picked.labelGuid;
                    gotoNode.targetGraph = picked.graph;
                }

                UnityEditor.EditorUtility.SetDirty(gotoNode);
                RefreshButtonLabel();
            });
        }

        input.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        input.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (evt.button != 0) return;
            OpenPopup();
            evt.StopPropagation();
        });
        row.RegisterCallback<MouseEnterEvent>(_ => RefreshButtonLabel());
        RegisterCallback<AttachToPanelEvent>(_ => RefreshButtonLabel());
        RegisterCallback<DetachFromPanelEvent>(_ => GotoDestinationPopup.Close());

        extensionContainer.Add(row);

        Label hint = new Label("クリックすると検索できます。行先ノードを先に作るとリストに出ます。")
        {
            style =
            {
                color = new StyleColor(new Color(0.75f, 0.75f, 0.75f)),
                whiteSpace = WhiteSpace.Normal,
                marginTop = 4
            }
        };
        extensionContainer.Add(hint);

        RefreshButtonLabel();
    }

    private void RebuildGotoOptions(GotoNode gotoNode, List<LabelOption> options)
    {
        options.Clear();
        options.Add(new LabelOption { display = "(未選択)", labelGuid = "", graph = null });
        CollectLabelOptions(options);

        HashSet<string> usedDisplays = new HashSet<string>();
        for (int i = 0; i < options.Count; i++)
        {
            LabelOption option = options[i];
            string display = option.display;
            if (!usedDisplays.Add(display))
            {
                string graphName = option.graph != null ? option.graph.name : "graph";
                display = $"{option.display}  ({graphName})";
                if (!usedDisplays.Add(display))
                {
                    string suffix = option.labelGuid.Length > 4 ? option.labelGuid.Substring(0, 4) : option.labelGuid;
                    display = display + " " + suffix;
                    usedDisplays.Add(display);
                }

                option.display = display;
            }
        }

        bool found = string.IsNullOrEmpty(gotoNode.targetLabelGuid);
        for (int i = 1; i < options.Count; i++)
        {
            if (options[i].labelGuid == gotoNode.targetLabelGuid)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            string shortGuid = gotoNode.targetLabelGuid.Length > 8
                ? gotoNode.targetLabelGuid.Substring(0, 8)
                : gotoNode.targetLabelGuid;
            options.Insert(1, new LabelOption
            {
                display = "(欠損) " + shortGuid,
                labelGuid = gotoNode.targetLabelGuid,
                graph = gotoNode.targetGraph
            });
        }
    }

    private void CollectLabelOptions(List<LabelOption> options)
    {
        HashSet<string> seen = new HashSet<string>();
        GraphView view = GetFirstAncestorOfType<GraphView>();
        StoryGraph currentGraph = ResolveOwnerGraph();

        if (view != null)
        {
            foreach (GraphElement element in view.graphElements)
            {
                if (element is not StoryNodeUI nodeUI || nodeUI.data is not LabelNode label) continue;
                if (string.IsNullOrEmpty(label.guid) || !seen.Add(MakeLabelKey(currentGraph, label.guid))) continue;

                options.Add(new LabelOption
                {
                    display = label.GetDisplayName(),
                    labelGuid = label.guid,
                    graph = currentGraph
                });
            }
        }

        string[] assetGuids = UnityEditor.AssetDatabase.FindAssets("t:StoryGraph");
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            StoryGraph graphAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
            if (graphAsset == null || graphAsset.nodes == null) continue;

            bool isCurrent = graphAsset == currentGraph;
            for (int n = 0; n < graphAsset.nodes.Count; n++)
            {
                if (graphAsset.nodes[n] is not LabelNode label) continue;
                if (string.IsNullOrEmpty(label.guid) || !seen.Add(MakeLabelKey(graphAsset, label.guid))) continue;

                options.Add(new LabelOption
                {
                    display = isCurrent ? label.GetDisplayName() : $"{label.GetDisplayName()}  ({graphAsset.name})",
                    labelGuid = label.guid,
                    graph = graphAsset
                });
            }
        }
    }

    private static string MakeLabelKey(StoryGraph graph, string labelGuid)
    {
        string graphId = "none";
        if (graph != null)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(graph);
            graphId = !string.IsNullOrEmpty(path) ? path : graph.name;
        }

        return graphId + ":" + labelGuid;
    }

    private StoryGraph ResolveOwnerGraph()
    {
        StoryGraphView storyView = GetFirstAncestorOfType<StoryGraphView>();
        if (storyView != null && storyView.CurrentGraph != null)
        {
            return storyView.CurrentGraph;
        }

        if (data == null) return null;

        string path = UnityEditor.AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(path) && data is GotoNode gotoNode && gotoNode.targetGraph != null)
        {
            path = UnityEditor.AssetDatabase.GetAssetPath(gotoNode.targetGraph);
        }

        return string.IsNullOrEmpty(path)
            ? null
            : UnityEditor.AssetDatabase.LoadAssetAtPath<StoryGraph>(path);
    }

    private void AttachMediaThumbnail()
    {
        if (data is not PlayClipNode && data is not PlayImageNode) return;

        mediaThumbnail = StoryMediaPreview.CreateImage();
        titleContainer.Insert(0, mediaThumbnail);
        titleContainer.style.alignItems = Align.Center;
        style.minWidth = 200;
        RegisterCallback<DetachFromPanelEvent>(_ => StoryMediaPreview.Cancel(ref mediaPreviewRetry));
        RefreshMediaThumbnail();
    }

    private void RefreshMediaThumbnail()
    {
        if (mediaThumbnail == null) return;

        StoryMediaPreview.Cancel(ref mediaPreviewRetry);
        mediaPreviewRetry = StoryMediaPreview.Bind(mediaThumbnail, GetMediaAsset());
    }

    private UnityEngine.Object GetMediaAsset()
    {
        if (data is PlayClipNode playClipNode) return playClipNode.clip;
        if (data is PlayImageNode playImageNode) return playImageNode.image;
        return null;
    }

    private class LabelOption
    {
        public string display;
        public string labelGuid;
        public StoryGraph graph;
    }
}