using UnityEngine;

/// <summary>
/// 隊形の「圧」(プレイヤーに付ける。FormationLeader と同じオブジェクト)。
/// 仲間が多いほど、隊形の前方に強いコーン状の流れを書く。
///  - 向こうから来る人は、先頭のプレイヤー1人ではなく「大群が来る」と感じて早めに道を空ける
///  - 動いた量ではなく時間で書くので、押し合いで止まっていても前に進もうとしている限り圧が出る
///  - 仲間が少ないうちは何もしない(プレイヤー1人の強さは FlowEmitter だけで決まる)
/// → 「仲間を集めるほど人混みが割れやすくなる」ご褒美
/// </summary>
[RequireComponent(typeof(FormationLeader))]
public class FormationPressure : MonoBehaviour
{
    [SerializeField] FlowField field;

    [Header("人数")]
    [Tooltip("この人数から圧が出始める")]
    [SerializeField, Min(0)] int minMembers = 3;
    [Tooltip("この人数で圧が最大")]
    [SerializeField, Min(1)] int fullMembers = 15;

    [Header("コーン")]
    [Tooltip("最大のときの強さ(1秒あたり)。プレイヤーの FlowEmitter のコーンは だいたい 速さ×coneGain ≒ 3")]
    [SerializeField, Min(0f)] float maxStrength = 6f;
    [Tooltip("コーンの長さ(m)。人数が少ないときは minLength、最大で maxLength")]
    [SerializeField, Min(0f)] float minLength = 4f;
    [SerializeField, Min(0f)] float maxLength = 8f;
    [Tooltip("中心線から片側の角度")]
    [SerializeField, Range(5f, 80f)] float halfAngle = 30f;
    [Tooltip("流れの外向き成分(左右に割れやすくする)")]
    [SerializeField, Range(0f, 1f)] float spread = 0.5f;
    [Tooltip("コーンの頂点の位置(マイナスで自分より後ろ)")]
    [SerializeField] float apexOffset = 0f;

    [Header("Debug")]
    [SerializeField] bool showDebug = true;

    FormationLeader leader;
    float lastRate;   // 0～1(デバッグ表示用)

    void Awake()
    {
        leader = GetComponent<FormationLeader>();
    }

    void Start()
    {
        if (field == null) field = FindAnyObjectByType<FlowField>();
    }

    /// <summary>今の圧の割合(0～1)。演出などに</summary>
    public float Rate => lastRate;

    void Update()
    {
        lastRate = 0f;
        if (field == null || leader == null) return;
        if (!leader.IsAdvancing) return;

        int count = leader.MemberCount;
        if (count < minMembers) return;

        float rate = Mathf.Clamp01((count - minMembers + 1) / (float)Mathf.Max(1, fullMembers - minMembers + 1));
        lastRate = rate;

        Vector3 fwd = leader.Forward;
        Vector2 dir = new Vector2(fwd.x, fwd.z);
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();

        Vector3 apex = transform.position + fwd * apexOffset;
        float length = Mathf.Lerp(minLength, maxLength, rate);
        float amount = maxStrength * rate * Time.deltaTime;

        field.AddFlowCone(apex, dir, length, halfAngle, spread, amount);
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || lastRate <= 0f || leader == null) return;

        Vector3 fwd = leader.Forward;
        Vector3 apex = transform.position + fwd * apexOffset + Vector3.up * 0.1f;
        float length = Mathf.Lerp(minLength, maxLength, lastRate);
        Vector3 l = Quaternion.Euler(0f, -halfAngle, 0f) * fwd * length;
        Vector3 r = Quaternion.Euler(0f, halfAngle, 0f) * fwd * length;

        Gizmos.color = Color.Lerp(Color.yellow, Color.red, lastRate);
        Gizmos.DrawLine(apex, apex + l);
        Gizmos.DrawLine(apex, apex + r);
        Gizmos.DrawLine(apex + l, apex + r);
    }
}
