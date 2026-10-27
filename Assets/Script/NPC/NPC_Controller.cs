using UnityEngine;

// NPCの「頭」。ステートマシンで「どこへ行きたいか」を決めて、
// NPC_FlowLocomotion(足)に方向を渡すだけ。実際の移動・回避・向きは足がやる。
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NPC_FlowLocomotion))]
public class NPC_Controller : MonoBehaviour
{
    private Rigidbody rb;
    private NPC_FlowLocomotion _locomotion;

    private bool physicsLodSuspended;
    private bool controllerEnabledBeforeLod;
    private bool locomotionEnabledBeforeLod;
    private bool wasKinematicBeforeLod;
    private bool detectedCollisionsBeforeLod;
    private bool wasSleepingBeforeLod;
    private Vector3 velocityBeforeLod;
    private Vector3 angularVelocityBeforeLod;

    public bool IsPhysicsLodSuspended => physicsLodSuspended;
    internal NpcPhysicsLodManager PhysicsLodManager { get; set; }

    // ===== インスペクターでの設定項目 =====
    [Header("立ち止まっている状態")]
    // 初期パラメータ
    private Vector3 _defaultPosition;       // 初期位置
    private Quaternion _defaultRotation;    // 初期向き

    // 初期位置に戻る際の速さ
    [SerializeField] private float _returnSpeed = 3.0f;
    // この距離より近づいたら減速し始める
    [SerializeField] private float _returnSlowRadius = 1.0f;
    // 初期位置とみなす範囲
    [SerializeField] private float _stopDeadZone = 0.25f;

    [Header("徘徊状態の設定")]
    [SerializeField]
    private Vector3[] _targetPositions = new Vector3[]
    {
        new Vector3(5, 0, 0),
        new Vector3(-5, 0, 0),
        new Vector3(0, 0, 5)
    };
    [SerializeField] private int _currentTargetIndex = 0;
    [SerializeField] private float _browseSpeed = 6.0f;
    // 徘徊:目標点までこの距離で到着 / ノード移動:エッジの終点のこの手前で到着
    [SerializeField] private float _arrivalDistance = 0.5f;

    [Header("ノードベースでの徘徊状態の設定")]
    [SerializeField] private Node_Edge_Data.Node _node;     // 今向かっているノード
    // 過去のノードのID
    private int _previousNodeID = -1;
    // ノードのデータ(未設定なら自動で探す)
    [SerializeField] private NodeEdgeGenerator _nodeGenerator;
    // エッジが見つからないときの幅
    [SerializeField] private float _defaultEdgeWidth = 2.0f;
    // 中心からのズレ(割合)に戻ろうとする強さ
    [SerializeField] private float _lateralGain = 0.5f;
    // 横に戻る補正の上限(1で最大45°くらい斜めに進む)
    [SerializeField] private float _maxLateralCorrection = 0.5f;
    // 押されて横にずれたとき、そのズレを新しい自分の位置として受け入れる速さ(0なら必ず元に戻る)
    [SerializeField, Min(0f)] private float _offsetAdaptRate = 0.3f;
    // エッジの端からこの割合だけ内側に収める(壁ぎわに張り付かないように)
    [SerializeField, Range(0f, 1f)] private float _maxOffsetRate = 0.9f;

    // 今歩いているエッジ(位置で持つ。始点がノードでないときもある)
    private bool _hasEdge;
    private Vector3 _edgeFrom;
    private Vector3 _edgeTo;
    private float _edgeHalfWidth;
    // 中心からのズレ(-1:左端 ～ 1:右端)。エッジを乗り換えても引き継ぐ
    private float _offsetRate;

    // ----- 状態 -----
    public enum NPCStateType
    {
        Standing,
        Browsing,
        Browsing_BasedOnNode
    }

    [Header("状態の設定")]
    [SerializeField] private NPCStateType _initialStateType = NPCStateType.Standing;
    [SerializeField] private NPCStateType _currentStateType = NPCStateType.Standing;
    private StateMachine<NPC_Controller> _NPCState;
    private NPCStanding _NPCStanding = new NPCStanding();
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing();
    private NPCBrowsing_BasedOnNode _NPCBrowsing_BasedOnNode = new NPCBrowsing_BasedOnNode();

    public float ArrivalDistance => _arrivalDistance;
    public NPCStateType CurrentStateType => _currentStateType;


    // ===== Unityのライフサイクル =====

    void Awake()
    {
        // 初期位置と初期向きを記憶
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;

        rb = GetComponent<Rigidbody>();
        _locomotion = GetComponent<NPC_FlowLocomotion>();

        // 回転のフリーズ(向きは足が MoveRotation で変える)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // ステートマシンの初期化
        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);

        // 初期状態の設定
        SetState(_initialStateType);
    }

    void FixedUpdate()
    {
        // ステートマシンの更新
        _NPCState.OnUpdate();
    }

    public void SetPhysicsLodSuspended(bool suspended)
    {
        if (physicsLodSuspended == suspended) return;

        if (suspended)
        {
            controllerEnabledBeforeLod = enabled;
            locomotionEnabledBeforeLod = _locomotion.enabled;
            wasKinematicBeforeLod = rb.isKinematic;
            detectedCollisionsBeforeLod = rb.detectCollisions;
            wasSleepingBeforeLod = rb.IsSleeping();
            if (!wasKinematicBeforeLod)
            {
                velocityBeforeLod = rb.linearVelocity;
                angularVelocityBeforeLod = rb.angularVelocity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.detectCollisions = false;
            rb.isKinematic = true;
            physicsLodSuspended = true;
            enabled = false;
            _locomotion.enabled = false;   // 足も止める(Thinkの負荷も止まる)
            return;
        }

        rb.isKinematic = wasKinematicBeforeLod;
        rb.detectCollisions = detectedCollisionsBeforeLod;
        if (!wasKinematicBeforeLod)
        {
            rb.linearVelocity = velocityBeforeLod;
            rb.angularVelocity = angularVelocityBeforeLod;
            if (wasSleepingBeforeLod) rb.Sleep();
            else rb.WakeUp();
        }

        physicsLodSuspended = false;
        enabled = controllerEnabledBeforeLod;
        _locomotion.enabled = locomotionEnabledBeforeLod;
    }

    // ===== 足への指示 =====

    private void Move(Vector3 dir, float speed, float rate = 1f)
    {
        _locomotion.MoveSpeed = speed;
        _locomotion.SetDesiredDirection(dir, rate);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ===== 立ち止まり =====

    // 初期位置からずれていたら戻る。戻ったら初期の向きを向いて踏ん張る
    public void UpdateStanding()
    {
        _locomotion.SetIdleFacing(_defaultRotation * Vector3.forward);

        Vector3 toDefault = Flat(_defaultPosition - transform.position);
        float dist = toDefault.magnitude;
        if (dist <= _stopDeadZone)
        {
            _locomotion.Stop();
            return;
        }

        // 近づくほど減速(行き過ぎて往復しないように)
        float rate = Mathf.Clamp(dist / Mathf.Max(_returnSlowRadius, 0.01f), 0.2f, 1f);
        Move(toDefault, _returnSpeed, rate);
    }

    // ===== 巡回地点(点から点) =====

    public void UpdatePatrol()
    {
        _locomotion.ClearIdleFacing();
        if (_targetPositions == null || _targetPositions.Length == 0)
        {
            _locomotion.Stop();
            return;
        }

        Vector3 toTarget = Flat(_targetPositions[_currentTargetIndex] - transform.position);
        if (toTarget.magnitude < _arrivalDistance)
        {
            AdvanceToNextTarget();
            return;
        }
        Move(toTarget, _browseSpeed);
    }

    public void AdvanceToNextTarget()
    {
        if (_targetPositions == null || _targetPositions.Length == 0) return;
        _currentTargetIndex = (_currentTargetIndex + 1) % _targetPositions.Length;
    }

    public Vector3 GetCurrentPatrolPosition()
    {
        if (_targetPositions == null || _targetPositions.Length == 0) return transform.position;
        return _targetPositions[_currentTargetIndex];
    }

    // ===== ノードベースの移動(エッジに沿って平行に進む) =====
    //
    // エッジ上の位置を2つの数字で考える
    //   s   : 始点から終点に向かってどこまで進んだか
    //   lat : 中心線から右にどれだけずれているか
    // 進む方向 = エッジの向き + 目標のズレ(_offsetRate × 幅の半分)へ戻る小さな補正
    // 到着判定 = s が「終点の少し手前」を越えたか(点ではなく線で判定するので1点に集まらない)

    public void UpdateNodeTravel()
    {
        _locomotion.ClearIdleFacing();
        if (!_hasEdge)
        {
            _locomotion.Stop();
            return;
        }

        Vector3 axis = Flat(_edgeTo - _edgeFrom);
        float length = axis.magnitude;
        if (length < 0.01f)
        {
            AdvanceToNextNode();
            return;
        }
        Vector3 forward = axis / length;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 rel = Flat(transform.position - _edgeFrom);
        float s = Vector3.Dot(rel, forward);
        float lat = Vector3.Dot(rel, right);

        // 到着:終点の手前の線を越えた
        if (s >= length - _arrivalDistance)
        {
            AdvanceToNextNode();
            return;
        }

        // 押されてずれた分を少しずつ「自分の位置」として受け入れる
        if (_edgeHalfWidth > 0.01f && _offsetAdaptRate > 0f)
        {
            float currentRate = Mathf.Clamp(lat / _edgeHalfWidth, -_maxOffsetRate, _maxOffsetRate);
            float t = 1f - Mathf.Exp(-_offsetAdaptRate * Time.fixedDeltaTime);
            _offsetRate = Mathf.Lerp(_offsetRate, currentRate, t);
        }

        // 目標のズレへ戻る補正(ずれが大きいほど斜めに戻る。上限あり)
        float targetLat = _offsetRate * _edgeHalfWidth;
        float correction = Mathf.Clamp((targetLat - lat) * _lateralGain, -_maxLateralCorrection, _maxLateralCorrection);

        Move(forward + right * correction, _browseSpeed);
    }

    // 今向かっているノードを設定する(スポナーなどから)。
    // 「どこから来たか」は、隣のノードのうち、自分がそのエッジ上に一番近いものとみなす。
    // 見つからなければ、今の位置からノードへ向かう仮のエッジを作る。
    public void SetNode(Node_Edge_Data.Node node)
    {
        _node = node;
        EnsureGenerator();

        int bestFrom = -1;
        float bestDist = float.PositiveInfinity;
        if (_nodeGenerator != null && node.adjacents != null)
        {
            foreach (int adjID in node.adjacents)
            {
                Vector3 a = _nodeGenerator.GetNode(adjID).position;
                float d = DistanceToSegment(Flat(transform.position), Flat(a), Flat(node.position));
                if (d < bestDist)
                {
                    bestDist = d;
                    bestFrom = adjID;
                }
            }
        }

        if (bestFrom != -1)
        {
            BeginEdge(_nodeGenerator.GetNode(bestFrom), node, measureOffset: true);
        }
        else
        {
            // 仮のエッジ:今の位置 → ノード
            _previousNodeID = -1;
            _edgeFrom = transform.position;
            _edgeTo = node.position;
            _edgeHalfWidth = _defaultEdgeWidth * 0.5f;
            _offsetRate = 0f;
            _hasEdge = true;
        }
    }

    // 現在のノードの座標を取得
    public Vector3 GetCurrentNodePosition()
    {
        return _node.position;
    }

    // 次のノードに進む(直前のノードにはなるべく戻らない)
    public void AdvanceToNextNode()
    {
        if (_node.adjacents == null || _node.adjacents.Count == 0)
        {
            // 行き止まり:止まる
            _hasEdge = false;
            return;
        }

        int count = _node.adjacents.Count;
        int nextNodeID;

        // 直前のノードが隣接リストに含まれていて、ほかに選択肢がある場合
        int prevIndex = (_previousNodeID != -1 && count > 1)
            ? _node.adjacents.IndexOf(_previousNodeID)
            : -1;

        if (prevIndex != -1)
        {
            // 直前のノードを除いた (count - 1) 個から抽選
            int randomIndex = Random.Range(0, count - 1);
            // 直前と同じインデックス以上の値が出たら、1つずらす
            if (randomIndex >= prevIndex) randomIndex++;
            nextNodeID = _node.adjacents[randomIndex];
        }
        else
        {
            // 行き止まり(戻るしかない)の場合は通常抽選
            nextNodeID = _node.adjacents[Random.Range(0, count)];
        }

        EnsureGenerator();
        if (_nodeGenerator == null)
        {
            _hasEdge = false;
            return;
        }

        Node_Edge_Data.Node from = _node;
        Node_Edge_Data.Node next = _nodeGenerator.GetNode(nextNodeID);
        _node = next;
        // ズレの割合はそのまま引き継ぐ(右寄りの人は次のエッジでも右寄り)
        BeginEdge(from, next, measureOffset: false);
    }

    // from → to のエッジを歩き始める
    private void BeginEdge(Node_Edge_Data.Node from, Node_Edge_Data.Node to, bool measureOffset)
    {
        _previousNodeID = from.NodeID;
        _edgeFrom = from.position;
        _edgeTo = to.position;

        float width = _defaultEdgeWidth;
        if (_nodeGenerator != null)
        {
            int edgeIndex = _nodeGenerator.FindEdgeIndex(from.NodeID, to.NodeID);
            if (edgeIndex != -1) width = _nodeGenerator.GetEdgeWidth(edgeIndex);
        }
        _edgeHalfWidth = Mathf.Max(0f, width * 0.5f);

        if (measureOffset)
        {
            // 今いる位置から、中心からのズレを測る(スポーン位置のばらつきがそのまま残る)
            Vector3 axis = Flat(_edgeTo - _edgeFrom);
            Vector3 right = axis.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, axis.normalized) : Vector3.right;
            float lat = Vector3.Dot(Flat(transform.position - _edgeFrom), right);
            _offsetRate = _edgeHalfWidth > 0.01f ? lat / _edgeHalfWidth : 0f;
        }
        _offsetRate = Mathf.Clamp(_offsetRate, -_maxOffsetRate, _maxOffsetRate);
        _hasEdge = true;
    }

    private void EnsureGenerator()
    {
        if (_nodeGenerator == null) _nodeGenerator = FindAnyObjectByType<NodeEdgeGenerator>();
    }

    // 古い呼び出しとの互換用
    public void SetNodeData(GameObject data)
    {
        if (data != null) _nodeGenerator = data.GetComponent<NodeEdgeGenerator>();
    }

    public void SetNodeGenerator(NodeEdgeGenerator generator) => _nodeGenerator = generator;

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }

    // ===== 状態の切り替え =====
    public void SetState(string stateName)
    {
        switch (stateName)
        {
            case "Standing": SetState(NPCStateType.Standing); break;
            case "Browsing": SetState(NPCStateType.Browsing); break;
            case "Browsing_BasedOnNode": SetState(NPCStateType.Browsing_BasedOnNode); break;
            default: Debug.LogWarning($"Unknown state: {stateName}"); break;
        }
    }

    public void SetState(NPCStateType stateType)
    {
        switch (stateType)
        {
            case NPCStateType.Standing:
                _NPCState.ChangeState(_NPCStanding);
                break;
            case NPCStateType.Browsing:
                _NPCState.ChangeState(_NPCBrowsing);
                break;
            case NPCStateType.Browsing_BasedOnNode:
                _NPCState.ChangeState(_NPCBrowsing_BasedOnNode);
                break;
            default:
                Debug.LogWarning($"Unknown state type: {stateType}");
                return;
        }
        _currentStateType = stateType;
    }

    // ===== デバッグ表示 =====
    // 選択中:歩いているエッジの幅(灰色)と、目標のズレの線(水色)
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !_hasEdge || _currentStateType != NPCStateType.Browsing_BasedOnNode) return;

        Vector3 axis = Flat(_edgeTo - _edgeFrom);
        if (axis.sqrMagnitude < 1e-6f) return;
        Vector3 right = Vector3.Cross(Vector3.up, axis.normalized);
        Vector3 up = Vector3.up * 0.05f;

        Vector3 l0 = _edgeFrom - right * _edgeHalfWidth + up, l1 = _edgeTo - right * _edgeHalfWidth + up;
        Vector3 r0 = _edgeFrom + right * _edgeHalfWidth + up, r1 = _edgeTo + right * _edgeHalfWidth + up;
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(l0, l1);
        Gizmos.DrawLine(r0, r1);

        Vector3 o = right * (_offsetRate * _edgeHalfWidth) + up;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_edgeFrom + o, _edgeTo + o);

        // 到着ライン(黄色)
        Vector3 arrive = _edgeTo - axis.normalized * _arrivalDistance + up;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(arrive - right * _edgeHalfWidth, arrive + right * _edgeHalfWidth);
    }
}
