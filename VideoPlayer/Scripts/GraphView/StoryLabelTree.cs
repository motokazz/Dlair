using System.Collections.Generic;
using UnityEngine;

public class StoryLabelTreeNode
{
    public string title;
    public StoryGraph graph;
    public LabelNode label;
    public List<StoryLabelTreeNode> children = new List<StoryLabelTreeNode>();

    public bool CanJump => label != null && graph != null;
    public bool HasChildren => children != null && children.Count > 0;
}

public static class StoryLabelTree
{
    public static StoryLabelTreeNode Build(StoryGraph root)
    {
        if (root == null) return null;
        return BuildGraph(root, new HashSet<StoryGraph>(), 0);
    }

    private static StoryLabelTreeNode BuildGraph(StoryGraph graph, HashSet<StoryGraph> visited, int depth)
    {
        if (graph == null || depth > 32 || !visited.Add(graph)) return null;

        StoryLabelTreeNode node = new StoryLabelTreeNode
        {
            title = string.IsNullOrEmpty(graph.name) ? "(unnamed)" : graph.name,
            graph = graph,
            label = graph.FindLabelByName(graph.name)
        };

        if (graph.nodes == null)
        {
            return node;
        }

        AppendExtraLabels(graph, node);

        // Goto 先を先に展開し、後続の SubGraph と同じ章が二重に出ないようにする。
        AppendDestinations(graph, node, visited, depth, gotoNodes: true);
        AppendDestinations(graph, node, visited, depth, gotoNodes: false);
        PruneEmpty(node);
        return node;
    }

    private static void AppendExtraLabels(StoryGraph graph, StoryLabelTreeNode node)
    {
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is not LabelNode label) continue;
            if (node.label != null && label == node.label) continue;

            node.children.Add(new StoryLabelTreeNode
            {
                title = label.GetDisplayName(),
                graph = graph,
                label = label
            });
        }
    }

    private static void AppendDestinations(
        StoryGraph graph,
        StoryLabelTreeNode node,
        HashSet<StoryGraph> visited,
        int depth,
        bool gotoNodes)
    {
        for (int i = 0; i < graph.nodes.Count; i++)
        {
            BaseNode current = graph.nodes[i];
            StoryGraph dest = null;

            if (gotoNodes)
            {
                if (current is not GotoNode gotoNode) continue;
                ResolveGoto(graph, gotoNode, out dest, out _);
            }
            else
            {
                if (current is not SubGraphNode subGraphNode) continue;
                dest = subGraphNode.subGraph;
            }

            if (dest == null || dest == graph || visited.Contains(dest)) continue;

            StoryLabelTreeNode child = BuildGraph(dest, visited, depth + 1);
            if (child != null)
            {
                node.children.Add(child);
            }
        }
    }

    private static void PruneEmpty(StoryLabelTreeNode node)
    {
        if (node?.children == null) return;

        for (int i = node.children.Count - 1; i >= 0; i--)
        {
            PruneEmpty(node.children[i]);
            StoryLabelTreeNode child = node.children[i];
            if (!child.CanJump && !child.HasChildren)
            {
                node.children.RemoveAt(i);
            }
        }
    }

    private static void ResolveGoto(StoryGraph currentGraph, GotoNode gotoNode, out StoryGraph owner, out LabelNode label)
    {
        owner = null;
        label = null;
        if (gotoNode == null || string.IsNullOrEmpty(gotoNode.targetLabelGuid)) return;

        if (gotoNode.targetGraph != null)
        {
            gotoNode.targetGraph.TryFindLabel(gotoNode.targetLabelGuid, out owner, out label);
        }

        if (label == null && currentGraph != null)
        {
            currentGraph.TryFindLabel(gotoNode.targetLabelGuid, out owner, out label);
        }
    }
}
