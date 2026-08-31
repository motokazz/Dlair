using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Video;
using System.Collections.Generic;

public class StoryNodeUI : Node
{
    public string guid;
    public BaseNode data;

    public StoryNodeUI(BaseNode nodeData)
    {
        this.guid = nodeData.guid;
        this.data = nodeData;

        // タイトルバーの名前を設定
        this.title = nodeData.GetType().Name.Replace("Node", "");
        this.SetPosition(new Rect(nodeData.position, Vector2.zero));

        Port inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Enter";
        inputContainer.Add(inputPort);

        if (nodeData is ExitNode || nodeData is GotoNode)
        {
            // Exit / Goto は入力のみ。行先はドロップダウンで指定する
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
        else
        {
            Port outputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            outputPort.portName = "Next";
            outputContainer.Add(outputPort);
        }

        DrawNodeSettings();
        RefreshExpandedState();
        RefreshPorts();
        ApplyNodeStyle();
        RefreshDynamicTitle();
    }

    private void RefreshDynamicTitle()
    {
        if (data is VariableOperationNode varOpNode)
        {
            title = varOpNode.GetDisplayTitle();
        }
        else if (data is ConditionNode conditionNode)
        {
            title = conditionNode.GetDisplayTitle();
        }
        else if (data is RandomBranchNode randomBranchNode)
        {
            title = randomBranchNode.GetDisplayTitle();
        }
        else if (data is LabelNode labelNode)
        {
            title = labelNode.GetDisplayTitle();
        }
        else if (data is GotoNode gotoNode)
        {
            title = gotoNode.GetDisplayTitle();
        }
    }

    private void ApplyNodeStyle()
    {
        Color titleColor;
        if (data is StartNode)
        {
            titleColor = new Color(0.20f, 0.55f, 0.25f);
        }
        else if (data is ConditionNode || data is VariableOperationNode)
        {
            titleColor = new Color(0.48f, 0.22f, 0.62f);
        }
        else if (data is RandomBranchNode)
        {
            titleColor = new Color(0.70f, 0.40f, 0.10f);
            style.minWidth = 240;
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
        else if (data is GotoNode)
        {
            titleColor = new Color(0.58f, 0.42f, 0.12f);
            style.minWidth = 240;
        }
        else if (data is ExitNode)
        {
            titleColor = new Color(0.55f, 0.22f, 0.22f);
            titleButtonContainer.style.display = DisplayStyle.None;
            extensionContainer.style.display = DisplayStyle.None;
        }
        else
        {
            return;
        }

        titleContainer.style.backgroundColor = new StyleColor(titleColor);

        if (data is ConditionNode || data is VariableOperationNode || data is RandomBranchNode || data is LabelNode || data is GotoNode)
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
        // もしデータが PlayClipNode だったら…
        if (data is PlayClipNode playClipNode)
        {
            ObjectField clipField = new ObjectField("Video Clip")
            {
                objectType = typeof(VideoClip),
                value = playClipNode.clip
            };

            clipField.RegisterValueChangedCallback(evt =>
            {
                playClipNode.clip = evt.newValue as VideoClip;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(clipField);

            ObjectField audioField = new ObjectField("Audio Clip")
            {
                objectType = typeof(AudioClip),
                value = playClipNode.audioClip
            };
            audioField.RegisterValueChangedCallback(evt =>
            {
                playClipNode.audioClip = evt.newValue as AudioClip;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(audioField);

            FloatField fadeField = new FloatField("クロスフェード(秒)")
            {
                value = playClipNode.crossFadeDuration
            };
            fadeField.RegisterValueChangedCallback(evt =>
            {
                playClipNode.crossFadeDuration = Mathf.Max(0f, evt.newValue);
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(fadeField);

            Toggle loopToggle = new Toggle("Loop")
            {
                value = playClipNode.isLooping
            };
            loopToggle.RegisterValueChangedCallback(evt =>
            {
                playClipNode.isLooping = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(loopToggle);

            FloatField outputTimeField = new FloatField("出力タイミング(秒)")
            {
                value = playClipNode.portOutputTime,
                tooltip = "0: 開始と同時に Next へ信号 / 負の値: 動画の終端 / 動画の長さ以上: 終端"
            };
            outputTimeField.RegisterValueChangedCallback(evt =>
            {
                playClipNode.portOutputTime = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(playClipNode);
            });
            extensionContainer.Add(outputTimeField);
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
                playImageNode.image = evt.newValue as Texture2D;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(imageField);

            FloatField durationField = new FloatField("表示秒数")
            {
                value = playImageNode.duration
            };
            durationField.RegisterValueChangedCallback(evt =>
            {
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
                playImageNode.audioClip = evt.newValue as AudioClip;
                UnityEditor.EditorUtility.SetDirty(playImageNode);
            });
            extensionContainer.Add(audioField);

            FloatField fadeField = new FloatField("クロスフェード(秒)")
            {
                value = playImageNode.crossFadeDuration
            };
            fadeField.RegisterValueChangedCallback(evt =>
            {
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

                subGraphNode.subGraph = assigned;
                UnityEditor.EditorUtility.SetDirty(subGraphNode);
                title = subGraphNode.subGraph != null ? subGraphNode.subGraph.name : "SubGraph";
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

            if (subGraphNode.subGraph != null)
            {
                title = subGraphNode.subGraph.name;
            }

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
        // ==========================================
        // ★追加：もしデータが ConditionNode だったら…
        // ==========================================
        else if (data is ConditionNode conditionNode)
        {
            TextField varNameField = new TextField("変数名")
            {
                value = conditionNode.variableName
            };
            varNameField.RegisterValueChangedCallback(evt =>
            {
                conditionNode.variableName = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(conditionNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(varNameField);

            EnumField opField = new EnumField("条件", conditionNode.comparison);
            opField.RegisterValueChangedCallback(evt =>
            {
                conditionNode.comparison = (ConditionOperator)evt.newValue;
                UnityEditor.EditorUtility.SetDirty(conditionNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(opField);

            IntegerField valField = new IntegerField("比較する値")
            {
                value = conditionNode.compareValue
            };
            valField.RegisterValueChangedCallback(evt =>
            {
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
            TextField varNameField = new TextField("対象変数")
            {
                value = varOpNode.variableName
            };
            varNameField.RegisterValueChangedCallback(evt =>
            {
                varOpNode.variableName = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });
            extensionContainer.Add(varNameField);

            EnumField opField = new EnumField("演算", varOpNode.operation);
            opField.RegisterValueChangedCallback(evt =>
            {
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
                varOpNode.operandValue = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });

            TextField operandVarField = new TextField("参照変数")
            {
                value = varOpNode.operandVariableName
            };
            operandVarField.style.display = varOpNode.useVariableOperand ? DisplayStyle.Flex : DisplayStyle.None;
            operandVarField.RegisterValueChangedCallback(evt =>
            {
                varOpNode.operandVariableName = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(varOpNode);
                RefreshDynamicTitle();
            });

            useVarToggle.RegisterValueChangedCallback(evt =>
            {
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
        // ==========================================
        // ★追加：もしデータが ChoiceNode だったら…
        // ==========================================
        else if (data is ChoiceNode choiceNode)
        {
            ObjectField prefabField = new ObjectField("UI Prefab")
            {
                objectType = typeof(GameObject),
                value = choiceNode.branchUIPrefab
            };
            prefabField.RegisterValueChangedCallback(evt =>
            {
                choiceNode.branchUIPrefab = evt.newValue as GameObject;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
            });
            extensionContainer.Add(prefabField);

            VisualElement choiceListContainer = new VisualElement();

            Button autoDetectBtn = new Button(() =>
            {
                if (choiceNode.branchUIPrefab != null)
                {
                    var buttons = choiceNode.branchUIPrefab.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                    if (buttons.Length > 0)
                    {
                        choiceNode.choices.Clear();
                        foreach (var b in buttons)
                        {
                            if (!choiceNode.choices.Contains(b.gameObject.name))
                            {
                                choiceNode.choices.Add(b.gameObject.name);
                            }
                        }
                        UnityEditor.EditorUtility.SetDirty(choiceNode);
                        BuildChoicePorts(choiceNode);
                        RefreshExpandedState();
                        RefreshPorts();
                        RedrawChoiceList(choiceNode, choiceListContainer);
                    }
                }
            });
            autoDetectBtn.text = "🔍 プレハブからボタンを自動取得";
            extensionContainer.Add(autoDetectBtn);

            extensionContainer.Add(choiceListContainer);
            RedrawChoiceList(choiceNode, choiceListContainer);

            Button addChoiceBtn = new Button(() =>
            {
                choiceNode.choices.Add($"Choice {choiceNode.choices.Count + 1}");
                UnityEditor.EditorUtility.SetDirty(choiceNode);
                BuildChoicePorts(choiceNode);
                RefreshExpandedState();
                RefreshPorts();
                RedrawChoiceList(choiceNode, choiceListContainer);
            });
            addChoiceBtn.text = "＋ 選択肢(ポート)を追加";
            extensionContainer.Add(addChoiceBtn);
        }
        else if (data is RandomBranchNode randomBranchNode)
        {
            Label hint = new Label("比率は相対ウェイトです（合計が100でなくても可）")
            {
                style =
                {
                    color = new StyleColor(new Color(0.75f, 0.75f, 0.75f)),
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 4
                }
            };
            extensionContainer.Add(hint);

            VisualElement branchListContainer = new VisualElement();
            extensionContainer.Add(branchListContainer);
            RedrawRandomBranchList(randomBranchNode, branchListContainer);

            Button addBranchBtn = new Button(() =>
            {
                EnsureRandomBranches(randomBranchNode);
                randomBranchNode.branches.Add(new RandomBranchEntry
                {
                    name = RandomBranchNode.NextDefaultName(randomBranchNode.branches),
                    weight = 1f
                });
                UnityEditor.EditorUtility.SetDirty(randomBranchNode);
                BuildRandomBranchPorts(randomBranchNode);
                RefreshExpandedState();
                RefreshPorts();
                RedrawRandomBranchList(randomBranchNode, branchListContainer);
            })
            {
                text = "＋ 分岐を追加"
            };
            extensionContainer.Add(addBranchBtn);
        }
    }

    private void BuildChoicePorts(ChoiceNode choiceNode)
    {
        outputContainer.Clear();
        if (choiceNode.choices == null || choiceNode.choices.Count == 0)
        {
            choiceNode.choices = new System.Collections.Generic.List<string> { "Choice 1" };
        }

        foreach (var choice in choiceNode.choices)
        {
            Port choicePort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            choicePort.portName = choice;
            outputContainer.Add(choicePort);
        }
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

            TextField tf = new TextField($"選択肢 {index + 1}")
            {
                value = choiceNode.choices[index]
            };
            tf.style.flexGrow = 1;
            tf.RegisterValueChangedCallback(evt =>
            {
                choiceNode.choices[index] = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(choiceNode);
                BuildChoicePorts(choiceNode);
                RefreshExpandedState();
                RefreshPorts();
            });
            row.Add(tf);

            if (choiceNode.choices.Count > 1)
            {
                Button delBtn = new Button(() =>
                {
                    choiceNode.choices.RemoveAt(index);
                    UnityEditor.EditorUtility.SetDirty(choiceNode);
                    BuildChoicePorts(choiceNode);
                    RefreshExpandedState();
                    RefreshPorts();
                    RedrawChoiceList(choiceNode, container);
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

    private void BuildRandomBranchPorts(RandomBranchNode node)
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
                entry.name = evt.newValue;
                UnityEditor.EditorUtility.SetDirty(node);
                BuildRandomBranchPorts(node);
                RefreshExpandedState();
                RefreshPorts();
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
                    node.branches.RemoveAt(index);
                    UnityEditor.EditorUtility.SetDirty(node);
                    BuildRandomBranchPorts(node);
                    RefreshExpandedState();
                    RefreshPorts();
                    RedrawRandomBranchList(node, container);
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

    private void DrawGotoSettings(GotoNode gotoNode)
    {
        List<LabelOption> options = new List<LabelOption>();
        DropdownField dropdown = new DropdownField("行先")
        {
            tooltip = "先に作成した Label（行先）ノードから選びます。"
        };

        void RefreshGotoDropdown()
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

            List<string> choices = new List<string>(options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                choices.Add(options[i].display);
            }

            string selected = "(未選択)";
            for (int i = 1; i < options.Count; i++)
            {
                if (options[i].labelGuid == gotoNode.targetLabelGuid)
                {
                    selected = options[i].display;
                    break;
                }
            }

            if (selected == "(未選択)" && !string.IsNullOrEmpty(gotoNode.targetLabelGuid))
            {
                string shortGuid = gotoNode.targetLabelGuid.Length > 8
                    ? gotoNode.targetLabelGuid.Substring(0, 8)
                    : gotoNode.targetLabelGuid;
                string missing = "(欠損) " + shortGuid;
                options.Insert(1, new LabelOption
                {
                    display = missing,
                    labelGuid = gotoNode.targetLabelGuid,
                    graph = gotoNode.targetGraph
                });
                choices.Insert(1, missing);
                selected = missing;
            }

            dropdown.choices = choices;
            dropdown.SetValueWithoutNotify(selected);
            RefreshDynamicTitle();
        }

        dropdown.RegisterValueChangedCallback(evt =>
        {
            LabelOption picked = options.Find(o => o.display == evt.newValue);
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
            RefreshDynamicTitle();
        });

        dropdown.RegisterCallback<MouseEnterEvent>(_ => RefreshGotoDropdown());
        dropdown.RegisterCallback<FocusInEvent>(_ => RefreshGotoDropdown());
        RegisterCallback<AttachToPanelEvent>(_ => RefreshGotoDropdown());

        extensionContainer.Add(dropdown);

        Label hint = new Label("行先ノードを先に作ると、このリストに表示されます。")
        {
            style =
            {
                color = new StyleColor(new Color(0.75f, 0.75f, 0.75f)),
                whiteSpace = WhiteSpace.Normal,
                marginTop = 4
            }
        };
        extensionContainer.Add(hint);

        RefreshGotoDropdown();
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
                if (string.IsNullOrEmpty(label.guid) || !seen.Add(label.guid)) continue;

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
                if (string.IsNullOrEmpty(label.guid) || !seen.Add(label.guid)) continue;

                options.Add(new LabelOption
                {
                    display = isCurrent ? label.GetDisplayName() : $"{label.GetDisplayName()}  ({graphAsset.name})",
                    labelGuid = label.guid,
                    graph = graphAsset
                });
            }
        }
    }

    private StoryGraph ResolveOwnerGraph()
    {
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

    private class LabelOption
    {
        public string display;
        public string labelGuid;
        public StoryGraph graph;
    }
}