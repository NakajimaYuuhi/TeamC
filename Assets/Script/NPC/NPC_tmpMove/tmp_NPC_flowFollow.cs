using UnityEngine;

// 進みたい方向を基準に、前方の「流れの良いところ」を選んで進む
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
    [SerializeField, Min(1)] int candidateCount = 7;
    // 進みたい方向から左右何度まで調べるか
    [SerializeField, Range(0f, 90f)] float searchAngle = 60f;
    // どれくらい先の流れを見るか(m)
    [SerializeField] float lookAhead = 1.5f;
    // 「進みたい方向に近いこと」の重み
    [SerializeField] float goalWeight = 1f;
    // 「流れが自分の向きと揃っていること」の重み
    [SerializeField] float flowWeight = 1f;
    // 流れに乗っているときの速度ボーナス(逆流だと遅くなる)
    [SerializeField, Range(0f, 1f)] float flowSpeedBonus = 0.5f;
    // 何秒ごとに方向を考え直すか
    [SerializeField] float thinkInterval = 0.2f;

    [Header("動き")]
    // 方向転換のなめらかさ(大きいほど素早く曲がる)
    [SerializeField] float turnSpeed = 5f;
    // 目標速度に近づく強さ
    [SerializeField] float acceleration = 10f;

    Rigidbody rb;
    Vector3 targetDir;      // Thinkで選んだ方向
    Vector3 currentDir;     // 実際に向かっている方向(targetDirへ徐々に寄せる)
    float speedScale = 1f;
    float thinkTimer;

    // デバッグ表示用
    Vector3[] debugDirs;
    float[] debugScores;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (field == null) field = FindAnyObjectByType<FlowField>();

        targetDir = currentDir = DesiredDir();

        // NPCごとに考えるタイミングをずらす(同じフレームに集中しないように)
        thinkTimer = Random.Range(0f, thinkInterval);
    }

    Vector3 DesiredDir()
    {
        return new Vector3(moveDirection.x, 0, moveDirection.y).normalized;
    }

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
        Vector3 targetVel = currentDir * (moveSpeed * speedScale);
        Vector3 v = rb.linearVelocity;
        Vector3 diff = targetVel - new Vector3(v.x, 0f, v.z);
        rb.AddForce(diff * acceleration, ForceMode.Acceleration);
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

        if (debugDirs == null || debugDirs.Length != candidateCount)
        {
            debugDirs = new Vector3[candidateCount];
            debugScores = new float[candidateCount];
        }

        float bestScore = float.NegativeInfinity;
        Vector3 bestDir = desired;

        for (int i = 0; i < candidateCount; i++)
        {
            // -searchAngle ? +searchAngle を均等に分ける
            float r = candidateCount == 1 ? 0.5f : i / (float)(candidateCount - 1);
            float angle = Mathf.Lerp(-searchAngle, searchAngle, r);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * desired;

            // その方向の少し先の流れ
            Vector2 flow = field.Sample(transform.position + dir * lookAhead);

            // 流れがその方向と揃っているか(-1:逆流 ? 1:最大の追い風)
            float align = Vector2.Dot(flow, new Vector2(dir.x, dir.z)) / maxStrength;

            // 進みたい方向にどれだけ近いか(正面=1)
            float goal = Vector3.Dot(dir, desired);

            float score = goal * goalWeight + align * flowWeight;

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
        float hereAlign = Vector2.Dot(here, new Vector2(targetDir.x, targetDir.z)) / maxStrength;
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

        Gizmos.color = Color.white;
        Gizmos.DrawLine(p, p + currentDir * lookAhead * 1.2f);
    }
}
