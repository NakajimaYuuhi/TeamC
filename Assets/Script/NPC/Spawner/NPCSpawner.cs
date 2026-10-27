using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("スポーンデータ(SO)")]
    [SerializeField] private SpawnData _spawnData;

    [Header("スポーン位置(複数)")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("NPC初期設定")]
    [SerializeField] private NPC_Controller.NPCStateType _initialNPCState = NPC_Controller.NPCStateType.Browsing_BasedOnNode;
    // 最初に向かうノード
    [SerializeField] private int _initialSpawnIndex;
    [SerializeField] private NodeEdgeGenerator _nodeedgeGenerator;

    [Header("スポーン位置のばらつき")]
    // エッジの幅のどこまでを使うか(1でエッジの端まで)
    [SerializeField, Range(0f, 1f)] private float _widthUsage = 0.9f;
    // エッジが見つからないときの幅(スポーン地点の右方向にばらつかせる)
    [SerializeField] private float _fallbackWidth = 2.0f;
    // 進む方向の前後にもばらつかせる(同時に出たNPCが一列に並ばないように)
    [SerializeField, Min(0f)] private float _alongJitter = 0.5f;
    // 他のNPCと重ならないかチェックする半径(0ならチェックしない)
    [SerializeField, Min(0f)] private float _overlapCheckRadius = 0.4f;
    [SerializeField] private LayerMask _overlapCheckMask;
    // 重なったときに場所を選び直す回数(全部だめならそのスポーンは飛ばす)
    [SerializeField, Min(1)] private int _maxTries = 5;

    private float _spawnTimer = 0f;
    private bool _isFirstSpawn = true;
    private bool _isFinished = false;

    private void Start()
    {
        if (_spawnData == null)
        {
            Debug.LogWarning("[NPCSpawner] SpawnData が設定されていません。");
            return;
        }
        if (_nodeedgeGenerator == null) _nodeedgeGenerator = FindAnyObjectByType<NodeEdgeGenerator>();

        // 初回インターバルをSOから取得してセット
        _spawnTimer = _spawnData.GetNextInterval();
    }

    private void Update()
    {
        // 終了済み、またはデータ未割り当てなら処理しない
        if (_isFinished || _spawnData == null) return;

        // 次のスポーンが可能か確認(Onceなら初回完了後に停止)
        if (!_spawnData.CanSpawn(_isFirstSpawn))
        {
            _isFinished = true;
            return;
        }

        _spawnTimer -= Time.deltaTime;

        if (_spawnTimer <= 0f)
        {
            // 全スポーンポイントに生成
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                {
                    SpawnNPC(_spawnPoints[i]);
                }
            }

            _isFirstSpawn = false;

            // 一括生成(Once)の場合はここで終了
            if (!_spawnData.CanSpawn(_isFirstSpawn))
            {
                _isFinished = true;
                return;
            }

            // 次の待機時間をSOから再取得(ランダムなら毎回再抽選される)
            _spawnTimer = _spawnData.GetNextInterval();
        }
    }

    // NPCをスポーンするメソッド
    private void SpawnNPC(Transform spawnPoint)
    {
        GameObject prefab = _spawnData.SpawnPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[NPCSpawner] SpawnData の SpawnPrefab が設定されていません。");
            return;
        }

        bool hasNode = _nodeedgeGenerator != null;
        Node_Edge_Data.Node targetNode = hasNode ? _nodeedgeGenerator.GetNode(_initialSpawnIndex) : default;

        // ばらつかせる向き(エッジに沿った前・右)と幅を決める
        GetSpawnFrame(spawnPoint, hasNode, targetNode, out Vector3 forward, out Vector3 right, out float halfWidth);

        // 重ならない場所を探す
        Vector3 position = spawnPoint.position;
        bool found = false;
        for (int i = 0; i < _maxTries; i++)
        {
            float lat = Random.Range(-1f, 1f) * halfWidth * _widthUsage;
            float along = Random.Range(-1f, 1f) * _alongJitter;
            Vector3 candidate = spawnPoint.position + right * lat + forward * along;

            if (_overlapCheckRadius <= 0f || _overlapCheckMask.value == 0 ||
                !Physics.CheckSphere(candidate + Vector3.up * _overlapCheckRadius, _overlapCheckRadius, _overlapCheckMask))
            {
                position = candidate;
                found = true;
                break;
            }
        }
        if (!found) return; // 混みすぎ:今回は出さない

        // エッジの方向を向いて生成
        Quaternion rotation = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : spawnPoint.rotation;
        GameObject npcInstance = Instantiate(prefab, position, rotation);

        NPC_Controller npcController = npcInstance.GetComponent<NPC_Controller>();
        if (npcController != null)
        {
            npcController.SetState(_initialNPCState);

            if (hasNode)
            {
                // 先にノードデータを渡してから、向かうノードを設定する
                // (SetNode の中で、今の位置から中心からのズレを測る)
                npcController.SetNodeGenerator(_nodeedgeGenerator);
                npcController.SetNode(targetNode);
            }
        }
    }

    // スポーン地点がどのエッジ上にあるかを調べて、その向きと幅を返す
    // (NPC_Controller.SetNode と同じく、向かうノードの隣のうち一番近いエッジを使う)
    private void GetSpawnFrame(Transform spawnPoint, bool hasNode, Node_Edge_Data.Node targetNode,
                               out Vector3 forward, out Vector3 right, out float halfWidth)
    {
        forward = Flat(spawnPoint.forward).normalized;
        right = Flat(spawnPoint.right).normalized;
        halfWidth = _fallbackWidth * 0.5f;

        if (!hasNode || targetNode.adjacents == null) return;

        Vector3 p = Flat(spawnPoint.position);
        Vector3 b = Flat(targetNode.position);
        float bestDist = float.PositiveInfinity;
        int bestFrom = -1;
        foreach (int adjID in targetNode.adjacents)
        {
            Vector3 a = Flat(_nodeedgeGenerator.GetNode(adjID).position);
            float d = DistanceToSegment(p, a, b);
            if (d < bestDist)
            {
                bestDist = d;
                bestFrom = adjID;
            }
        }
        if (bestFrom == -1) return;

        Vector3 axis = b - Flat(_nodeedgeGenerator.GetNode(bestFrom).position);
        if (axis.sqrMagnitude < 1e-6f) return;
        forward = axis.normalized;
        right = Vector3.Cross(Vector3.up, forward);

        int edgeIndex = _nodeedgeGenerator.FindEdgeIndex(bestFrom, targetNode.NodeID);
        if (edgeIndex != -1) halfWidth = _nodeedgeGenerator.GetEdgeWidth(edgeIndex) * 0.5f;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }

    // スポーンの範囲を表示(エッジが見つからないときはスポーン地点の右方向で表示)
    private void OnDrawGizmosSelected()
    {
        if (_spawnPoints == null) return;
        bool hasNode = _nodeedgeGenerator != null && Application.isPlaying;
        Node_Edge_Data.Node targetNode = hasNode ? _nodeedgeGenerator.GetNode(_initialSpawnIndex) : default;

        Gizmos.color = Color.magenta;
        foreach (var sp in _spawnPoints)
        {
            if (sp == null) continue;
            GetSpawnFrame(sp, hasNode, targetNode, out Vector3 f, out Vector3 r, out float hw);
            Vector3 w = r * hw * _widthUsage;
            Vector3 l = f * _alongJitter;
            Vector3 c = sp.position + Vector3.up * 0.05f;
            Gizmos.DrawLine(c - w - l, c + w - l);
            Gizmos.DrawLine(c - w + l, c + w + l);
            Gizmos.DrawLine(c - w - l, c - w + l);
            Gizmos.DrawLine(c + w - l, c + w + l);
        }
    }
}
