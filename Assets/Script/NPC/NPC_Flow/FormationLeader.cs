using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 隊形の中心(プレイヤーに付ける)。
/// 自分の後ろに「席」を並べて、FormationMember に席を貸し出す。
///  - 形は縦長(Column:1人席×3列 → 2人席×3列 → …)か、くさび形(Wedge)
///  - 隊形の向きは、なめらかにした移動方向に合わせる
///  - 壁にめり込んでいる席・逆向きの人混みに埋まっている席は使わない
///    (狭い所やプレイヤーが開けた細い道では、隊形が道に合わせて細くなる)
/// </summary>
public class FormationLeader : MonoBehaviour
{
    public enum FormationShape { Column, Wedge }

    [Header("隊形")]
    [Tooltip("Column:縦長(道に沿って並ぶ) / Wedge:くさび形")]
    [SerializeField] FormationShape shape = FormationShape.Column;
    [Tooltip("Column:何列ごとに横の人数を1人増やすか(3なら 1人×3列 → 2人×3列 → …)")]
    [SerializeField, Min(1)] int columnRowsPerWidth = 3;
    [Tooltip("Column:横の最大人数(後ろの帯の幅に合わせる)")]
    [SerializeField, Min(1)] int columnMaxWidth = 3;
    [Tooltip("Column:列数")]
    [SerializeField, Min(1)] int columnRows = 10;
    [Tooltip("Wedge:くさびの列数。列 r には r+1 個の席が並ぶ")]
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
    [Tooltip("逆向きの流れが強い席(人混みに埋まっている席)を使わない")]
    [SerializeField] bool blockByCounterFlow = true;
    [Tooltip("席の位置の逆流が maxStrength のこの割合を超えたら使えなくする。半分まで下がったらまた使える(ちらつき防止)")]
    [SerializeField, Range(0.05f, 1f)] float counterBlockRatio = 0.3f;
    [SerializeField] FlowField field;

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
    [Tooltip("リーダーよりこの距離(m)以上前にいる人は参加できない。前を歩いている人が下がってきて後ろの席に入るのを防ぐ(追い抜いた人だけが仲間になる)。0で真横から後ろだけ")]
    [SerializeField] float joinFrontLimit = 0.5f;
    [Tooltip("あれば、リーダーと同じレーンにいる人だけが参加できる")]
    [SerializeField] FlowLaneAnalyzer analyzer;
    [SerializeField] bool requireSameLane = true;

    [Header("通った跡(遅れた仲間がたどる道)")]
    [Tooltip("プレイヤーが通った跡を何mごとに記録するか")]
    [SerializeField, Min(0.1f)] float trailSpacing = 0.5f;
    [Tooltip("跡を何m分覚えておくか")]
    [SerializeField, Min(1f)] float trailLength = 30f;
    [Tooltip("はぐれたばかりの人が戻ってこれる範囲(m)。普段の joinRadius より広く")]
    [SerializeField, Min(0f)] float rejoinRadius = 10f;

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
    readonly List<Vector3> trail = new List<Vector3>();   // [0] = 古い / [Count-1] = 新しい
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
    /// <summary>リーダーが前に進もうとしているか(入力があればその有無、なければ動いているか)。押し合いで止まっていても true になりうる</summary>
    public bool IsAdvancing => intendedTimer > 0f ? intendedDir != Vector3.zero : Velocity.magnitude > minMoveSpeed;

    // ------------------------------------------------------------
    void Awake()
    {
        BuildSlots();
        prevPos = transform.position;

        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude > 1e-4f) Forward = f.normalized;

        if (analyzer == null) analyzer = FindAnyObjectByType<FlowLaneAnalyzer>();
        if (field == null) field = FindAnyObjectByType<FlowField>();
    }

    /// <summary>席を作る(形は shape で切り替え)</summary>
    void BuildSlots()
    {
        slots.Clear();
        if (shape == FormationShape.Column) BuildColumnSlots();
        else BuildWedgeSlots();
    }

    /// <summary>縦長の席。columnRowsPerWidth 列ごとに横が1人ずつ増える(最大 columnMaxWidth)</summary>
    void BuildColumnSlots()
    {
        for (int r = 1; r <= columnRows; r++)
        {
            int w = Mathf.Min(1 + (r - 1) / columnRowsPerWidth, columnMaxWidth);
            float z = -(r * spacing + firstRowOffset);
            for (int i = 0; i < w; i++)
            {
                float x = (i - (w - 1) * 0.5f) * spacing;
                slots.Add(new Slot { local = new Vector3(x, 0f, z), row = r });
            }
        }
    }

    /// <summary>くさび形の席。列 r に r+1 個(六角形の詰め方)</summary>
    void BuildWedgeSlots()
    {
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

        RecordTrail();

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

        float maxStrength = field != null ? Mathf.Max(field.MaxStrength, 0.0001f) : 1f;
        Vector2 f2 = new Vector2(Forward.x, Forward.z);

        for (int i = 0; i < slots.Count; i++)
        {
            Slot s = slots[i];
            Vector3 pos = GetSlotPosition(i);
            bool valid = true;

            // 壁にめり込んでいないか
            if (obstacleMask.value != 0)
            {
                Vector3 p = pos + Vector3.up * checkHeight;
                if (Physics.CheckSphere(p, checkRadius, obstacleMask, QueryTriggerInteraction.Ignore)) valid = false;
            }

            // 逆向きの人混みに埋まっていないか(プレイヤーが開けた道の外の席は使わない)
            if (valid && blockByCounterFlow && field != null)
            {
                float counter = -Vector2.Dot(field.Sample(pos), f2) / maxStrength;
                // 一度使えなくなった席は、逆流が半分まで下がるまで使えないまま(ちらつき防止)
                float limit = s.valid ? counterBlockRatio : counterBlockRatio * 0.5f;
                if (counter > limit) valid = false;
            }

            s.valid = valid;

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
    public bool CanJoin(Vector3 pos, Vector2 moveDir) => CanJoinCore(pos, moveDir, joinRadius, requireSameLane);

    /// <summary>はぐれたばかりの人が戻れるか(範囲が広く、レーンは問わない)</summary>
    public bool CanRejoin(Vector3 pos, Vector2 moveDir) => CanJoinCore(pos, moveDir, rejoinRadius, false);

    bool CanJoinCore(Vector3 pos, Vector2 moveDir, float radius, bool checkLane)
    {
        if (MemberCount >= maxMembers) return false;

        Vector3 d = pos - transform.position;
        d.y = 0f;
        if (d.magnitude > radius) return false;

        // リーダーより前にいる人は入れない(追い抜いて、横か後ろになってから)
        if (Vector3.Dot(d, Forward) > joinFrontLimit) return false;

        // 同じ向きに進んでいる人だけ(逆向きの人や止まっている人は入らない)
        if (moveDir == Vector2.zero) return false;
        Vector2 f = new Vector2(Forward.x, Forward.z);
        if (Vector2.Dot(moveDir, f) < joinAlign) return false;

        // 同じ流れ(レーン)に入っている人だけ
        if (checkLane && analyzer != null)
        {
            var myLane = analyzer.GetLaneAt(transform.position);
            var theirLane = analyzer.GetLaneAt(pos);
            if (myLane == null || theirLane == null || myLane.id != theirLane.id) return false;
        }

        return true;
    }

    // ------------------------------------------------------------
    // 通った跡
    // ------------------------------------------------------------

    void RecordTrail()
    {
        Vector3 p = transform.position;
        if (trail.Count > 0)
        {
            Vector3 d = p - trail[trail.Count - 1];
            d.y = 0f;
            if (d.sqrMagnitude < trailSpacing * trailSpacing) return;
        }
        trail.Add(p);

        int max = Mathf.CeilToInt(trailLength / trailSpacing);
        if (trail.Count > max) trail.RemoveRange(0, trail.Count - max);
    }

    /// <summary>
    /// from から一番近い跡の点を探して、そこから lookAhead(m)だけ新しい方(リーダー側)へ進んだ点を返す。
    /// 遅れた仲間がこれを目指すと、プレイヤーが開けた道をたどって戻ってこれる。
    /// </summary>
    public Vector3 GetTrailTarget(Vector3 from, float lookAhead)
    {
        if (trail.Count == 0) return transform.position;

        int best = 0;
        float bestD = float.PositiveInfinity;
        for (int i = 0; i < trail.Count; i++)
        {
            Vector3 d = trail[i] - from;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestD) { bestD = sq; best = i; }
        }

        Vector3 cur = trail[best];
        float remain = lookAhead;
        for (int i = best + 1; i <= trail.Count; i++)
        {
            Vector3 next = i < trail.Count ? trail[i] : transform.position;   // 最後はリーダーの今の位置
            Vector3 seg = next - cur;
            seg.y = 0f;
            float len = seg.magnitude;
            if (len >= remain && len > 1e-4f) return Vector3.Lerp(cur, next, remain / len);
            remain -= len;
            cur = next;
        }
        return transform.position;
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

        // 通った跡(水色)
        Gizmos.color = Color.cyan;
        for (int i = 1; i < trail.Count; i++)
            Gizmos.DrawLine(trail[i - 1] + Vector3.up * 0.05f, trail[i] + Vector3.up * 0.05f);
    }
}
