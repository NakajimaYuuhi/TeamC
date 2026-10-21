using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NodeData", menuName = "Scriptable Objects/NodeData")]
public class Node_Edge_Data : ScriptableObject
{
    [Serializable]
    public struct Node
    {
        // Inspectorで編集不可（表示のみ）にしたい場合は属性をつけるかprivateにする
        [SerializeField] private int nodeID;
        public Vector3 position;
        public List<int> adjacents;

        public int NodeID => nodeID;

        public Node(int id, Vector3 pos, List<int> adj)
        {
            nodeID = id;
            position = pos;
            adjacents = adj;
        }

        // ID更新用の内部メソッド
        public void SetID(int id) => nodeID = id;
        public void SetAdjacents(List<int> list) => adjacents = list;
    }

    // エッジの構造体
    [Serializable]
    public struct Edge
    {
        public int fromNodeID;
        public int toNodeID;
        public float distance;     // 距離や重み（A*などで使うなら）
        public bool isTwoWay;  // 双方向にするかどうか
        public float width;     // エッジの幅
    }


    // ノードのリスト
    [SerializeField]
    private List<Node> nodes = new List<Node>();


    // エッジのリスト
    [SerializeField]
    private List<Edge> edges = new List<Edge>();



    public IReadOnlyList<Node> Nodes => nodes;
    public IReadOnlyList<Edge> Edges => edges;


    // Inspectorで値が変わったり要素が追加・削除されたときに自動実行される
    private void OnValidate()
    {
        if (nodes == null) return;

        // ノードのIDの連番保証
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            // インデックス順に 0, 1, 2... と連番を強制固定
            if (node.NodeID != i)
            {
                node.SetID(i);
                nodes[i] = node; // structなのでリストに戻す
            }
        }

        // 2. 各ノードの一時的な隣接リストを用意
        var adjMap = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < nodes.Count; i++)
        {
            adjMap[i] = new HashSet<int>();
        }

        // 3. エッジ情報から隣接ノードを収集（重複防止のためHashSetを使用）
        foreach (var edge in edges)
        {
            // 有効なID範囲かチェック
            if (IsValidID(edge.fromNodeID) && IsValidID(edge.toNodeID))
            {
                adjMap[edge.fromNodeID].Add(edge.toNodeID);

                // 双方向なら逆向きも追加
                if (edge.isTwoWay)
                {
                    adjMap[edge.toNodeID].Add(edge.fromNodeID);
                }
            }
        }

        // 4. 各ノードの adjacents に反映
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            node.SetAdjacents(new List<int>(adjMap[i]));
            nodes[i] = node;
        }

        // 5. エッジの距離を更新（必要に応じて）
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (IsValidID(edge.fromNodeID) && IsValidID(edge.toNodeID))
            {
                var fromNode = nodes[edge.fromNodeID];
                var toNode = nodes[edge.toNodeID];
                edge.distance = Vector3.Distance(fromNode.position, toNode.position);
                edges[i] = edge;
            }
        }
    }

    private bool IsValidID(int id) => id >= 0 && id < nodes.Count;
}

