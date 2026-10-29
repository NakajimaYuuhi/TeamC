using UnityEngine;

//NPC の腕と脚を、歩く速さに合わせて前後に振る。頭も少し左右に揺らす
//部品は名前で探す。ArmL ArmR LegL LegR Head
public class NPCLimbSwing : MonoBehaviour
{
    [Tooltip("速さを読む Rigidbody。空なら親から探す")]
    [SerializeField] Rigidbody body;
    [Tooltip("全速のときの腕の振れ幅。度")]
    [SerializeField] float armAngle = 35f;
    [Tooltip("全速のときの脚の振れ幅。度")]
    [SerializeField] float legAngle = 30f;
    [Tooltip("頭の左右の揺れ。度")]
    [SerializeField] float headAngle = 6f;
    [Tooltip("この速さで振れ幅がいちばん大きくなる。m/秒")]
    [SerializeField] float fullSpeed = 2.5f;
    [Tooltip("この速さより遅いと振らない。m/秒")]
    [SerializeField] float minSpeed = 0.15f;
    [Tooltip("1m 進むあいだに足を踏み出す回数")]
    [SerializeField] float stepsPerMeter = 1.4f;
    [Tooltip("振れ幅が変わる速さ。大きいほどすぐ止まる")]
    [SerializeField] float settle = 8f;
    [Tooltip("押すときに両腕を前へ出す角度。度")]
    [SerializeField] float pushAngle = 80f;
    [Tooltip("押す動きの長さ。秒")]
    [SerializeField] float pushTime = 0.35f;

    class Part
    {
        public Transform t;
        public Quaternion baseRotation;
        public Vector3 swingAxis;
        public Vector3 swayAxis;
    }

    Part armL, armR, legL, legR, head;

    //テスト用。0 より大きいと、止まっていてもこの速さで振る
    public float PreviewSpeed { get; set; }
    float phase, amount, pushTimer;
    bool posed;

    public void SetBody(Rigidbody rb)
    {
        if (rb != null) body = rb;
    }

    //両腕をすばやく前へ突き出して戻す
    public void Push()
    {
        pushTimer = pushTime;
    }

    void Awake()
    {
        if (body == null) body = GetComponentInParent<Rigidbody>();
        armL = Find("ArmL");
        armR = Find("ArmR");
        legL = Find("LegL");
        legR = Find("LegR");
        head = Find("Head");
    }

    //取り込みで部品の向きが変わっていても、NPC の右と前を軸にして回す
    Part Find(string partName)
    {
        var t = FindDeep(transform, partName);
        if (t == null) return null;
        var parent = t.parent != null ? t.parent : transform;
        var frame = transform.parent != null ? transform.parent : transform;
        return new Part
        {
            t = t,
            baseRotation = t.localRotation,
            swingAxis = parent.InverseTransformDirection(frame.right).normalized,
            swayAxis = parent.InverseTransformDirection(frame.forward).normalized,
        };
    }

    static Transform FindDeep(Transform root, string partName)
    {
        foreach (Transform child in root)
        {
            if (child.name == partName) return child;
            var found = FindDeep(child, partName);
            if (found != null) return found;
        }
        return null;
    }

    void Update()
    {
        float speed = 0f;
        if (body != null)
        {
            Vector3 v = body.linearVelocity;
            speed = new Vector2(v.x, v.z).magnitude;
        }
        if (PreviewSpeed > 0f) speed = Mathf.Max(speed, PreviewSpeed);
        float target = Mathf.Clamp01((speed - minSpeed) / Mathf.Max(0.01f, fullSpeed - minSpeed));

        //押す動きは最初の4分の1で突き出し、残りで戻す
        float push = 0f;
        if (pushTimer > 0f)
        {
            pushTimer -= Time.deltaTime;
            float t = 1f - Mathf.Max(0f, pushTimer) / Mathf.Max(0.01f, pushTime);
            push = Mathf.SmoothStep(0f, 1f, t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f);
        }

        //止まって振りもおさまったら、何もしない
        if (target <= 0f && amount < 0.001f && push <= 0f)
        {
            if (posed) { amount = 0f; posed = false; ResetPose(); }
            return;
        }
        posed = true;
        amount = Mathf.Lerp(amount, target, 1f - Mathf.Exp(-settle * Time.deltaTime));
        phase += Mathf.Max(speed, minSpeed) * stepsPerMeter * Mathf.PI * Time.deltaTime;

        float s = Mathf.Sin(phase) * amount;
        Swing(armL, Mathf.Lerp(s * armAngle, -pushAngle, push));
        Swing(armR, Mathf.Lerp(-s * armAngle, -pushAngle, push));
        Swing(legL, -s * legAngle);
        Swing(legR, s * legAngle);
        if (head != null) head.t.localRotation = Quaternion.AngleAxis(s * headAngle, head.swayAxis) * head.baseRotation;
    }

    void ResetPose()
    {
        foreach (var p in new[] { armL, armR, legL, legR, head }) if (p != null) p.t.localRotation = p.baseRotation;
    }

    static void Swing(Part p, float angle)
    {
        if (p == null) return;
        p.t.localRotation = Quaternion.AngleAxis(angle, p.swingAxis) * p.baseRotation;
    }
}
