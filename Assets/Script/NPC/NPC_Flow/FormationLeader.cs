using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 隊形の中心(プレイヤーに付ける)。
/// 自分の後ろにくさび形(V字)の「席」を並べて、FormationMember に席を貸し出す。
///  - 席は六角形の詰め方(蜂の巣状)
///  - 隊形の向きは、なめらかにした移動方向に合わせる
///  - 壁にめり込んでいる席は使わない(狭い所では隊形が細く縦長になる)
/// </summary>
public class FormationLeader : MonoBehaviour
{
    [Header("隊形")]
    [Tooltip("くさびの列数。列 r には r+1 個の席が並ぶ")]
    [SerializeField, Min(1)] int maxRows = 6;
    [Tooltip("席の間隔(m)。NPCのコライダーの直径より少し大きく")]
    [SerializeField, Min(0.3f)] float spacing = 1.1f;
    [Tooltip("1列目をさらに後ろにずらす(m)")]
    [SerializeField, Min(0f)] float firstRowOffset = 0f;
    [Tooltip("隊形に入れる最大人数。あふれた人は普通に流れについていく")]
    [SerializeField, Min(1)] int maxMembers = 20;
    [Tooltip("席を選ぶときに前の列をどれだけ優先するか(大きいほど前から埋まる)")]
    [SerializeField, Min(0f)] float frontBias = 0.5f;

    [Header("席の有効チェック")]
    [Tooltip("壁などのレイヤー。Nothing ならチェックしない")]
    [SerializeField] LayerMask obstacleMask;
    [SerializeField, Min(0.05f)] float checkRadius = 0.4f;
    [SerializeField] float checkHeight = 1f;
    [SerializeField, Min(0.02f)] float validateInterval = 0.1f;

    [Header("向き")]
    [Tooltip("隊形が向きを変える速さ。小さいほど曲がるときに外側が振られにくい")]
    [SerializeField, Min(0.1f)] float turnSpeed = 4f;
    [Tooltip("これより遅いときは向きを更新しない(止まったときに隊形がくるくる回らないように)")]
    [SerializeField, Min(0f)] float minMoveSpeed = 0.2f;
    [Tooltip("今の向きから大きく逆向き(この dot 未満)への動きは「押し返された」とみなす")]
    [SerializeField, Range(-1f, 0f)] float reverseDot = -0.2f;
    [Tooltip("逆向きの動きがこの秒数続いたら、本当に振り返ったとみなして向きを変える。それまでは向きを変えない")]
    [SerializeField, Min(0f)] float reverseHoldTime = 0.6f;
    [Tooltip("SetIntendedDirection がこの秒数呼ばれなかったら、動いた向きで決める方式に戻る")]
    [SerializeField, Min(0f)] float intendedTimeout = 0.2f;

    [Header("参加条件")]
    [Tooltip("リーダーからこの距離以内の人だけが参加できる")]
    [SerializeField, Min(0f)] float joinRadius = 4f;
    [Tooltip("参加する人の進む向きと、隊形の向きの揃い具合(dot)がこれ以上")]
    [SerializeField, Range(-1f, 1f)] float joinAlign = 0.5f;
    [Tooltip("あれば、リーダーと同じレーンにいる人だけが参加できる")]
    [SerializeField] FlowLaneAnalyzer analyzer;
    [SerializeField] bool requireSameLane = true;

    [Header("Debug")]
    [SerializeField] bool showDebug = true;

    class Slot
    {
        public Vector3 local;           // リーダーから見た位置(z がマイナス = 後ろ)
        public int row;
        public bool valid = true;
        public FormationMember member;  // 座っている人(空なら null)
    }

    readonly List<Slot> slots = new List<Slot>();
    readonly List<FormationMember> lostMembers = new List<FormationMember>();
    Vector3 prevPos;
    float validateTimer;
    float reverseTimer;
    Vector3 intendedDir;       // プレイヤーの入力の向き(zero = 入力なし)
    float intendedTimer;       // > 0 の間は入力の向きで隊形の向きを決める

    /// <summary>なめらかにしたリーダーの速度(メンバーはこれに合わせて動く)</summary>
    public Vector3 Velocity { get; private set; }
    /// <summary>隊形の向き</summary>
    public Vector3 Forward { get; private set; } = Vector3.forward;
    public int MemberCount { get; private set; }

    // ------------------------------------------------------------
    void Awake()
    {
        BuildSlots();
        prevPos = transform.position;

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude > 1e-4f) Forward = f.normalized;

        if (analyzer == null) analyzer = FindAnyObjectByType<FlowLaneAnalyzer>();
    }

    /// <summary>くさび形の席を作る。列 r(1?maxRows)に r+1 個</summary>
    void BuildSlots()
    {
        slots.Clear();
        float rowSpacing = spacing * 0.866f;   // 六角形の詰め方の列の間隔(√3/2)

        for (int r = 1; r <= maxRows; r++)
        {
            for (int i = 0; i <= r; i++)
            {
                float x = (i - r * 0.5f) * spacing;
                float z = -(r * rowSpacing + firstRowOffset);
                slots.Add(new Slot { local = new Vector3(x, 0f, z), row = r });
            }
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ---- 速度と向き ----
        Vector3 v = (transform.position - prevPos) / dt;
        v.y = 0f;
        prevPos = transform.position;

        // フレームごとのブレをならす
        Velocity = Vector3.Lerp(Velocity, v, 1f - Mathf.Exp(-10f * dt));

        intendedTimer -= dt;
        if (intendedTimer > 0f)
        {
            // 入力の向きをもらえているとき:隊形の向きは入力だけで決める
            // (人混みに押し流されても、前に進もうとしている限り隊形は前を向く)
            reverseTimer = 0f;
            if (intendedDir != Vector3.zero)
            {
                float t = 1f - Mathf.Exp(-turnSpeed * dt);
                Forward = Vector3.Slerp(Forward, intendedDir, t).normalized;
            }
        }
        else if (Velocity.magnitude > minMoveSpeed)
        {
            // 今の向きと大きく逆向きの動きは、人混みに押し返されただけかもしれない。
            // しばらく続いたとき(自分で振り返ったとき)だけ向きを変える。
            // → 押し返された瞬間に隊形が後ろを向いて、向こうから来た人が仲間に入るのを防ぐ
            bool reversing = Vector3.Dot(Velocity.normalized, Forward) < reverseDot;
            reverseTimer = reversing ? reverseTimer + dt : 0f;

            if (!reversing || reverseTimer > reverseHoldTime)
            {
                float t = 1f - Mathf.Exp(-turnSpeed * dt);
                Forward = Vector3.Slerp(Forward, Velocity.normalized, t).normalized;
            }
        }
        else
        {
            reverseTimer = 0f;
        }

        // ---- 席が壁にめり込んでないか ----
        validateTimer -= dt;
        if (validateTimer <= 0f)
        {
            validateTimer += validateInterval;
            ValidateSlots();
        }
    }

    /// <summary>
    /// プレイヤーの移動スクリプトから毎フレーム呼ぶ:入力の向き(ワールド座標。Yは無視)。
    /// 入力がないときは Vector3.zero を渡す(隊形の向きはそのまま)。
    /// 呼ばれている間は、実際に動いた向きではなくこの向きで隊形の向きを決める。
    /// </summary>
    public void SetIntendedDirection(Vector3 worldDir)
    {
        worldDir.y = 0f;
        intendedDir = worldDir.sqrMagnitude > 1e-4f ? worldDir.normalized : Vector3.zero;
        intendedTimer = intendedTimeout;
    }

    // ------------------------------------------------------------
    // 席の位置
    // ------------------------------------------------------------
    public Vector3 GetSlotPosition(int index)
    {
        return transform.position + Quaternion.LookRotation(Forward) * slots[index].local;
    }

    void ValidateSlots()
    {
        lostMembers.Clear();

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            if (obstacleMask.value == 0)
            {
                s.valid = true;
                continue;
            }

            Vector3 p = GetSlotPosition(i) + Vector3.up * checkHeight;
            s.valid = !Physics.CheckSphere(p, checkRadius, obstacleMask, QueryTriggerInteraction.Ignore);

            // 座っていた席が使えなくなった → 一旦外して、あとで別の席を探す
            if (!s.valid && s.member != null)
            {
                lostMembers.Add(s.member);
                s.member = null;
                MemberCount--;
            }
        }

        // 追い出された人に別の席を探す。なければ隊形から外れる
        foreach (var m in lostMembers)
        {
            int idx = FindBestSlot(m.transform.position);
            if (idx >= 0)
            {
                slots[idx].member = m;
                MemberCount++;
                m.OnSlotChanged(idx);
            }
            else
            {
                m.OnSlotChanged(-1);
            }
        }
    }

    // ------------------------------------------------------------
    // メンバーから呼ばれるもの
    // ------------------------------------------------------------

    /// <summary>この人が隊形に参加できるか</summary>
    public bool CanJoin(Vector3 pos, Vector2 moveDir)
    {
        if (MemberCount >= maxMembers) return false;

        Vector3 d = pos - transform.position;
        d.y = 0f;
        if (d.magnitude > joinRadius) return false;

        // 同じ向きに進んでいる人だけ(逆向きの人や止まっている人は入らない)
        if (moveDir == Vector2.zero) return false;
        Vector2 f = new Vector2(Forward.x, Forward.z);
        if (Vector2.Dot(moveDir, f) < joinAlign) return false;

        // 同じ流れ(レーン)に入っている人だけ
        if (requireSameLane && analyzer != null)
        {
            var myLane = analyzer.GetLaneAt(transform.position);
            var theirLane = analyzer.GetLaneAt(pos);
            if (myLane == null || theirLane == null || myLane.id != theirLane.id) return false;
        }

        return true;
    }

    /// <summary>空いている席を割り当てる。なければ -1</summary>
    public int RequestSlot(FormationMember member)
    {
        int idx = FindBestSlot(member.transform.position);
        if (idx < 0) return -1;

        slots[idx].member = member;
        MemberCount++;
        return idx;
    }

    public void ReleaseSlot(int index, FormationMember member)
    {
        if (index < 0 || index >= slots.Count) return;
        if (slots[index].member != member) return;

        slots[index].member = null;
        MemberCount--;
    }

    /// <summary>空いていて有効な席のうち、近くて前の方の席</summary>
    int FindBestSlot(Vector3 pos)
    {
        int best = -1;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            if (!s.valid || s.member != null) continue;

            float score = Vector3.Distance(pos, GetSlotPosition(i)) + s.row * frontBias;
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    // ------------------------------------------------------------
    // 白 = 空き、緑 = 使用中、赤 = 壁で使えない
    // ------------------------------------------------------------
    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            Vector3 p = GetSlotPosition(i) + Vector3.up * 0.05f;

            Gizmos.color = !s.valid ? Color.red : (s.member != null ? Color.green : Color.white);
            Gizmos.DrawWireSphere(p, spacing * 0.35f);

            if (s.member != null)
                Gizmos.DrawLine(p, s.member.transform.position);
        }

        // 参加できる範囲
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, joinRadius);
    }
}
