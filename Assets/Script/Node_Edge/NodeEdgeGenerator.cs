using UnityEngine;

public class NodeEdgeGenerator : MonoBehaviour
{
    [SerializeField]
    private Node_Edge_Data nodeData;

    [SerializeField]
    GameObject nodePrefab;

    [SerializeField]
    GameObject edgePrefab;

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
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
