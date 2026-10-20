using UnityEngine;

public class NPC_Controller : MonoBehaviour
{

    Rigidbody rb;


    [Header("立ち止まっている状態の設定")]
    Vector3 _defaultPosition = new Vector3(0, 0, 0);
    [SerializeField] private float _returnSpeed = 4.0f;       // 元の位置に戻る時の最高速度
    [SerializeField] private float _returnAcceleration = 8.0f;   // 戻る時の加速の強さ

    // TaragetPositions
    [Header("徘徊状態の設定")]

    [SerializeField]
    private Vector3[] _targetPositions = new Vector3[]
    {
        new Vector3(5, 0, 0),
        new Vector3(-5, 0, 0),
        new Vector3(0, 0, 5)
    };
    [SerializeField] private int _currentTargetIndex = 0;          // 現在の巡回ポイントのリストのインデックス
    [SerializeField] private float _browseSpeed = 8f;
    [SerializeField] private float _browseAcceleration = 8.0f;
    [SerializeField] private float _arrivalDistance = 0.5f;         // 目的地に到着したとみなす距離


    // ----- 状態 -----
    [Header("状態の設定")]
    [SerializeField] private string initialState = "Standing"; // 初期状態を指定（"Standing" または "Browsing"）
    private StateMachine<NPC_Controller> _NPCState;      // 運行状態(通常/詰まり
    private NPCStanding _NPCStanding = new NPCStanding(); // 立ち止まり状態
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing(); // 徘徊状態

    public float ArrivalDistance => _arrivalDistance;

    void Awake()
    {
        _defaultPosition = transform.position;

        rb = GetComponent<Rigidbody>();

        // 物理演算で勝手に転がらないように、回転を固定（Z軸やX軸の傾き防止）
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);

        // 仮データの作成
        _targetPositions = new Vector3[3] { new Vector3(10, 0, 0), new Vector3(-10, 0, 0), new Vector3(0, 0, 10) };

    }

    void Start()
    { 
        // 初期状態を設定
        SetState(initialState);
    }

    void FixedUpdate()
    {
        // 物理演算（Rigidbody）を扱う移動処理は、UpdateではなくFixedUpdateで行うのが鉄則です
        _NPCState.OnUpdate();

        // 速度の出力
        Debug.Log($"NPC Velocity: {rb.linearVelocity}");
    }

    // 次の目的地をセットする関数
    public void AdvanceToNextTarget()
    {
        // 順番に巡回する場合（3つの座標をループ）
        _currentTargetIndex = (_currentTargetIndex + 1) % _targetPositions.Length;

        // ランダムにしたい場合
        // CurrentTargetIndex = UnityEngine.Random.Range(0, TargetPositions.Length);
    }


    public Vector3 GetCurrentPatrolPosition()
    {
        return _targetPositions[_currentTargetIndex];
    }

    

    // --- 物理移動・復帰処理（共通ロジック） ---
    public void MoveTowardsPatrolPoint(Vector3 targetPos)
    {
        AccelerateTowards(targetPos, _browseSpeed, _browseAcceleration);
    }

    public void ReturnToDefaultPosition()
    {
        AccelerateTowards(_defaultPosition, _returnSpeed, _returnAcceleration);
    }

    private void AccelerateTowards(Vector3 targetPos, float maxSpeed, float accel)
    {
        targetPos.y = transform.position.y; // 高さの引っ張り合いを防止
        Vector3 toTarget = targetPos - transform.position;

        Vector3 desiredVelocity = Vector3.zero;
        if (toTarget.magnitude > 0.05f)
        {
            desiredVelocity = toTarget.normalized * maxSpeed;
            desiredVelocity.y = rb.linearVelocity.y; // 重力落下速度は維持
        }

        // 目標速度に向けて加速度ベースで補間
        Vector3 newVelocity = Vector3.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            accel * Time.fixedDeltaTime
        );

        // 押された・吹っ飛んだときの余白を残しつつ上限を設ける
        rb.linearVelocity = Vector3.ClampMagnitude(newVelocity, maxSpeed * 2.0f);

        // 進行方向への旋回
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.01f)
        {
            transform.forward = toTarget.normalized;
        }
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
