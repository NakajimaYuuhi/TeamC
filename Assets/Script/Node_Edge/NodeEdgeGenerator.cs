using System.Collections.Generic;
using UnityEngine;

public class NodeEdgeGenerator : MonoBehaviour
{
    [SerializeField]
    private Node_Edge_Data nodeData;

    [SerializeField]
    GameObject nodePrefab;

    [SerializeField]
    GameObject edgePrefab;

    // ノードをセットするためのパラメータ
    // セット先のオブジェクト
    [SerializeField]
    private NPC_Controller _npcController;

    // ノードのIDを指定するためのパラメータ
    [SerializeField]
    private int _nodeID;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // ノードの生成
        for (int i = 0; i < nodeData.Nodes.Count; i++)
        {
            var node = nodeData.Nodes[i];
            Instantiate(nodePrefab, node.position, Quaternion.identity);
        }

        // エッジの生成
        for (int i = 0; i < nodeData.Edges.Count; i++)
        {
            var edge = nodeData.Edges[i];
            var fromNode = nodeData.Nodes[edge.fromNodeID];
            var toNode = nodeData.Nodes[edge.toNodeID];

            if (fromNode.position != null && toNode.position != null)
            {
            
                // 中点の計算
                Vector3 midPoint = (fromNode.position + toNode.position) / 2;

                // エッジの生成
                GameObject edgeObj = Instantiate(edgePrefab, midPoint, Quaternion.identity);
                
                // エッジの向きを設定する場合は、ここで回転を計算して設定する
                Vector3 direction = toNode.position - fromNode.position;
                edgeObj.transform.rotation = Quaternion.LookRotation(direction);
                
                // エッジのスケールを距離に応じて調整する場合は、ここでスケールを設定する
                float distance = Vector3.Distance(fromNode.position, toNode.position);
                edgeObj.transform.localScale = new Vector3(edgeObj.transform.localScale.x, edgeObj.transform.localScale.y, distance);

                // エッジの幅を設定する場合は、ここでスケールを調整する
                edgeObj.transform.localScale = new Vector3(edge.width, edgeObj.transform.localScale.y, edgeObj.transform.localScale.z);


            }
        }


        // ノードの情報をNPC_Controllerにセットする
        if (_npcController != null)
        {
            Node_Edge_Data.Node nodeInfo = GetNode(_nodeID);
            _npcController.SetNode(nodeInfo);
        }
        else
        {
            Debug.LogError("NPC_Controller is not assigned.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // ノードの情報を取得する
    public Node_Edge_Data.Node GetNode(int nodeID)
    {
        if (nodeID < 0 || nodeID >= nodeData.Nodes.Count)
        {
            Debug.LogError($"Invalid nodeID: {nodeID}");
            return default;
        }
        return nodeData.Nodes[nodeID];
    }

    // エッジの情報を取得する
    public Node_Edge_Data.Edge GetEdge(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= nodeData.Edges.Count)
        {
            Debug.LogError($"Invalid edgeIndex: {edgeIndex}");
            return default;
        }
        return nodeData.Edges[edgeIndex];
    }

    public float GetEdgeWidth(int edgeIndex)
    {
        if (edgeIndex < 0 || edgeIndex >= nodeData.Edges.Count)
        {
            Debug.LogError($"Invalid edgeIndex: {edgeIndex}");
            return 0f;
        }
        return nodeData.Edges[edgeIndex].width;
    }

    // from → to のエッジの番号を返す(なければ -1)。双方向のエッジは逆向きでも見つける
    public int FindEdgeIndex(int fromNodeID, int toNodeID)
    {
        for (int i = 0; i < nodeData.Edges.Count; i++)
        {
            var e = nodeData.Edges[i];
            if (e.fromNodeID == fromNodeID && e.toNodeID == toNodeID) return i;
            if (e.isTwoWay && e.fromNodeID == toNodeID && e.toNodeID == fromNodeID) return i;
        }
        return -1;
    }

    // toNodeID に入ってくるエッジのうち、pos が一番近いエッジの始点ノードIDを返す(なければ -1)
    // 一方通行のエッジにも対応(隣接リストは「出ていく」方向しか持っていないため)
    public int FindNearestIncomingNode(int toNodeID, Vector3 pos)
    {
        Vector3 p = new Vector3(pos.x, 0f, pos.z);
        Vector3 b = nodeData.Nodes[toNodeID].position; b.y = 0f;
        int best = -1;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < nodeData.Edges.Count; i++)
        {
            var e = nodeData.Edges[i];
            int from;
            if (e.toNodeID == toNodeID) from = e.fromNodeID;
            else if (e.isTwoWay && e.fromNodeID == toNodeID) from = e.toNodeID;
            else continue;

            Vector3 a = nodeData.Nodes[from].position; a.y = 0f;
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            float d = Vector3.Distance(p, a + ab * t);
            if (d < bestDist) { bestDist = d; best = from; }
        }
        return best;
    }
}
