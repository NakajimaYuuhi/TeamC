using DG.Tweening;
using UnityEngine;
using TeamC.FanAsset;

//扇風機の見た目。MD_Fan に付ける
//ステージに置いてあるときと持っているときは羽を止めて小さくしておく
//投げて着地したら、投げた向きを向いて大きくなり、羽を回す
//着地は親の Rigidbody の速さで見る。投げる処理は書きかえない
[RequireComponent(typeof(FanSpin))]
public class FanDeploy : MonoBehaviour
{
    [Header("大きさ")]
    [Tooltip("置いてあるときの大きさ")]
    [SerializeField] float idleScale = 0.35f;
    [Tooltip("設置したときの大きさ")]
    [SerializeField] float deployedScale = 0.8f;
    [Tooltip("大きくなるまでの秒数")]
    [SerializeField] float popTime = 0.35f;

    [Header("着地")]
    [Tooltip("この速さより遅くなったら止まったとみなす")]
    [SerializeField] float landSpeed = 0.5f;
    [Tooltip("止まったまま、この秒数たったら設置する")]
    [SerializeField] float landWait = 0.1f;
    [Tooltip("設置したら拾えないように Item タグを外す")]
    [SerializeField] bool lockAfterDeploy = true;

    [Header("羽")]
    [Tooltip("設置したあとの羽の速さ。720 で1秒に2回転")]
    [SerializeField] float spinSpeed = 720f;
    [Tooltip("羽が全速になるまでの秒数")]
    [SerializeField] float spinUpTime = 0.6f;

    [Header("演出")]
    [Tooltip("設置したときに出すエフェクト。なくてもよい")]
    [SerializeField] GameObject deployEffect;

    public bool IsDeployed { get; private set; }

    //設置したときに呼ぶ。引数は風を送る向き
    public event System.Action<Vector3> Deployed;

    FanSpin spin;
    Rigidbody body;
    Vector3 faceDir;
    bool thrown;
    float slowTimer;

    void Awake()
    {
        spin = GetComponent<FanSpin>();
        spin.DegreesPerSecond = 0f;
        transform.localScale = Vector3.one * idleScale;
    }

    /// <summary>向く方向を決める。投げた向きを入れる</summary>
    public void SetFaceDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-4f) faceDir = dir.normalized;
    }

    void Update()
    {
        if (IsDeployed) return;

        //Rigidbody は投げたときに付くので、見つかるまで探す
        if (body == null) body = GetComponentInParent<Rigidbody>();
        if (body == null || body.isKinematic)
        {
            slowTimer = 0f;
            return;
        }

        float speed = body.linearVelocity.magnitude;
        //飛んでいるのを見てから、止まるのを待つ
        if (!thrown)
        {
            if (speed > landSpeed * 2f) thrown = true;
            return;
        }
        if (speed < landSpeed)
        {
            slowTimer += Time.deltaTime;
            if (slowTimer >= landWait) Deploy();
        }
        else
        {
            slowTimer = 0f;
        }
    }

    /// <summary>その場に設置して羽を回す。着地で自動で呼ばれる</summary>
    public void Deploy()
    {
        if (IsDeployed) return;
        IsDeployed = true;

        Transform root = body != null ? body.transform : transform;
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        //まっすぐ立てて、投げた向きを向く。向きがわからなければ今の向きのまま
        Vector3 f = faceDir;
        if (f.sqrMagnitude < 1e-4f)
        {
            f = root.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        }
        f.Normalize();
        root.rotation = Quaternion.LookRotation(f, Vector3.up);
        transform.localRotation = Quaternion.identity;

        //真下の地面に置く。自分の当たり判定は無視する
        if (FindGround(root, out Vector3 ground)) root.position = ground;

        if (lockAfterDeploy && root.CompareTag("Item")) root.tag = "Untagged";

        //ぼんっと大きくなる
        transform.DOKill();
        transform.localScale = Vector3.one * idleScale;
        transform.DOScale(deployedScale, popTime).SetEase(Ease.OutBack).SetLink(gameObject);

        //羽を回し始める
        DOTween.To(() => spin.DegreesPerSecond, v => spin.DegreesPerSecond = v, spinSpeed, spinUpTime)
            .SetEase(Ease.OutQuad).SetLink(gameObject);

        if (deployEffect != null) Instantiate(deployEffect, root.position, Quaternion.identity);
        Deployed?.Invoke(f);
    }

    /// <summary>羽を止める</summary>
    public void StopSpin(float time)
    {
        DOTween.To(() => spin.DegreesPerSecond, v => spin.DegreesPerSecond = v, 0f, time)
            .SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    static bool FindGround(Transform root, out Vector3 point)
    {
        point = default;
        var hits = Physics.RaycastAll(root.position + Vector3.up, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(root)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                point = h.point;
            }
        }
        return best < float.MaxValue;
    }
}
