using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

//扇風機アイテムの風。PFB_Item_Fan のいちばん上に付ける
//投げた向きを覚えておき、設置したらその向きへ風を放って NPC を押す
//風が止まったら小さくなって消える
public class FanWind : MonoBehaviour, IThrowableItem
{
    [Header("風")]
    [Tooltip("風の届く長さ")]
    [SerializeField] float range = 6f;
    [Tooltip("風の幅")]
    [SerializeField] float width = 2.5f;
    [Tooltip("風の高さ")]
    [SerializeField] float height = 2f;
    [Tooltip("扇風機のそばで NPC を押す強さ。重さに関係なく同じだけ加速する")]
    [SerializeField] float power = 30f;
    [Tooltip("風の先のほうの強さ。そばの強さに対する割合")]
    [SerializeField, Range(0f, 1f)] float farPower = 0.4f;
    [Tooltip("風を出し続ける秒数")]
    [SerializeField] float windTime = 8f;
    [Tooltip("風の向きを逆にする。モデルの向きが合わないとき用")]
    [SerializeField] bool reverse;
    [Tooltip("押す相手のタグ")]
    [SerializeField] string npcTag = "NPC";

    [Header("終わり")]
    [Tooltip("風が止まったら消す")]
    [SerializeField] bool removeAfterWind = true;
    [Tooltip("羽が止まるまでの秒数")]
    [SerializeField] float stopTime = 0.6f;

    [Header("演出")]
    [Tooltip("風を出しているあいだ出すエフェクト。風の向きを向けて子にする。なくてもよい")]
    [SerializeField] GameObject windEffect;

    FanDeploy deploy;
    Vector3 windDir;
    float windLeft;
    GameObject effect;
    AudioSource loopAudio;
    readonly HashSet<Rigidbody> pushed = new HashSet<Rigidbody>();
    readonly Collider[] hits = new Collider[64];

    void Awake()
    {
        deploy = GetComponentInChildren<FanDeploy>(true);
        if (deploy != null) deploy.Deployed += StartWind;
    }

    void OnDestroy()
    {
        if (loopAudio != null) { loopAudio.Stop(); Destroy(loopAudio); }
        if (deploy != null) deploy.Deployed -= StartWind;
    }

    public void OnPickedUp(GameObject picker) { }

    public void OnThrown(GameObject thrower, Vector3 velocity)
    {
        if (deploy != null) deploy.SetFaceDirection(velocity);
    }

    void StartWind(Vector3 dir)
    {
        windDir = reverse ? -dir : dir;
        windLeft = windTime;
        loopAudio = SceneAudioController.Instance?.PlayItemLoop(gameObject);
        if (windEffect != null)
            effect = Instantiate(windEffect, transform.position, Quaternion.LookRotation(windDir, Vector3.up), transform);
    }

    void FixedUpdate()
    {
        if (windLeft <= 0f) return;
        windLeft -= Time.fixedDeltaTime;
        Blow();
        if (windLeft <= 0f) EndWind();
    }

    //風の箱の中にいる NPC を風の向きへ押す
    void Blow()
    {
        Vector3 origin = transform.position;
        Vector3 center = origin + windDir * (range * 0.5f) + Vector3.up * (height * 0.5f);
        var half = new Vector3(width * 0.5f, height * 0.5f, range * 0.5f);
        int n = Physics.OverlapBoxNonAlloc(center, half, hits, Quaternion.LookRotation(windDir, Vector3.up), ~0, QueryTriggerInteraction.Ignore);

        pushed.Clear();
        for (int i = 0; i < n; i++)
        {
            var npc = FindNpcRoot(hits[i].transform);
            if (npc == null) continue;
            var rb = hits[i].attachedRigidbody != null ? hits[i].attachedRigidbody : npc.GetComponentInChildren<Rigidbody>();
            if (rb == null || rb.isKinematic || !pushed.Add(rb)) continue;

            //扇風機から遠いほど弱くする
            float d = Vector3.Dot(rb.position - origin, windDir);
            float k = Mathf.Lerp(1f, farPower, Mathf.Clamp01(d / range));
            rb.AddForce(windDir * (power * k), ForceMode.Acceleration);
        }
    }

    void EndWind()
    {
        if (loopAudio != null) { loopAudio.Stop(); Destroy(loopAudio); loopAudio = null; }
        if (effect != null) Destroy(effect);
        if (deploy != null) deploy.StopSpin(stopTime);
        if (!removeAfterWind) return;
        transform.DOScale(0f, 0.3f).SetDelay(stopTime).SetEase(Ease.InBack).SetLink(gameObject)
            .OnComplete(() => Destroy(gameObject));
    }

    GameObject FindNpcRoot(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag(npcTag)) return t.gameObject;
        }
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 dir = windLeft > 0f ? windDir : transform.forward;
        if (reverse && windLeft <= 0f) dir = -dir;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + dir * (range * 0.5f) + Vector3.up * (height * 0.5f), Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, range));
    }
}
