/////////////////////////////////////////
/// WarpDemoCrowdAgent.cs
/// ワープデモ用の簡易NPC（通路を乱雑に行き交う）
///
/// 本番のNPC_Controllerには手を入れず、デモ専用で動かすためのもの。
/// 開始点 → 途中のランダムな点 → 反対側の端 と歩き、着いたら消える。
/////////////////////////////////////////

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WarpDemoCrowdAgent : MonoBehaviour
{
    private static readonly Collider[] neighborBuffer = new Collider[16];

    [SerializeField] private float acceleration = 6f;
    [SerializeField] private float arriveDistance = 0.6f;
    [SerializeField] private float avoidRadius = 1.1f;
    [SerializeField] private float avoidWeight = 0.8f;
    [SerializeField] private float turnSpeed = 10f;

    private Rigidbody rb;
    private WarpDemoCrowdSpawner owner;
    private readonly Vector3[] path = new Vector3[2];
    private int pathIndex;
    private float speed;
    private float life;
    private int npcMask;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Begin(WarpDemoCrowdSpawner spawner, Vector3 mid, Vector3 end, float moveSpeed, int mask)
    {
        owner = spawner;
        path[0] = mid;
        path[1] = end;
        pathIndex = 0;
        speed = moveSpeed;
        life = 0f;
        npcMask = mask;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 dir = mid - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
    }

    private void FixedUpdate()
    {
        if (owner == null) return;

        life += Time.fixedDeltaTime;
        if (life > owner.MaxLifetime)
        {
            owner.Despawn(this);
            return;
        }

        Vector3 pos = rb.position;
        Vector3 toTarget = path[pathIndex] - pos;
        toTarget.y = 0f;

        if (toTarget.magnitude < arriveDistance)
        {
            pathIndex++;
            if (pathIndex >= path.Length)
            {
                owner.Despawn(this);
                return;
            }
            toTarget = path[pathIndex] - pos;
            toTarget.y = 0f;
        }

        Vector3 forward = toTarget.normalized;
        Vector3 desiredDir = (forward + GetSeparation(pos, forward) * avoidWeight).normalized;

        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 next = Vector3.MoveTowards(current, desiredDir * speed, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector3(next.x, rb.linearVelocity.y, next.z);

        if (next.sqrMagnitude > 0.05f)
        {
            Quaternion look = Quaternion.LookRotation(next.normalized);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));
        }
    }

    //前方にいる人からだけ離れる簡易回避
    private Vector3 GetSeparation(Vector3 pos, Vector3 forward)
    {
        int count = Physics.OverlapSphereNonAlloc(pos, avoidRadius, neighborBuffer, npcMask, QueryTriggerInteraction.Ignore);
        Vector3 sep = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            Collider c = neighborBuffer[i];
            if (c.attachedRigidbody == rb) continue;

            Vector3 diff = pos - c.transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist < 0.01f) continue;
            if (Vector3.Dot(forward, -diff / dist) < 0.2f) continue; //前方の人だけ

            sep += diff / (dist * dist);
        }
        return sep;
    }
}
