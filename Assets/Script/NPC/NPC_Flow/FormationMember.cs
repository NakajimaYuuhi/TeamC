using UnityEngine;

/// <summary>
/// 隊形のメンバー(NPCに付ける)。
///  - 条件を満たしたら、リーダーの空いている席に入る
///  - 席に入っている間は「リーダーの速度 + 席へのバネ」で動く(位置は固定しない)
///  - 席から長く離れたら(よろけた・はぐれた)席を手放して、普通のNPCに戻る
/// 移動そのものは NPC_FlowLocomotion が DesiredVelocity を読んで行う。
/// </summary>
public class FormationMember : MonoBehaviour
{
    [SerializeField] FormationLeader leader;

    [Header("席を追う")]
    [Tooltip("席に引っぱられる強さ(1/秒)。大きいほど席にピタッと戻る")]
    [SerializeField, Min(0f)] float spring = 3f;
    [Tooltip("席に戻るときに、リーダーの速度に上乗せできる速さの上限(m/s)")]
    [SerializeField, Min(0f)] float maxCatchUpSpeed = 3f;

    [Header("外れる・入り直す")]
    [Tooltip("席からこの距離以上離れた状態が続いたら外れる(m)")]
    [SerializeField, Min(0f)] float breakDistance = 3f;
    [Tooltip("離れた状態が何秒続いたら外れるか")]
    [SerializeField, Min(0f)] float breakTime = 1f;
    [Tooltip("外れたあと、何秒は入り直さないか(入ったり出たりを繰り返さないように)")]
    [SerializeField, Min(0f)] float rejoinCooldown = 2f;
    [Tooltip("参加できるかを何秒ごとに確認するか")]
    [SerializeField, Min(0.02f)] float joinCheckInterval = 0.25f;

    [Header("向きが合わなくなったら外れる")]
    [Tooltip("自分の行きたい向きと隊形の向きの内積がこれ未満なら「向きが合っていない」。0.2で約78°以上ずれたら。参加の条件(リーダーの joinAlign)より小さくしておく")]
    [SerializeField, Range(-1f, 1f)] float leaveAlign = 0.2f;
    [Tooltip("向きが合っていない状態が何秒続いたら外れるか(一瞬の方向転換では外れないように)")]
    [SerializeField, Min(0f)] float leaveTime = 0.5f;

    [Header("Debug")]
    [SerializeField] bool showDebug = false;

    int slot = -1;
    float farTimer;
    float misalignTimer;
    float cooldown;
    float checkTimer;
    Vector3 prevPos;
    Vector2 moveDir;
    NPC_FlowLocomotion locomotion;   // 付いていれば「行きたい向き」で参加を判定する

    public bool HasSlot => slot >= 0;
    /// <summary>席に入っているときの目標速度</summary>
    public Vector3 DesiredVelocity { get; private set; }

    void Start()
    {
        if (leader == null) leader = FindAnyObjectByType<FormationLeader>();
        locomotion = GetComponent<NPC_FlowLocomotion>();
        prevPos = transform.position;
        checkTimer = Random.Range(0f, joinCheckInterval);  // NPCごとに確認のタイミングをずらす
    }

    void OnDisable()
    {
        Leave(0f);
    }

    void Update()
    {
        if (leader == null) return;
        float dt = Time.deltaTime;

        // 自分の進んでいる向き
        Vector3 d = transform.position - prevPos;
        prevPos = transform.position;
        Vector2 m = new Vector2(d.x, d.z);
        if (m.sqrMagnitude > 1e-6f) moveDir = m.normalized;

        cooldown -= dt;

        // ---- 席がない:参加できるか確認 ----
        if (!HasSlot)
        {
            checkTimer -= dt;
            if (checkTimer > 0f || cooldown > 0f) return;
            checkTimer = joinCheckInterval;

            // 実際に動いた向き(moveDir)ではなく、本人が行きたい向きで判定する。
            // 押し返されて後ろ向きに動いている人を、仲間だと勘違いして入れないように
            if (leader.CanJoin(transform.position, IntendedDir()))
                slot = leader.RequestSlot(this);
            return;
        }

        // ---- 席がある:席を追う ----
        Vector3 toSlot = leader.GetSlotPosition(slot) - transform.position;
        toSlot.y = 0f;

        // 離れすぎた状態が続いたら外れる
        if (toSlot.magnitude > breakDistance) farTimer += dt;
        else farTimer = 0f;

        if (farTimer > breakTime)
        {
            Leave(rejoinCooldown);
            return;
        }

        // 自分の行きたい向きと隊形の向きが合わなくなった状態が続いたら外れる
        // (リーダーが横や後ろに向きを変えたら、前に行きたい人は離れていく。
        //  後ろ向きに歩いて逆向きの人を集めても、振り返ったら離れる)
        Vector2 intended = IntendedDir();
        Vector2 f = new Vector2(leader.Forward.x, leader.Forward.z);
        if (intended != Vector2.zero && Vector2.Dot(intended, f.normalized) < leaveAlign) misalignTimer += dt;
        else misalignTimer = 0f;

        if (misalignTimer > leaveTime)
        {
            Leave(rejoinCooldown);
            return;
        }

        // リーダーと同じ速度で動きつつ、ずれた分だけバネで席に戻る
        Vector3 catchUp = Vector3.ClampMagnitude(toSlot * spring, maxCatchUpSpeed);
        DesiredVelocity = leader.Velocity + catchUp;
    }

    // 本人が行きたい向き(NPC_FlowLocomotion がなければ、実際に動いた向き)
    Vector2 IntendedDir()
    {
        if (locomotion == null) return moveDir;
        Vector3 w = locomotion.DesiredDirection;
        return w.sqrMagnitude > 1e-6f ? new Vector2(w.x, w.z).normalized : Vector2.zero;
    }

    /// <summary>席を手放す</summary>
    public void Leave(float cooldownSec)
    {
        if (!HasSlot) return;
        if (leader != null) leader.ReleaseSlot(slot, this);
        slot = -1;
        farTimer = 0f;
        misalignTimer = 0f;
        cooldown = cooldownSec;
    }

    /// <summary>リーダーから呼ばれる:席が壁で使えなくなって、別の席に移った(-1 なら外れた)</summary>
    public void OnSlotChanged(int newSlot)
    {
        slot = newSlot;
        farTimer = 0f;
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying || !HasSlot || leader == null) return;
        Gizmos.color = farTimer > 0f ? Color.yellow : Color.green;
        Gizmos.DrawLine(transform.position + Vector3.up, leader.GetSlotPosition(slot) + Vector3.up);
    }
}
