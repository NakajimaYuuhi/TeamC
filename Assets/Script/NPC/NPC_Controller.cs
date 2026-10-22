using UnityEngine;

public class NPC_Controller : MonoBehaviour
{
    private Rigidbody rb;
    private bool physicsLodSuspended;
    private bool controllerEnabledBeforeLod;
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

    // 初期位置に戻る際のパラメータ
    [SerializeField] private float _returnSpeed = 3.0f;         // 最大速度
    [SerializeField] private float _returnAcceleration = 6.0f;  // 加速度

    // 初期位置に戻る際のデッドゾーン
    [SerializeField] private float _stopDeadZone = 0.25f;       // 初期位置とみなす範囲内

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
    [SerializeField] private float _browseAcceleration = 10.0f;
    [SerializeField] private float _arrivalDistance = 0.5f;

    [Header("ノードベースでの徘徊状態の設定")]
    [SerializeField] private Node_Edge_Data.Node _node;
    // 過去のノードのID
    private int _previousNodeID = -1;
    // 仮でノードのデータを持ってる場所
    [SerializeField] private GameObject _nodeData;

    // ----- 状態 -----
    public enum NPCStateType
    {
        Standing,
        Browsing,
        Browsing_BasedOnNode

    }


    [Header("状態の設定")]
    [SerializeField] private string initialState = "Standing";
    [SerializeField] private NPCStateType _initialStateType = NPCStateType.Standing;
    [SerializeField] private NPCStateType _currentStateType = NPCStateType.Standing;
    private StateMachine<NPC_Controller> _NPCState;
    private NPCStanding _NPCStanding = new NPCStanding();
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing();
    private NPCBrowsing_BasedOnNode _NPCBrowsing_BasedOnNode = new NPCBrowsing_BasedOnNode();


    [Header("回避（ステアリング）設定")]
    [SerializeField] private float _avoidRadius = 1.4f;
    [SerializeField] private float _avoidWeight = 1.5f;
    [SerializeField] private LayerMask _npcLayer;
    [SerializeField] private float _avoidSmoothing = 6.0f;
    [SerializeField] private float _turnSpeed = 10.0f;

    private Vector3 _currentAvoidDir = Vector3.forward;

    public float ArrivalDistance => _arrivalDistance;


    // ===== Unityのライフサイクル =====

    // Awake
    // デフォルトパラメータの初期化
    void Awake()
    {

        // 初期位置と初期向きを記憶
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation; // 初期の向きを記憶

        // Rigidbody の取得
        rb = GetComponent<Rigidbody>();

        // 回転のフリーズ
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // ステートマシンの初期化
        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);

        // 初期状態の設定
        SetState(_initialStateType);
    }

    // Start
    void Start()
    {

    }

    // FixedUpdate
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
    }

    // ===== 巡回地点の管理 =====

    // --- 次の巡回地点に進む ---
    public void AdvanceToNextTarget()
    {
        // 次の巡回地点に進む
        if (_targetPositions == null || _targetPositions.Length == 0) return;
        _currentTargetIndex = (_currentTargetIndex + 1) % _targetPositions.Length;
    }

    // --- 現在の巡回地点を取得 ---
    public Vector3 GetCurrentPatrolPosition()
    {
        if (_targetPositions == null || _targetPositions.Length == 0) return transform.position;
        return _targetPositions[_currentTargetIndex];
    }

    // --- 巡回時の移動 ---
    public void MoveTowardsPatrolPoint(Vector3 targetPos)
    {
        Vector3 steerTarget = GetAvoidanceTarget(targetPos);
        //Debug.Log($"NPC is moving towards steerTarget position: {steerTarget}");

        // 通常の徘徊移動（デッドゾーンは小さめ 0.05m）
        AccelerateTowards(steerTarget, _browseSpeed, _browseAcceleration, 0.05f, false);
    }

    // --- 立ち止まり時の復帰 ---
    public void ReturnToDefaultPosition()
    {
        var position = transform.position;
        var offset = new Vector2(position.x - _defaultPosition.x, position.z - _defaultPosition.z);
        if (offset.sqrMagnitude <= _stopDeadZone * _stopDeadZone && transform.rotation == _defaultRotation)
        {
            if (rb.IsSleeping()) return;

            var velocity = rb.linearVelocity;
            if (velocity.x == 0f && velocity.z == 0f) return;
        }

        // [改善]_stopDeadZone を渡し、かつ初期の向きを保たせるフラグ(isStanding=true)を立てる
        AccelerateTowards(_defaultPosition, _returnSpeed, _returnAcceleration, _stopDeadZone, true);
    }

    // ===== 移動処理 =====
    private void AccelerateTowards(Vector3 targetPos, float maxSpeed, float accel, float stopThreshold, bool isStanding)
    {
        Vector3 currentPos = transform.position;
        Vector3 toTarget = new Vector3(targetPos.x - currentPos.x, 0f, targetPos.z - currentPos.z);
        float distToTarget = toTarget.magnitude;

        Vector3 desiredHorizontalVel = Vector3.zero;

        // 目標地点から一定距離以上離れているときだけ自力で進む（デッドゾーン内なら desiredVelocity は 0）
        if (distToTarget > stopThreshold)
        {
            desiredHorizontalVel = toTarget.normalized * maxSpeed;
        }

        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        Vector3 newHorizontalVel = Vector3.MoveTowards(
            currentHorizontalVel,
            desiredHorizontalVel,
            accel * Time.fixedDeltaTime
        );

        newHorizontalVel = Vector3.ClampMagnitude(newHorizontalVel, maxSpeed);
        rb.linearVelocity = new Vector3(newHorizontalVel.x, rb.linearVelocity.y, newHorizontalVel.z);

        // --- 旋回（首振り）の制御 ---
        if (isStanding)
        {
            // 立ち止まり中：大きく動かされている時は移動方向を向くが、
            // デッドゾーン付近や押し合いでモゾモゾしている時は初期の向き（前）を向いて踏ん張る
            if (distToTarget > stopThreshold * 1.5f && newHorizontalVel.sqrMagnitude > 0.3f)
            {
                Quaternion targetRot = Quaternion.LookRotation(newHorizontalVel.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnSpeed * Time.fixedDeltaTime);
            }
            else
            {
                // 初期向きに戻す（ぐるぐる回転の完全防止）
                transform.rotation = Quaternion.Slerp(transform.rotation, _defaultRotation, _turnSpeed * 0.5f * Time.fixedDeltaTime);
            }
        }
        else
        {
            // 徘徊中：しっかり進んでいる時だけ旋回
            if (newHorizontalVel.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(newHorizontalVel.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _turnSpeed * Time.fixedDeltaTime);
            }
        }
    }

    // ==== 回避（ステアリング）処理 =====
    private Vector3 GetAvoidanceTarget(Vector3 originalTarget)
    {
        Vector3 toTarget = originalTarget - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude < 0.01f) return originalTarget;
        toTarget.Normalize();

        // 1. 周囲のNPCを球状コライダーでスキャン
        Collider[] hits = Physics.OverlapSphere(transform.position, _avoidRadius, _npcLayer);

        Vector3 separation = Vector3.zero;
        int frontObstacleCount = 0;

        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;// 自分自身は除外

            Vector3 diff = transform.position - hit.transform.position;
            diff.y = 0;
            float dist = diff.magnitude;

            // 前方の視野（約120度以内）にいる人のみ回避対象にする
            if (Vector3.Dot(toTarget, -diff.normalized) < 0.2f) continue;

            if (dist > 0.01f)
            {
                // 距離に反比例した反発力を決定（近ければ近いほど強く離れる）
                separation += (diff.normalized / dist);
                frontObstacleCount++;
            }
        }

        // 周囲に障害物がいない場合：本来の目的地方向へスムーズに戻す
        if (frontObstacleCount == 0)
        {
            _currentAvoidDir = Vector3.RotateTowards(_currentAvoidDir, toTarget, _avoidSmoothing * Time.fixedDeltaTime, 0.0f);
            return originalTarget;
        }

        // [チャタリング・膠着打破]2人の真ん中で左右の反発力が相殺された場合は、強制的に右側へバイアスを加える
        if (frontObstacleCount >= 2 && separation.sqrMagnitude < 0.3f)
        {
            Vector3 rightDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            separation += rightDir * 1.5f;
        }

        // 2. 「前進ベクトル」と「反発ベクトル」の合成
        Vector3 steerDir = toTarget + (separation * _avoidWeight);

        // 【前進成分の保証】反発が強すぎて真横や後ろを向いてしまうのを防ぐ（最低限の前進力を維持）
        if (Vector3.Dot(toTarget, steerDir.normalized) < 0.3f)
        {
            Vector3 sideDir = Vector3.ProjectOnPlane(steerDir, toTarget);
            steerDir = (toTarget * 0.5f + sideDir.normalized * 0.8f).normalized;
        }

        // 3. 回避方向の急激な反転を抑えるため、滑らかに角度補間（ヒステリシス）
        _currentAvoidDir = Vector3.RotateTowards(
            _currentAvoidDir,
            steerDir.normalized,
            _avoidSmoothing * Time.fixedDeltaTime,
            0.0f
        );

        // 現在位置から回避方向に十分離れた地点（3m先）を目標地点として返す（手前失速の防止）
        Vector3 avoidTarget = transform.position + _currentAvoidDir * 3.0f;
        avoidTarget.y = transform.position.y;
        return avoidTarget;
    }

    // ===== 状態の切り替え =====
    public void SetState(string stateName)
    {
        switch (stateName)
        {
            case "Standing":
                _NPCState.ChangeState(_NPCStanding);
                _currentStateType = NPCStateType.Standing;
                break;
            case "Browsing":
                _NPCState.ChangeState(_NPCBrowsing);
                _currentStateType = NPCStateType.Browsing;
                break;
            case "Browsing_BasedOnNode":
                _NPCState.ChangeState(_NPCBrowsing_BasedOnNode);
                _currentStateType = NPCStateType.Browsing_BasedOnNode;
                break;
            default:
                Debug.LogWarning($"Unknown state: {stateName}");
                break;
        }
    }

    public void SetState(NPCStateType stateType)
    {
        switch (stateType)
        {
            case NPCStateType.Standing:
                _NPCState.ChangeState(_NPCStanding);
                _currentStateType = NPCStateType.Standing;
                break;
            case NPCStateType.Browsing:
                _NPCState.ChangeState(_NPCBrowsing);
                _currentStateType = NPCStateType.Browsing;
                break;
            case NPCStateType.Browsing_BasedOnNode:
                _NPCState.ChangeState(_NPCBrowsing_BasedOnNode);
                _currentStateType = NPCStateType.Browsing_BasedOnNode;
                break;
            default:
                Debug.LogWarning($"Unknown state type: {stateType}");
                break;
        }
    }


    // ===== ノードベースの移動用の関数 =====

    // ノードのセット
    public void SetNode(Node_Edge_Data.Node node)
    {
        _node = node;
    }

    // 現在のノードの座標を取得
    public Vector3 GetCurrentNodePosition()
    {
        return _node.position;
    }

    // 次のノードに進む（ここでは単純にランダムで次のノードを選ぶ）
    public void AdvanceToNextNode()
    {


        if (_node.adjacents == null || _node.adjacents.Count == 0) return;

        // 再抽選が効率悪いなら...
        // 過去のノードのインデックスを記録して、ランダム抽選を1少ない数で行う
        // 過去のノードのインデックッス以上のインデックスなら、インデックス値を1足して使う

        // 再抽選方式
        // 無限ループの可能性がゼロではない（よっぽど無い）

        //// ランダムに次のノードを選ぶ
        //int randomIndex = Random.Range(0, _node.adjacents.Count);
        //int nextNodeID = _node.adjacents[randomIndex];

        //// 現在のノードと同じノードを選んでしまった場合は、再度ランダムに選び直す
        //while (_previousNodeID != -1 && nextNodeID == _previousNodeID && _node.adjacents.Count > 1 )
        //{
        //    randomIndex = Random.Range(0, _node.adjacents.Count);
        //    nextNodeID = _node.adjacents[randomIndex];
        //}



        int count = _node.adjacents.Count;
        int nextNodeID;

        // 直前のノードが隣接リストに含まれていて、かつ他に選択肢がある場合
        int prevIndex = (_previousNodeID != -1 && count > 1)
            ? _node.adjacents.IndexOf(_previousNodeID)
            : -1;

        if (prevIndex != -1)
        {
            // 直前のノードを除いた (count - 1) 個から抽選
            int randomIndex = UnityEngine.Random.Range(0, count - 1);

            // 除外したインデックス以上の値が出たら、1つ後ろにずらす
            if (randomIndex >= prevIndex)
            {
                randomIndex++;
            }

            nextNodeID = _node.adjacents[randomIndex];
        }
        else
        {
            // 初回や袋小路（戻るしかない）の場合は通常抽選
            int randomIndex = UnityEngine.Random.Range(0, count);
            nextNodeID = _node.adjacents[randomIndex];
        }



        // 過去ノードを更新
        _previousNodeID = _node.NodeID;

        // ノードのIDからノードを取得
        NodeEdgeGenerator nodeData = _nodeData.GetComponent<NodeEdgeGenerator>();

        // 次のノードを取得
        Node_Edge_Data.Node nextNode = nodeData.GetNode(nextNodeID);        // ノードを更新
        SetNode(nextNode);
    }


}