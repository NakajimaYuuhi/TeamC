using System;
using UnityEngine;

//ステージの群衆の1人に付けて、NPC_Controller の代わりに行き先を決める
//動き方は 立つ、行き来する、道をたどる の3つ。足の NPC_FlowLocomotion に向きと速さを渡すだけ
public class CrowdAgent : MonoBehaviour
{
    public enum Mode { Hold, Patrol, Route }

    NPC_FlowLocomotion locomotion;
    NPC_Controller controller;
    Rigidbody body;

    Mode mode = Mode.Hold;
    Vector3 anchor, facing;
    float pull = 1f;
    Vector3[] points;
    int index;
    float speed = 2f;
    float arrive = 0.6f;

    public Mode CurrentMode => mode;
    public Rigidbody Body => body;
    public NPC_Controller Controller => controller;
    //道の最後に着いたとき
    public event Action<CrowdAgent> RouteEnded;

    //NPC に付ける。NPC_Controller は止めて、こちらで動かす
    public static CrowdAgent Attach(GameObject npc)
    {
        var a = npc.GetComponent<CrowdAgent>();
        if (a == null) a = npc.AddComponent<CrowdAgent>();
        a.Setup();
        return a;
    }

    void Setup()
    {
        if (locomotion != null) return;
        locomotion = GetComponent<NPC_FlowLocomotion>();
        controller = GetComponent<NPC_Controller>();
        body = GetComponent<Rigidbody>();
        if (controller != null) controller.enabled = false;
    }

    void Awake() { Setup(); }

    //その場に立つ。押されてずれたら、pull の強さでゆっくり戻る
    public void Hold(Vector3 at, Vector3 face, float returnPull)
    {
        mode = Mode.Hold; anchor = at; facing = face; pull = returnPull;
    }

    //2点のあいだを行き来する
    public void Patrol(Vector3 a, Vector3 b, float moveSpeed)
    {
        mode = Mode.Patrol; points = new[] { a, b }; index = 1; speed = moveSpeed;
    }

    //点を順にたどる。最後に着いたら RouteEnded を呼ぶ
    public void Route(Vector3[] path, float moveSpeed)
    {
        mode = Mode.Route; points = path; index = 0; speed = moveSpeed;
        if (points == null || points.Length == 0) RouteEnded?.Invoke(this);
    }

    //動く床に乗せる。乗っているあいだは物理も足も止めて、外から位置を決める
    public void SetRiding(bool riding)
    {
        Setup();
        if (body != null) { body.isKinematic = riding; body.detectCollisions = !riding; }
        if (locomotion != null) locomotion.enabled = !riding;
        //乗っているあいだは自分で歩いていないので、手足を振らない
        foreach (var s in GetComponentsInChildren<NPCLimbSwing>(true)) s.enabled = !riding;
    }

    //別の場所へ移す
    public void Warp(Vector3 pos)
    {
        if (body != null)
        {
            body.position = pos;
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
        }
        transform.position = pos;
    }

    void FixedUpdate()
    {
        //遠くて止められているときや、隊形に入っているときは何もしない
        if (locomotion == null || !locomotion.enabled) return;

        switch (mode)
        {
            case Mode.Hold:
                {
                    Vector3 to = Flat(anchor - transform.position);
                    float d = to.magnitude;
                    if (d < 0.3f)
                    {
                        locomotion.SetIdleFacing(facing);
                        locomotion.Stop();
                        return;
                    }
                    locomotion.ClearIdleFacing();
                    locomotion.MoveSpeed = 1.5f;
                    locomotion.SetDesiredDirection(to, Mathf.Clamp01(d) * pull);
                    break;
                }
            case Mode.Patrol:
            case Mode.Route:
                {
                    if (points == null || points.Length == 0) { locomotion.Stop(); return; }
                    Vector3 to = Flat(points[index] - transform.position);
                    if (to.magnitude < arrive)
                    {
                        if (mode == Mode.Patrol) index = (index + 1) % points.Length;
                        else if (index + 1 < points.Length) index++;
                        else { locomotion.Stop(); RouteEnded?.Invoke(this); return; }
                        to = Flat(points[index] - transform.position);
                    }
                    locomotion.ClearIdleFacing();
                    locomotion.MoveSpeed = speed;
                    locomotion.SetDesiredDirection(to, 1f);
                    break;
                }
        }
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
