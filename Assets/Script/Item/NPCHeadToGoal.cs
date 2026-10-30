using UnityEngine;

//しばらくのあいだ、決めた場所へ向かって歩かせる。双眼鏡の効果で NPC に付く
//NPC_Controller が決めた向きを、毎フレームあとから上書きする。時間が来るか着いたら自分を消す
public class NPCHeadToGoal : MonoBehaviour
{
    Vector3 goal;
    float timeLeft;
    float speedRate = 1.2f;
    NPC_FlowLocomotion locomotion;
    Rigidbody body;

    //向かわせ始める。もう付いていれば行き先と時間を入れ直す
    public static void Begin(GameObject npc, Vector3 goal, float duration, float speedRate)
    {
        var head = npc.GetComponent<NPCHeadToGoal>();
        if (head == null) head = npc.AddComponent<NPCHeadToGoal>();
        head.goal = goal;
        head.timeLeft = duration;
        head.speedRate = speedRate;
    }

    void Awake()
    {
        locomotion = GetComponent<NPC_FlowLocomotion>();
        body = GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        timeLeft -= Time.deltaTime;
        Vector3 to = goal - transform.position;
        to.y = 0f;
        if (timeLeft <= 0f || to.sqrMagnitude < 1f)
        {
            Destroy(this);
            return;
        }
        if (locomotion != null) locomotion.SetDesiredDirection(to, speedRate);
    }

    //流れで動かない古い NPC は、力で押して向かわせる
    void FixedUpdate()
    {
        if (locomotion != null || body == null) return;
        Vector3 to = goal - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 1e-4f) body.AddForce(to.normalized * 8f, ForceMode.Acceleration);
    }
}
