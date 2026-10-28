using UnityEngine;

// NPCの「足」。どっちに行きたいか(SetDesiredDirection)を受け取って、
// 前方の流れを見ながら実際に進む方向を決め、AddForceで動かす。
// どこへ行くか(ノード・立ち止まりなど)は NPC_Controller が決める。
//
// SetDesiredDirection が一度も呼ばれなければ、moveDirection の方向に進む
// (tmp_NPC_flowFollow と同じ動き。単体テスト用)
[RequireComponent(typeof(Rigidbody))]
public class NPC_FlowLocomotion : MonoBehaviour
{
    [Header("目的")]
    // SetDesiredDirection が呼ばれていないときに進む方向(テスト用)
    [SerializeField] Vector2 moveDirection = new Vector2(1, 0);
    // 基本の移動速度
    [SerializeField] float moveSpeed = 1f;

    [Header("流れの読み取り")]
    [SerializeField] FlowField field;
    // 何方向を候補として調べるか
    [SerializeField, Min(1)] int candidateCount = 9;
    // 普段、進みたい方向から左右何度まで調べるか
    [SerializeField, Range(0f, 90f)] float searchAngle = 60f;
    // どれくらい先の流れを見るか(m)
    [SerializeField] float lookAhead = 1.5f;
    // 「進みたい方向に近いこと」の重み
    [SerializeField] float goalWeight = 1f;
    // 「流れに乗ること」の重み
    [SerializeField] float flowWeight = 1f;
    // 流れに乗っているときの速度ボーナス(逆流だと遅くなる)
    [SerializeField, Range(0f, 1f)] float flowSpeedBonus = 0.5f;
    // 何秒ごとに方向を考え直すか
    [SerializeField] float thinkInterval = 0.2f;

    [Header("逆流を避ける")]
    // 正面の逆流が強いとき、左右何度まで探索範囲を広げるか
    [SerializeField, Range(0f, 120f)] float wideSearchAngle = 90f;
    // 正面の逆流にどれだけ敏感に探索範囲を広げるか
    [SerializeField, Min(0f)] float counterSensitivity = 2f;
    // 足元の「自分の向きの流れ」をどれだけ自信として使うか(0で使わない=前の動き)
    // 大きい群れの中にいる人ほど足元の流れが強いので、少しの逆流では避けなくなる
    // → 群れの大きさ(=力)の差で、どっちが道を譲るかが決まる
    [SerializeField, Min(0f)] float supportWeight = 1f;
    // 逆流のペナルティ(2乗で効くので、弱い逆流はほぼ無視・強い逆流だけ避ける)
    [SerializeField, Min(0f)] float counterWeight = 3f;
    // 今の方向を保とうとする強さ(左右に迷ってフラフラするのを防ぐ)
    [SerializeField, Min(0f)] float stickiness = 0.2f;
    // 正面の危険度がこれ未満なら、流れを探さずにまっすぐ進む(0にすると常に流れを探す=前の動き)
    [SerializeField, Range(0f, 1f)] float calmThreshold = 0.15f;
    // 逆流を避けるとき、どっち側に避けやすくするか(+で右、-で左、0で偏りなし)
    // 全員が同じルールで避けるので、向かい合った人同士が反対側に分かれてすれ違える
    [SerializeField, Range(-1f, 1f)] float passSideBias = 0.3f;

    [Header("動き")]
    // 方向転換のなめらかさ(大きいほど素早く曲がる)
    [SerializeField] float turnSpeed = 5f;
    // 目標速度に近づく強さ
    [SerializeField] float acceleration = 10f;
    // 体の向きを変える速さ
    [SerializeField] float bodyTurnSpeed = 10f;
    // この速さ以上で動いているときだけ進行方向を向く
    [SerializeField] float faceMinSpeed = 0.3f;

    Rigidbody rb;
    FlowLaneRider laneRider;   // 付いていればレーンの速度ボーナスを使う(なくても動く)
    FormationMember member;    // 付いていて席に入っていれば、隊形の速度で動く(なくても動く)

    // 外から指定される「進みたい方向」と速さの割合
    bool hasExternalDesire;
    Vector3 desiredDir;
    float desiredSpeedRate = 1f;

    // 止まっているときに向く方向(null = 今の向きのまま)
    bool hasIdleFacing;
    Vector3 idleFacing;

    Vector3 targetDir;      // Thinkで選んだ方向
    Vector3 currentDir;     // 実際に向かっている方向(targetDirへ徐々に寄せる)
    float speedScale = 1f;
    float thinkTimer;

    // デバッグ表示用
    Vector3[] debugDirs;
    float[] debugScores;
    float debugDanger;
    float debugSupport;

    // ===== 外から使う =====

    /// <summary>
    /// 進みたい方向を指定する(Y成分は無視)。
    /// speedRate は moveSpeed に対する割合(0で止まる。到着前の減速などに使う)
    /// </summary>
    public void SetDesiredDirection(Vector3 dir, float speedRate = 1f)
    {
        hasExternalDesire = true;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f)
        {
            desiredSpeedRate = 0f;
            return;
        }
        desiredDir = dir.normalized;
        desiredSpeedRate = Mathf.Max(0f, speedRate);
    }

    /// <summary>その場で止まる(押されたら戻らずに止まるだけ)</summary>
    public void Stop()
    {
        hasExternalDesire = true;
        desiredSpeedRate = 0f;
    }

    /// <summary>止まっているときに向く方向を指定する</summary>
    public void SetIdleFacing(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) return;
        hasIdleFacing = true;
        idleFacing = forward.normalized;
    }

    public void ClearIdleFacing() => hasIdleFacing = false;

    /// <summary>本人が行きたい向き(止まっているときは zero)。押されて動いている向きとは別</summary>
    public Vector3 DesiredDirection => IsStopped ? Vector3.zero : desiredDir;

    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = Mathf.Max(0f, value);
    }

    // ===== Unityのライフサイクル =====

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        laneRider = GetComponent<FlowLaneRider>();
        member = GetComponent<FormationMember>();
    }

    void Start()
    {
        if (field == null) field = FindAnyObjectByType<FlowField>();

        if (!hasExternalDesire)
        {
            desiredDir = new Vector3(moveDirection.x, 0, moveDirection.y).normalized;
        }
        targetDir = currentDir = desiredDir;

        // NPCごとに考えるタイミングをずらす(同じフレームに集中しないように)
        thinkTimer = Random.Range(0f, thinkInterval);
    }

    static Vector2 ToV2(Vector3 v) => new Vector2(v.x, v.z);

    bool IsStopped => desiredSpeedRate <= 0f || desiredDir.sqrMagnitude < 1e-6f;

    void Update()
    {
        if (IsStopped) return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer += thinkInterval;
            Think();
        }

        // 急にカクッと曲がらないように、選んだ方向へなめらかに寄せる
        float t = 1f - Mathf.Exp(-turnSpeed * Time.deltaTime);
        currentDir = Vector3.Slerp(currentDir, targetDir, t);
    }

    void FixedUpdate()
    {
        if (rb.isKinematic) return; // 物理LODで止められているときなど

        // 速度を直接上書きせず「目標速度との差」を力として加える
        // → 他のNPCやプレイヤーに押された分が自然に残る
        Vector3 targetVel;
        if (member != null && member.HasSlot)
        {
            // 隊形の席に入っている → リーダーの速度 + 席へのバネ
            // (速度ボーナスはリーダー側にかかっているので、ここでは掛けない)
            targetVel = member.DesiredVelocity;
            if (targetVel.sqrMagnitude > 1e-4f) currentDir = targetVel.normalized; // 外れたときに向きが飛ばないように
        }
        else if (IsStopped)
        {
            targetVel = Vector3.zero;
        }
        else
        {
            // 流れに乗っている分(speedScale) × レーンの大きさの分(laneMul)
            float laneMul = laneRider != null ? laneRider.SpeedMultiplier : 1f;
            targetVel = currentDir * (moveSpeed * desiredSpeedRate * speedScale * laneMul);
        }

        Vector3 v = rb.linearVelocity;
        Vector3 diff = targetVel - new Vector3(v.x, 0f, v.z);
        rb.AddForce(diff * acceleration, ForceMode.Acceleration);

        UpdateFacing(v);
    }

    // 体の向き:動いているときは進行方向、止まっているときは idleFacing
    void UpdateFacing(Vector3 velocity)
    {
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 face;
        if (horizontal.sqrMagnitude > faceMinSpeed * faceMinSpeed) face = horizontal.normalized;
        else if (hasIdleFacing) face = idleFacing;
        else return;

        Quaternion target = Quaternion.LookRotation(face);
        float t = 1f - Mathf.Exp(-bodyTurnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, t));
    }

    // dir 方向の前方の流れ(近い所と遠い所の平均。1点だけだとブレるので)
    Vector2 SampleAhead(Vector3 dir)
    {
        Vector3 p = transform.position;
        return (field.Sample(p + dir * (lookAhead * 0.5f)) +
                field.Sample(p + dir * lookAhead)) * 0.5f;
    }

    // 候補方向それぞれの前方の流れを見て、一番いい方向を選ぶ
    void Think()
    {
        Vector3 desired = desiredDir;
        if (field == null)
        {
            targetDir = desired;
            speedScale = 1f;
            return;
        }

        float maxStrength = Mathf.Max(field.MaxStrength, 0.0001f);

        // ---- 正面の逆流をチェックして、探索範囲を決める ----
        Vector2 front = SampleAhead(desired);
        float frontCounter = Mathf.Clamp01(-Vector2.Dot(front, ToV2(desired)) / maxStrength);
        // 足元で自分の向きに流れている強さ(仲間が多いほど強い)= 自信
        Vector2 hereFlow = field.Sample(transform.position);
        float support = Mathf.Clamp01(Vector2.Dot(hereFlow, ToV2(desired)) / maxStrength);
        debugSupport = support;
        // 正面の逆流から自信の分を差し引いたものが、実際に感じる圧力
        float pressure = Mathf.Max(0f, frontCounter - support * supportWeight);
        float danger = Mathf.Clamp01(pressure * counterSensitivity); // 0:平和 ～ 1:正面が強い逆流
        float angleRange = Mathf.Lerp(searchAngle, wideSearchAngle, danger);
        debugDanger = danger;

        // 正面に逆流がなければ、まっすぐ進む(候補は探さない)
        // → 自分や周りの人が書いた流れに引っ張られてくねくねするのを防ぐ
        if (danger < calmThreshold)
        {
            targetDir = desired;
            debugDirs = null; // Gizmoの候補表示を消す
            UpdateSpeedScale(maxStrength);
            return;
        }

        if (debugDirs == null || debugDirs.Length != candidateCount)
        {
            debugDirs = new Vector3[candidateCount];
            debugScores = new float[candidateCount];
        }

        // ---- 候補の採点 ----
        Vector3 desiredRight = Vector3.Cross(Vector3.up, desired);
        float bestScore = float.NegativeInfinity;
        Vector3 bestDir = desired;

        for (int i = 0; i < candidateCount; i++)
        {
            // -angleRange ～ +angleRange を均等に分ける
            float r = candidateCount == 1 ? 0.5f : i / (float)(candidateCount - 1);
            float angle = Mathf.Lerp(-angleRange, angleRange, r);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * desired;

            Vector2 flow = SampleAhead(dir);

            // その方向に対する流れ(-1:逆流 ～ 1:追い風)
            float align = Mathf.Clamp(Vector2.Dot(flow, ToV2(dir)) / maxStrength, -1f, 1f);
            float follow = Mathf.Max(0f, align);   // 乗れる分
            float counter = Mathf.Max(0f, -align);  // 逆らう分
            // ※ 流れを真横に横切る方向は align が 0 なのでペナルティなし
            //   → 逆流のレーンから横に避ける動きが選ばれやすい

            float goal = Vector3.Dot(dir, desired);  // 進みたい方向への近さ
            float keep = Vector3.Dot(dir, targetDir); // 今の方向を保つ
            float side = Vector3.Dot(dir, desiredRight); // 右に避ける方向ほど +

            float score = goal * goalWeight
                        + follow * flowWeight
                        - counter * counter * counterWeight
                        + keep * stickiness
                        + side * passSideBias * danger; // 逆流が強いときほど決まった側に避ける

            debugDirs[i] = dir;
            debugScores[i] = score;

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }
        }

        targetDir = bestDir;
        UpdateSpeedScale(maxStrength);
    }

    // 今いる場所の流れに乗っていれば速く、逆らっていれば遅く
    void UpdateSpeedScale(float maxStrength)
    {
        Vector2 here = field.Sample(transform.position);
        float hereAlign = Vector2.Dot(here, ToV2(targetDir)) / maxStrength;
        speedScale = 1f + Mathf.Clamp(hereAlign, -0.5f, 1f) * flowSpeedBonus;
    }

    // 選択中のNPCの候補方向を表示(緑ほどスコアが高い)
    void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position + Vector3.up * 0.1f;

        // 進みたい方向(青)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(p, p + desiredDir * lookAhead * 1.5f);

            // 自信(足元の自分の向きの流れ)。緑の円が大きいほど、逆流に強い
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(p + Vector3.up * 1.5f, 0.1f + 0.4f * debugSupport);
        }

        if (debugDirs == null) return;

        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (float s in debugScores) { min = Mathf.Min(min, s); max = Mathf.Max(max, s); }

        for (int i = 0; i < debugDirs.Length; i++)
        {
            float n = max > min ? (debugScores[i] - min) / (max - min) : 1f;
            Gizmos.color = Color.Lerp(Color.red, Color.green, n);
            Gizmos.DrawLine(p, p + debugDirs[i] * lookAhead);
        }

        // 実際に向かっている方向
        Gizmos.color = Color.white;
        Gizmos.DrawLine(p, p + currentDir * lookAhead * 1.2f);

        // 正面の危険度(黄色い円が大きいほど強い逆流が正面にある)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, 0.1f + 0.4f * debugDanger);
    }
}
