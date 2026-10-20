//using UnityEngine;

//public class NPC_Controller : MonoBehaviour
//{
//    private Rigidbody rb;

//    [Header("立ち止まっている状態の設定")]
//    private Vector3 _defaultPosition = new Vector3(0, 0, 0);
//    [SerializeField] private float _returnSpeed = 4.0f;
//    [SerializeField] private float _returnAcceleration = 8.0f;

//    [Header("徘徊状態の設定")]
//    [SerializeField]
//    private Vector3[] _targetPositions = new Vector3[]
//    {
//        new Vector3(5, 0, 0),
//        new Vector3(-5, 0, 0),
//        new Vector3(0, 0, 5)
//    };
//    [SerializeField] private int _currentTargetIndex = 0;
//    [SerializeField] private float _browseSpeed = 8f;
//    [SerializeField] private float _browseAcceleration = 8.0f;
//    [SerializeField] private float _arrivalDistance = 0.5f;

//    // ----- 状態 -----
//    [Header("状態の設定")]
//    [SerializeField] private string initialState = "Standing";
//    [SerializeField] private string currentState = "Standing";
//    private StateMachine<NPC_Controller> _NPCState;
//    private NPCStanding _NPCStanding = new NPCStanding();
//    private NPCBrowsing _NPCBrowsing = new NPCBrowsing();

//    [Header("回避（ステアリング）設定")]
//    [SerializeField] private float _avoidRadius = 1.2f;
//    [SerializeField] private float _avoidWeight = 1.5f;
//    [SerializeField] private LayerMask _npcLayer;
//    [SerializeField] private float _avoidSmoothing = 5.0f;
//    [SerializeField] private float _turnSpeed = 10.0f; // 旋回スピード（滑らかに振り向く）

//    private Vector3 _currentAvoidDir = Vector3.forward; // ゼロベクトル事故防止

//    public float ArrivalDistance => _arrivalDistance;

//    void Awake()
//    {
//        _defaultPosition = transform.position;
//        rb = GetComponent<Rigidbody>();

//        // 勝手に倒れないように回転固定
//        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

//        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);
//    }

//    void Start()
//    {
//        SetState(initialState);
//    }

//    void FixedUpdate()
//    {
//        _NPCState.OnUpdate();
//    }

//    public void AdvanceToNextTarget()
//    {
//        if (_targetPositions == null || _targetPositions.Length == 0) return;
//        _currentTargetIndex = (_currentTargetIndex + 1) % _targetPositions.Length;
//    }

//    public Vector3 GetCurrentPatrolPosition()
//    {
//        if (_targetPositions == null || _targetPositions.Length == 0) return transform.position;
//        return _targetPositions[_currentTargetIndex];
//    }

//    // --- 物理移動・復帰処理 ---
//    public void MoveTowardsPatrolPoint(Vector3 targetPos)
//    {
//        Vector3 steerTarget = GetAvoidanceTarget(targetPos);
//        AccelerateTowards(steerTarget, _browseSpeed, _browseAcceleration);
//    }

//    public void ReturnToDefaultPosition()
//    {
//        AccelerateTowards(_defaultPosition, _returnSpeed, _returnAcceleration);
//    }

//    private void AccelerateTowards(Vector3 targetPos, float maxSpeed, float accel)
//    {
//        // 水平方向（XZ平面）のみで距離と方向を計算
//        Vector3 currentPos = transform.position;
//        Vector3 toTarget = new Vector3(targetPos.x - currentPos.x, 0f, targetPos.z - currentPos.z);

//        Vector3 desiredHorizontalVel = Vector3.zero;
//        if (toTarget.sqrMagnitude > 0.0025f) // 約0.05m以上の距離
//        {
//            desiredHorizontalVel = toTarget.normalized * maxSpeed;
//        }

//        // 現在の水平速度のみを補間（Y軸の重力落下速度には一切干渉しない！）
//        Vector3 currentHorizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
//        Vector3 newHorizontalVel = Vector3.MoveTowards(
//            currentHorizontalVel,
//            desiredHorizontalVel,
//            accel * Time.fixedDeltaTime
//        );

//        // 水平速度の上限クランプ
//        newHorizontalVel = Vector3.ClampMagnitude(newHorizontalVel, maxSpeed * 2.0f);

//        // 水平速度 + 元の重力速度(Y) を合成して戻す
//        rb.linearVelocity = new Vector3(newHorizontalVel.x, rb.linearVelocity.y, newHorizontalVel.z);



//        // 旋回（前進方向を向く）
//        //if (newHorizontalVel.sqrMagnitude > 0.05f)
//        //{
//        //    transform.forward = newHorizontalVel.normalized;
//        //}

//        // 改善:急に向きを変えない 
//        //目標地点ではなく「実際に移動している速度の向き」に向かって滑らかに旋回する
//        if (newHorizontalVel.sqrMagnitude > 0.1f)
//        {
//            Quaternion targetRotation = Quaternion.LookRotation(newHorizontalVel.normalized);
//            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _turnSpeed * Time.fixedDeltaTime);
//        }
//    }

//    private Vector3 GetAvoidanceTarget(Vector3 originalTarget)
//    {
//        Vector3 toTarget = originalTarget - transform.position;
//        toTarget.y = 0;
//        if (toTarget.sqrMagnitude < 0.01f) return originalTarget;
//        toTarget.Normalize();

//        Collider[] hits = Physics.OverlapSphere(transform.position, _avoidRadius, _npcLayer);

//        Vector3 separation = Vector3.zero;
//        int obstacleCount = 0;

//        foreach (var hit in hits)
//        {
//            if (hit.gameObject == this.gameObject) continue;

//            Vector3 diff = transform.position - hit.transform.position;
//            diff.y = 0;
//            float dist = diff.magnitude;

//            // 前方視野（約120度）にいる人のみ判定
//            if (Vector3.Dot(toTarget, -diff.normalized) < 0.2f) continue;

//            if (dist > 0.01f)
//            {
//                // 距離に反比例した反発
//                separation += (diff.normalized / dist);
//                obstacleCount++;
//            }
//        }

//        if (obstacleCount == 0)
//        {
//            _currentAvoidDir = Vector3.RotateTowards(_currentAvoidDir, toTarget, _avoidSmoothing * Time.fixedDeltaTime, 0.0f);
//            return originalTarget;
//        }

//        // 正面衝突の膠着時は右側にバイアス
//        if (obstacleCount >= 2 && separation.sqrMagnitude < 0.3f)
//        {
//            Vector3 rightDir = Vector3.Cross(Vector3.up, toTarget).normalized;
//            separation += rightDir * 1.5f;
//        }

//        // 【改善2】進行方向（前進）を殺さない合成
//        // 目的地へのベクトル（toTarget）をベースにし、反発力は左右方向（横移動）を主にする
//        Vector3 steerDir = toTarget + (separation * _avoidWeight);

//        // 後ろ向きベクトルになってしまわないようクランプ（前進成分を最低でも0.3以上残す）
//        if (Vector3.Dot(toTarget, steerDir.normalized) < 0.3f)
//        {
//            // 横方向に逃げる成分だけを残しつつ前進力を維持
//            Vector3 sideDir = Vector3.ProjectOnPlane(steerDir, toTarget);
//            steerDir = (toTarget * 0.5f + sideDir.normalized * 0.8f).normalized;
//        }

//        Vector3 targetAvoidDir = steerDir.normalized;

//        // 回避ベクトルのスムーズ補間
//        _currentAvoidDir = Vector3.RotateTowards(
//            _currentAvoidDir,
//            targetAvoidDir,
//            _avoidSmoothing * Time.fixedDeltaTime,
//            0.0f
//        );

//        // 適切な距離（約3m先）を目標にする
//        Vector3 avoidTarget = transform.position + _currentAvoidDir * 3.0f;
//        avoidTarget.y = transform.position.y;
//        return avoidTarget;
//    }

//    void SetState(string stateName)
//    {
//        switch (stateName)
//        {
//            case "Standing":
//                _NPCState.ChangeState(_NPCStanding);
//                currentState = "Standing";
//                break;
//            case "Browsing":
//                _NPCState.ChangeState(_NPCBrowsing);
//                currentState = "Browsing";
//                break;
//            default:
//                Debug.LogWarning($"Unknown state: {stateName}");
//                break;
//        }
//    }
//}

using UnityEngine;

public class NPC_Controller : MonoBehaviour
{
    private Rigidbody rb;

    [Header("立ち止まっている状態の設定")]
    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation; // スポーン時の初期向き
    [SerializeField] private float _returnSpeed = 3.0f;
    [SerializeField] private float _returnAcceleration = 6.0f;
    [SerializeField] private float _stopDeadZone = 0.25f; // 【重要】この範囲内なら初期位置とみなして自力で押さない

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

    // ----- 状態 -----
    [Header("状態の設定")]
    [SerializeField] private string initialState = "Standing";
    private StateMachine<NPC_Controller> _NPCState;
    private NPCStanding _NPCStanding = new NPCStanding();
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing();

    [Header("回避（ステアリング）設定")]
    [SerializeField] private float _avoidRadius = 1.4f;
    [SerializeField] private float _avoidWeight = 1.5f;
    [SerializeField] private LayerMask _npcLayer;
    [SerializeField] private float _avoidSmoothing = 6.0f;
    [SerializeField] private float _turnSpeed = 10.0f;

    private Vector3 _currentAvoidDir = Vector3.forward;

    public float ArrivalDistance => _arrivalDistance;

    void Awake()
    {
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation; // 初期の向きを記憶

        rb = GetComponent<Rigidbody>();

        // 倒れ防止
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);
    }

    void Start()
    {
        SetState(initialState);
    }

    void FixedUpdate()
    {
        _NPCState.OnUpdate();
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

    // --- 巡回時の移動 ---
    public void MoveTowardsPatrolPoint(Vector3 targetPos)
    {
        Vector3 steerTarget = GetAvoidanceTarget(targetPos);
        // 通常の徘徊移動（デッドゾーンは小さめ 0.05m）
        AccelerateTowards(steerTarget, _browseSpeed, _browseAcceleration, 0.05f, false);
    }

    // --- 立ち止まり時の復帰 ---
    public void ReturnToDefaultPosition()
    {
        // 【改善】_stopDeadZone を渡し、かつ初期の向きを保たせるフラグ(isStanding=true)を立てる
        AccelerateTowards(_defaultPosition, _returnSpeed, _returnAcceleration, _stopDeadZone, true);
    }

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

    private Vector3 GetAvoidanceTarget(Vector3 originalTarget)
    {
        Vector3 toTarget = originalTarget - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude < 0.01f) return originalTarget;
        toTarget.Normalize();

        Collider[] hits = Physics.OverlapSphere(transform.position, _avoidRadius, _npcLayer);

        Vector3 separation = Vector3.zero;
        int frontObstacleCount = 0;

        foreach (var hit in hits)
        {
            if (hit.gameObject == this.gameObject) continue;

            Vector3 diff = transform.position - hit.transform.position;
            diff.y = 0;
            float dist = diff.magnitude;

            if (Vector3.Dot(toTarget, -diff.normalized) < 0.2f) continue;

            if (dist > 0.01f)
            {
                separation += (diff.normalized / dist);
                frontObstacleCount++;
            }
        }

        if (frontObstacleCount == 0)
        {
            _currentAvoidDir = Vector3.RotateTowards(_currentAvoidDir, toTarget, _avoidSmoothing * Time.fixedDeltaTime, 0.0f);
            return originalTarget;
        }

        if (frontObstacleCount >= 2 && separation.sqrMagnitude < 0.3f)
        {
            Vector3 rightDir = Vector3.Cross(Vector3.up, toTarget).normalized;
            separation += rightDir * 1.5f;
        }

        Vector3 steerDir = toTarget + (separation * _avoidWeight);

        if (Vector3.Dot(toTarget, steerDir.normalized) < 0.3f)
        {
            Vector3 sideDir = Vector3.ProjectOnPlane(steerDir, toTarget);
            steerDir = (toTarget * 0.5f + sideDir.normalized * 0.8f).normalized;
        }

        _currentAvoidDir = Vector3.RotateTowards(
            _currentAvoidDir,
            steerDir.normalized,
            _avoidSmoothing * Time.fixedDeltaTime,
            0.0f
        );

        Vector3 avoidTarget = transform.position + _currentAvoidDir * 3.0f;
        avoidTarget.y = transform.position.y;
        return avoidTarget;
    }

    void SetState(string stateName)
    {
        switch (stateName)
        {
            case "Standing":
                _NPCState.ChangeState(_NPCStanding);
                break;
            case "Browsing":
                _NPCState.ChangeState(_NPCBrowsing);
                break;
            default:
                Debug.LogWarning($"Unknown state: {stateName}");
                break;
        }
    }
}