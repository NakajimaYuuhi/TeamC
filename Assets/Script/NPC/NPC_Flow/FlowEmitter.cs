using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 動いた方向に流れを作る。プレイヤーにもNPCにも付けられる。
///  - 足元:半径 radius の円に、動いた方向の流れを書く
///  - 前方:扇形(コーン)に流れを書く → 相手がぶつかる前に左右に道を空け始める
///  - 後方:帯に流れを書く → 開いた道が後ろですぐ閉じないように保つ
///  - 既にある流れに沿って動いていると増幅、逆らっていると弱まる
/// </summary>
public class FlowEmitter : MonoBehaviour
{
    [SerializeField] FlowField field;
    [SerializeField] float radius = 2f;
    [SerializeField] float gain = 3f;

    [Header("増幅")]
    [Tooltip("流れに完全に沿っているとき、書き込みが (1 + amplify) 倍になる")]
    [SerializeField, Min(0f)] float amplify = 2f;

    [Header("逆流")]
    [Tooltip("逆流にどれだけ敏感に書き込みを止めるか。3なら、最大の1/3程度の逆流で書き込み0")]
    [SerializeField, Min(0f)] float counterSensitivity = 3f;
    [Tooltip("オン:逆流の中でもコーンと帯を弱めない。人混みに突っ込むプレイヤー向け。NPCはオフ推奨")]
    [FormerlySerializedAs("coneIgnoresCounterFlow")]
    [SerializeField] bool shapeIgnoresCounterFlow = false;
    [Tooltip("コーンと帯の向きのなめらかさ。小さいほどゆっくり向きを変える")]
    [FormerlySerializedAs("coneTurnSpeed")]
    [SerializeField, Min(0.1f)] float shapeTurnSpeed = 8f;

    [Header("前方コーン")]
    [SerializeField] bool coneEnabled = true;
    [Tooltip("コーンの長さ(m)。長いほど遠くの人が早めに避け始める")]
    [SerializeField, Min(0f)] float coneLength = 6f;
    [Tooltip("中心線から片側の角度。大きいほど広く道を開ける")]
    [SerializeField, Range(5f, 80f)] float coneHalfAngle = 35f;
    [Tooltip("流れの向き。0 = まっすぐ前 / 1 = 外側に広がる(左右に割れやすい)")]
    [SerializeField, Range(0f, 1f)] float coneSpread = 0.5f;
    [Tooltip("コーンの強さ(足元の gain と同じ単位)")]
    [SerializeField, Min(0f)] float coneGain = 2f;
    [Tooltip("コーンの頂点の位置。マイナスで自分より後ろに置く → 自分の位置で既に幅がある")]
    [SerializeField] float coneApexOffset = -0.5f;

    [Header("後方の帯")]
    [SerializeField] bool bandEnabled = true;
    [Tooltip("帯の長さ(m)。この長さぶん、後ろの道が保たれる")]
    [SerializeField, Min(0f)] float bandLength = 5f;
    [Tooltip("帯の片側の幅(m)。グループの幅の半分くらいに合わせる")]
    [SerializeField, Min(0.1f)] float bandHalfWidth = 1.5f;
    [Tooltip("帯の強さ(足元の gain と同じ単位)")]
    [SerializeField, Min(0f)] float bandGain = 1.5f;

    [Header("Burst(回避成功などで一瞬強く道を開ける)")]
    [Tooltip("Burst の強さ(1秒あたり)。足元・コーンの書き込みは だいたい 速さ×gain ≒ 3 なので、それより十分大きく")]
    [SerializeField, Min(0f)] float burstStrength = 25f;
    [Tooltip("Burst を何秒続けるか(Burst の引数を省略したとき)")]
    [SerializeField, Min(0f)] float burstDuration = 0.5f;
    [Tooltip("Burst のコーンの長さ(m)")]
    [SerializeField, Min(0f)] float burstLength = 8f;
    [Tooltip("Burst のコーンの中心線から片側の角度")]
    [SerializeField, Range(5f, 80f)] float burstHalfAngle = 25f;
    [Tooltip("Burst の流れの外向き成分(左右に割れやすくする)")]
    [SerializeField, Range(0f, 1f)] float burstSpread = 0.6f;
    [Tooltip("Burst のコーンの頂点の位置(マイナスで自分より後ろ)")]
    [SerializeField] float burstApexOffset = -0.5f;
    [Tooltip("オン:時間とともに弱くなる / オフ:最後まで同じ強さ")]
    [SerializeField] bool burstFadeOut = true;

    [Header("Debug")]
    [SerializeField] bool showDebug = false;

    /// <summary>コーンの長さに掛ける倍率。グループの大きさで伸ばす、などを外から入れる用</summary>
    public float ConeScale { get; set; } = 1f;
    /// <summary>帯の幅に掛ける倍率。隊形の幅に合わせる、などを外から入れる用</summary>
    public float BandWidthScale { get; set; } = 1f;
    /// <summary>帯の長さに掛ける倍率。隊列の長さに合わせる、などを外から入れる用</summary>
    public float BandLengthScale { get; set; } = 1f;
    /// <summary>インスペクタで設定した帯の片側の幅(倍率を掛ける前)</summary>
    public float BaseBandHalfWidth => bandHalfWidth;
    /// <summary>インスペクタで設定した帯の長さ(倍率を掛ける前)</summary>
    public float BaseBandLength => bandLength;

    Vector3 prevPos;
    Vector2 burstDir;
    float burstTimer;
    float burstTotal;
    Vector2 shapeDir;     // なめらかにした向き(コーンと帯で共通)
    float lastAlign;
    float lastScale = 1f;

    void Start()
    {
        if (field == null) field = FindAnyObjectByType<FlowField>();
        prevPos = transform.position;
    }

    /// <summary>
    /// 一瞬だけ強い流れを書いて、その向きに道を開ける(回避成功の演出など)。
    /// worldDir:開けたい向き(Yは無視)。duration:秒数(省略 or 0以下ならインスペクタの burstDuration)。
    /// 時間ベースで書くので、止まっていても書かれる。続けて呼ぶと、向きと時間が上書きされる。
    /// </summary>
    public void Burst(Vector3 worldDir, float duration = -1f)
    {
        Vector2 d = new Vector2(worldDir.x, worldDir.z);
        if (d.sqrMagnitude < 1e-6f) return;
        burstDir = d.normalized;
        burstTotal = duration > 0f ? duration : burstDuration;
        burstTimer = burstTotal;
    }

    /// <summary>Burst 中か</summary>
    public bool IsBursting => burstTimer > 0f;

    void UpdateBurst()
    {
        if (burstTimer <= 0f || field == null) return;

        float rate = burstFadeOut && burstTotal > 0f ? burstTimer / burstTotal : 1f;
        burstTimer -= Time.deltaTime;

        Vector3 fwd3 = new Vector3(burstDir.x, 0f, burstDir.y);
        Vector3 apex = transform.position + fwd3 * burstApexOffset;
        field.AddFlowCone(apex, burstDir, burstLength, burstHalfAngle, burstSpread,
                          burstStrength * rate * Time.deltaTime);
    }

    void Update()
    {
        UpdateBurst();

        Vector3 delta = transform.position - prevPos;
        prevPos = transform.position;

        Vector2 move = new Vector2(delta.x, delta.z);   // 1フレームの移動量
        if (move.sqrMagnitude < 1e-6f || field == null) return;

        Vector2 moveDir = move.normalized;
        float moveLen = move.magnitude;

        // 今いる場所の流れと、自分の動きがどれだけ揃っているか(-1?1)
        Vector2 here = field.Sample(transform.position);
        float align = Vector2.Dot(here, moveDir) / Mathf.Max(field.MaxStrength, 0.0001f);

        float scale;
        if (align >= 0f)
        {
            // 沿っている → 増幅
            scale = 1f + amplify * align;
        }
        else
        {
            // 逆らっている → 弱める(強い逆流ほど0に近づく)
            scale = 1f - Mathf.Clamp01(-align * counterSensitivity);
        }

        lastAlign = align;
        lastScale = scale;

        // ---- 足元の円 ----
        // 移動量ベースなのでフレームレートが変わっても総量はほぼ同じ
        if (scale > 0f)
            field.AddFlow(transform.position, move * (gain * scale), radius);

        // ---- コーンと帯の向き ----
        // 毎フレームの小さなブレで首を振らないように、なめらかにする
        if (shapeDir == Vector2.zero) shapeDir = moveDir;
        float t = 1f - Mathf.Exp(-shapeTurnSpeed * Time.deltaTime);
        shapeDir = Vector2.Lerp(shapeDir, moveDir, t).normalized;

        float shapeScale = (shapeIgnoresCounterFlow && align < 0f) ? 1f : scale;
        if (shapeScale <= 0f) return;

        Vector3 fwd3 = new Vector3(shapeDir.x, 0f, shapeDir.y);

        // ---- 前方のコーン ----
        if (coneEnabled)
        {
            Vector3 apex = transform.position + fwd3 * coneApexOffset;
            field.AddFlowCone(apex, shapeDir, coneLength * ConeScale, coneHalfAngle, coneSpread,
                              moveLen * coneGain * shapeScale);
        }

        // ---- 後方の帯 ----
        if (bandEnabled)
        {
            field.AddFlowBand(transform.position, shapeDir, bandLength * BandLengthScale, bandHalfWidth * BandWidthScale,
                              moveLen * bandGain * shapeScale);
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // 緑 = 増幅中、赤 = 逆流で抑制中。円の大きさ = 書き込みの強さ
        Gizmos.color = lastAlign >= 0f ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f,
                              0.2f + 0.15f * lastScale);

        if (shapeDir == Vector2.zero) return;
        Vector3 fwd = new Vector3(shapeDir.x, 0f, shapeDir.y);
        Vector3 up = Vector3.up * 0.1f;

        // コーンの範囲(水色)
        if (coneEnabled)
        {
            Vector3 apex = transform.position + fwd * coneApexOffset + up;
            float len = coneLength * ConeScale;
            Vector3 left = Quaternion.Euler(0f, -coneHalfAngle, 0f) * fwd * len;
            Vector3 right = Quaternion.Euler(0f, coneHalfAngle, 0f) * fwd * len;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(apex, apex + left);
            Gizmos.DrawLine(apex, apex + right);

            const int seg = 12;
            Vector3 prev = apex + left;
            for (int i = 1; i <= seg; i++)
            {
                float ang = Mathf.Lerp(-coneHalfAngle, coneHalfAngle, i / (float)seg);
                Vector3 p = apex + Quaternion.Euler(0f, ang, 0f) * fwd * len;
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }

        // 帯の範囲(黄色)
        if (bandEnabled)
        {
            Vector3 side = new Vector3(-fwd.z, 0f, fwd.x) * (bandHalfWidth * BandWidthScale);
            Vector3 s = transform.position + up;
            Vector3 e = s - fwd * (bandLength * BandLengthScale);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(s + side, e + side);
            Gizmos.DrawLine(s - side, e - side);
            Gizmos.DrawLine(e + side, e - side);
        }
    }

    public void SetField(FlowField flowField)
    {
        field = flowField;
    }
}
