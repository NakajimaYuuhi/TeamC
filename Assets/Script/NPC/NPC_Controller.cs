using UnityEngine;

public class NPC_Controller : MonoBehaviour
{
    Vector3 TargetPos = new Vector3(0, 0, 0);

    Rigidbody rb;


    [Header("立ち止まっている状態の設定")]
    [SerializeField] private float maxSpeed = 4.0f;       // 元の位置に戻る時の最高速度
    [SerializeField] private float acceleration = 8.0f;   // 戻る時の加速の強さ
    [SerializeField] private float knockbackPower = 10.0f; // ぶつかられた時に吹っ飛ぶ強さ（物理なので少し高めがおすすめ）

    // TaragetPositions
    [Header("徘徊状態の設定")]

    Vector3[] TargetPositions;
    public int CurrentTargetIndex = 0;
    public float MoveSpeed = 8f;
    public float BrowsingAcceleration = 8.0f;
    public float ArrivalDistance = 0.5f; // 目的地に到着したとみなす距離


    // ----- 状態 -----
    [SerializeField] private string initialState = "Standing"; // 初期状態を指定（"Standing" または "Browsing"）
    private StateMachine<NPC_Controller> _NPCState;      // 運行状態(通常/詰まり
    private NPCStanding _NPCStanding = new NPCStanding(); // 立ち止まり状態
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing(); // 徘徊状態




    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TargetPos = transform.position;

        rb = GetComponent<Rigidbody>();

        // 物理演算で勝手に転がらないように、回転を固定（Z軸やX軸の傾き防止）
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);

        // 仮データの作成
        TargetPositions = new Vector3[3] { new Vector3(10, 0, 0), new Vector3(-10, 0, 0), new Vector3(0, 0, 10) };

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



    // Update is called once per frame
    void Update()
    {
    }

    // 次の目的地をセットする関数
    public void AdvanceToNextTarget()
    {
        // 順番に巡回する場合（3つの座標をループ）
        CurrentTargetIndex = (CurrentTargetIndex + 1) % TargetPositions.Length;

        // ランダムにしたい場合はこちら：
        // CurrentTargetIndex = UnityEngine.Random.Range(0, TargetPositions.Length);
    }

    public Vector3 GetCurrentTarget()
    {
        return TargetPositions[CurrentTargetIndex];
    }

    public Vector3 GetCurrentPatrolPosition()
    {
        return TargetPositions[CurrentTargetIndex];
    }

    public void MoveToTarget(Vector3 targetPos)
    {
        Vector3 desiredVelocity = Vector3.zero;

        // 高さをNPC自身に合わせることで、Y軸の引っ張り合いを防ぐ
        targetPos.y = transform.position.y;

        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

        if (distance > 0.05f)
        {
            desiredVelocity = toTarget.normalized * MoveSpeed;
            desiredVelocity.y = rb.linearVelocity.y; // 重力は維持
        }

        Vector3 newVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVelocity, BrowsingAcceleration * Time.fixedDeltaTime);
        rb.linearVelocity = Vector3.ClampMagnitude(newVelocity, MoveSpeed * 2.0f);

        // 進行方向を向く処理（必要であれば）
        if (toTarget.magnitude > 0.1f)
        {
            Vector3 lookDir = toTarget;
            lookDir.y = 0;
            if (lookDir != Vector3.zero) transform.forward = lookDir;
        }
    }

    public void ReturnTarget()
    {

        // 戻る速度
        Vector3 desiredVelocity = Vector3.zero;
        
        
        // --- 最初の位置への方向と距離を計算 ---
        Vector3 toTarget = TargetPos - transform.position;
        float distance = toTarget.magnitude;
        

        // 最初の位置から少しでもズレていたら、戻るための理想の速度を計算
        if (distance > 0.05f)
        {
            // 最初の位置に向かう理想の速度（方向 × 最高速度）
            desiredVelocity = toTarget.normalized * maxSpeed;
            desiredVelocity.y = rb.linearVelocity.y; // 重力（Y軸の落下速度）は維持する
        }

        // 現在の物理的な速度（rb.linearVelocity）を、理想の速度に向けて滑らかに近づける
        Vector3 newVelocity = Vector3.MoveTowards(rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);

        // 速度上限（maxSpeed）の適用（Vector3.ClampMagnitude で安全にクランプ）
        // 吹っ飛んだ直後だけは上限を超えて吹っ飛んでほしいので、少し余裕（2倍）を持たせてクランプします
        rb.linearVelocity = Vector3.ClampMagnitude(newVelocity, maxSpeed * 2.0f);
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
