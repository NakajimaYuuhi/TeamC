using UnityEngine;

// 進みたい方向を基準に、前方の「流れの良いところ」を選んで進む
// 正面に強い逆流があれば、探す範囲を広げて横に避ける
[RequireComponent(typeof(Rigidbody))]
public class tmp_NPC_flowFollow : MonoBehaviour
{
    [Header("目的")]
    // 進みたい方向(あとでゴール座標から計算する形に差し替える想定)
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
    // 「流れに乗れること」の重み
    [SerializeField] float flowWeight = 1f;
    // 流れに乗っているときの速度ボーナス(逆流だと遅くなる)
    [SerializeField, Range(0f, 1f)] float flowSpeedBonus = 0.5f;
    // 何秒ごとに方向を考え直すか
    [SerializeField] float thinkInterval = 0.2f;

    [Header("逆流を避ける")]
    // 正面の逆流が強いとき、左右何度まで探す範囲を広げるか
    [SerializeField, Range(0f, 120f)] float wideSearchAngle = 90f;
    // 正面の逆流にどれだけ敏感に探す範囲を広げるか
    [SerializeField, Min(0f)] float counterSensitivity = 2f;
    // 逆流のペナルティ(2乗で効くので、弱い逆流はほぼ無視・強い逆流だけ強く避ける)
    [SerializeField, Min(0f)] float counterWeight = 3f;
    // 今の方向を保とうとする強さ(左右に迷ってフラフラするのを防ぐ)
    [SerializeField, Min(0f)] float stickiness = 0.2f;

    [Header("動き")]
    // 方向転換のなめらかさ(大きいほど素早く曲がる)
    [SerializeField] float turnSpeed = 5f;
    // 目標速度に近づく強さ
    [SerializeField] float acceleration = 10f;

    Rigidbody rb;
    FlowLaneRider laneRider;   // 付いていればレーンの速度ボーナスを使う(なくても動く)
    FormationMember member;    // 付いていて席に入っていれば、隊形の速度で動く(なくても動く)
    Vector3 targetDir;      // Thinkで選んだ方向
    Vector3 currentDir;     // 実際に向かっている方向(targetDirへ徐々に寄せる)
    float speedScale = 1f;
    float thinkTimer;

    // デバッグ表示用
    Vector3[] debugDirs;
    float[] debugScores;
    float debugDanger;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        laneRider = GetComponent<FlowLaneRider>();
        member = GetComponent<FormationMember>();
        if (field == null) field = FindAnyObjectByType<FlowField>();

        targetDir = currentDir = DesiredDir();

        // NPCごとに考えるタイミングをずらす(同じフレームに集中しないように)
        thinkTimer = Random.Range(0f, thinkInterval);
    }

    Vector3 DesiredDir()
    {
        return new Vector3(moveDirection.x, 0, moveDirection.y).normalized;
    }

    static Vector2 ToV2(Vector3 v) => new Vector2(v.x, v.z);

    void Update()
    {
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
        // 速度を直接上書きせず「目標速度との差」を力として加える
        // → 他のNPCやプレイヤーに押された分が消えずに残る
        Vector3 targetVel;
        if (member != null && member.HasSlot)
        {
            // 隊形の席に入っている → リーダーの速度 + 席へのバネ
            // (速度ボーナスはリーダー側にかかっているので、ここでは掛けない)
            targetVel = member.DesiredVelocity;
            if (targetVel.sqrMagnitude > 1e-4f) currentDir = targetVel.normalized; // 外れたときに向きが飛ばないように
        }
        else
        {
            // 流れに乗っている分(speedScale) × レーンの大きさの分(laneMul)
            float laneMul = laneRider != null ? laneRider.SpeedMultiplier : 1f;
            targetVel = currentDir * (moveSpeed * speedScale * laneMul);
        }
        Vector3 v = rb.linearVelocity;
        Vector3 diff = targetVel - new Vector3(v.x, 0f, v.z);
        rb.AddForce(diff * acceleration, ForceMode.Acceleration);
    }

    // dir 方向の前方の流れ(近い所と遠い所の平均。1点だけよりブレにくい)
    Vector2 SampleAhead(Vector3 dir)
    {
        Vector3 p = transform.position;
        return (field.Sample(p + dir * (lookAhead * 0.5f)) +
                field.Sample(p + dir * lookAhead)) * 0.5f;
    }

    // 候補方向それぞれの前方の流れを見て、一番いい方向を選ぶ
    void Think()
    {
        Vector3 desired = DesiredDir();
        if (field == null)
        {
            targetDir = desired;
            return;
        }

        float maxStrength = Mathf.Max(field.MaxStrength, 0.0001f);

        // ---- 正面の逆流をチェックして、探す範囲を決める ----
        Vector2 front = SampleAhead(desired);
        float frontCounter = Mathf.Clamp01(-Vector2.Dot(front, ToV2(desired)) / maxStrength);
        float danger = Mathf.Clamp01(frontCounter * counterSensitivity); // 0:平和 ? 1:正面が強い逆流
        float angleRange = Mathf.Lerp(searchAngle, wideSearchAngle, danger);
        debugDanger = danger;

        if (debugDirs == null || debugDirs.Length != candidateCount)
        {
            debugDirs = new Vector3[candidateCount];
            debugScores = new float[candidateCount];
        }

        // ---- 候補を採点 ----
        float bestScore = float.NegativeInfinity;
        Vector3 bestDir = desired;

        for (int i = 0; i < candidateCount; i++)
        {
            // -angleRange ? +angleRange を均等に分ける
            float r = candidateCount == 1 ? 0.5f : i / (float)(candidateCount - 1);
            float angle = Mathf.Lerp(-angleRange, angleRange, r);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * desired;

            Vector2 flow = SampleAhead(dir);

            // その方向に対する流れ(-1:逆流 ? 1:追い風)
            float align = Mathf.Clamp(Vector2.Dot(flow, ToV2(dir)) / maxStrength, -1f, 1f);
            float follow = Mathf.Max(0f, align);   // 乗れる分
            float counter = Mathf.Max(0f, -align);  // 逆らう分
            // ※ 流れを真横に横切る方向は align ≒ 0 なのでペナルティなし
            //   → 逆流のレーンから横に抜ける動きが選ばれやすい

            float goal = Vector3.Dot(dir, desired);  // 進みたい方向への近さ
            float keep = Vector3.Dot(dir, targetDir); // 今の方向を保つ

            float score = goal * goalWeight
                        + follow * flowWeight
                        - counter * counter * counterWeight
                        + keep * stickiness;

            debugDirs[i] = dir;
            debugScores[i] = score;

            if (score > bestScore)
            {
                bestScore = score;
                bestDir = dir;
            }
        }

        targetDir = bestDir;

        // 今いる場所の流れに乗っていれば速く、逆らっていれば遅く
        Vector2 here = field.Sample(transform.position);
        float hereAlign = Vector2.Dot(here, ToV2(targetDir)) / maxStrength;
        speedScale = 1f + Mathf.Clamp(hereAlign, -0.5f, 1f) * flowSpeedBonus;
    }

    // 選択したNPCの候補方向を表示(緑ほどスコアが高い)
    void OnDrawGizmosSelected()
    {
        if (debugDirs == null) return;

        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        foreach (float s in debugScores) { min = Mathf.Min(min, s); max = Mathf.Max(max, s); }

        Vector3 p = transform.position + Vector3.up * 0.1f;
        for (int i = 0; i < debugDirs.Length; i++)
        {
            float n = max > min ? (debugScores[i] - min) / (max - min) : 1f;
            Gizmos.color = Color.Lerp(Color.red, Color.green, n);
            Gizmos.DrawLine(p, p + debugDirs[i] * lookAhead);
        }

        // 実際に向かっている方向
        Gizmos.color = Color.white;
        Gizmos.DrawLine(p, p + currentDir * lookAhead * 1.2f);

        // 正面の危険度(黄色い円が大きいほど強い逆流を感じている)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p, 0.1f + 0.4f * debugDanger);
    }
}
