using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 自分がいるレーンの大きさから速度倍率を出す(ゲームプレイ層)。
/// 移動処理はしない。NPCやプレイヤーの移動スクリプトが SpeedMultiplier を読んで使う。
/// </summary>
public class FlowLaneRider : MonoBehaviour
{
    [SerializeField] FlowLaneAnalyzer analyzer;

    [Header("レーンの大きさ → ボーナス")]
    [Tooltip("このセル数からボーナスが出始める")]
    [SerializeField, Min(1)] int smallLaneCells = 10;
    [Tooltip("このセル数でボーナス最大")]
    [SerializeField, Min(2)] int bigLaneCells = 200;
    [Tooltip("最大で (1 + maxBonus) 倍")]
    [SerializeField, Range(0f, 2f)] float maxBonus = 0.5f;

    [Header("向き")]
    [Tooltip("レーンの向きとの揃い具合(dot)がこれ以下ならボーナスなし。0.5 ≒ 60°以内")]
    [SerializeField, Range(-1f, 1f)] float minAlign = 0.5f;

    [Header("なめらかさ")]
    [Tooltip("倍率が目標値に近づく速さ(解析は0.5秒ごとなので、段差をならす)")]
    [SerializeField] float smoothing = 4f;

    [Header("Debug")]
    [SerializeField] bool showDebug = false;

    /// <summary>移動速度に掛ける倍率(1 = ボーナスなし)</summary>
    public float SpeedMultiplier { get; private set; } = 1f;
    /// <summary>今いるレーン(いなければ null)</summary>
    public FlowLaneAnalyzer.Lane CurrentLane { get; private set; }
    /// <summary>レーンの大きさ 0~1(UIや演出用)</summary>
    public float LaneRatio { get; private set; }

    Vector3 prevPos;
    Vector2 moveDir;

    void Start()
    {
        if (analyzer == null) analyzer = FindAnyObjectByType<FlowLaneAnalyzer>();
        prevPos = transform.position;
    }

    void Update()
    {
        // 自分の進んでいる向き(止まっているときは前回の向きを使う)
        Vector3 d = transform.position - prevPos;
        prevPos = transform.position;
        Vector2 m = new Vector2(d.x, d.z);
        if (m.sqrMagnitude > 1e-6f) moveDir = m.normalized;

        float target = 1f;
        LaneRatio = 0f;
        CurrentLane = analyzer != null ? analyzer.GetLaneAt(transform.position) : null;

        if (CurrentLane != null && moveDir != Vector2.zero)
        {
            // レーンの向きにどれだけ沿っているか(0?1)
            float align = Vector2.Dot(moveDir, CurrentLane.avgDir);
            float alignT = Mathf.InverseLerp(minAlign, 1f, align);

            // レーンの大きさ(対数で 0?1 に。小さいレーンの差は大きく、大きいレーン同士の差は小さく)
            float size = Mathf.Max(1, CurrentLane.cellCount);
            float sizeT = Mathf.InverseLerp(Mathf.Log(smallLaneCells), Mathf.Log(bigLaneCells), Mathf.Log(size));

            LaneRatio = sizeT;
            target = 1f + maxBonus * sizeT * alignT;
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        SpeedMultiplier = Mathf.Lerp(SpeedMultiplier, target, t);
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!showDebug || !Application.isPlaying) return;
        string lane = CurrentLane != null ? $"#{CurrentLane.id}" : "-";
        Handles.Label(transform.position + Vector3.up * 2f, $"{lane}  x{SpeedMultiplier:F2}");
#endif
    }
}
