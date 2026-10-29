using UnityEngine;

//NPC の見た目を、色違いのモデルからランダムに1つ選んで出す。NPC のいちばん上に付ける
//チームの NPC_AnimationManager が揺らす Body の上下と傾きを、モデルにも写す
public class NPCLook : MonoBehaviour
{
    [Tooltip("色違いのモデル。この中から1つ選ぶ")]
    [SerializeField] GameObject[] models;
    [Tooltip("番号を決めたいときは 0 以上。-1 ならランダム")]
    [SerializeField] int fixedIndex = -1;
    [Tooltip("モデルの大きさ。1.25 で身長約2m、今の当たり判定と同じ")]
    [SerializeField] float modelScale = 1.25f;
    [Tooltip("これまでの見た目。当たり判定は残して表示だけ切る")]
    [SerializeField] Renderer[] hideRenderers;

    [Header("体の揺れ")]
    [Tooltip("揺れを写す元。チームの Body")]
    [SerializeField] Transform bobSource;
    [Tooltip("この高さより上に上がった分だけモデルを浮かせる。止まっているときは地面に立つ")]
    [SerializeField] float bobBase = 0.35f;
    [Tooltip("写す揺れの強さ")]
    [SerializeField] float bobScale = 0.6f;
    [Tooltip("この速さで揺れがいちばん大きくなる。止まっているときは揺らさない。m/秒")]
    [SerializeField] float bobFullSpeed = 1f;

    [Header("テスト")]
    [Tooltip("0 より大きいと、止まっていてもこの速さで歩いているように腕と脚を振る。m/秒")]
    [SerializeField] float previewSpeed = 0f;

    public Transform Model { get; private set; }

    Quaternion modelRotation;
    Rigidbody body;
    float bobAmount;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        foreach (var r in hideRenderers) if (r != null) r.enabled = false;
        if (models == null || models.Length == 0) return;

        int index = fixedIndex >= 0 ? Mathf.Min(fixedIndex, models.Length - 1) : Random.Range(0, models.Length);
        var model = Instantiate(models[index], transform);
        model.name = "Model";
        Model = model.transform;
        //取り込みで付いた向きと大きさは残して、そこから大きくする
        Model.localPosition = Vector3.zero;
        modelRotation = Model.localRotation;
        Model.localScale *= modelScale;

        //NPC はたくさん出るので、影は足元の丸い影にまかせる
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        var swing = model.GetComponent<NPCLimbSwing>();
        if (swing == null) swing = model.AddComponent<NPCLimbSwing>();
        swing.SetBody(body);
        swing.PreviewSpeed = previewSpeed;
    }

    void LateUpdate()
    {
        if (bobSource == null || Model == null) return;
        //チームの揺れは止まっていても続くので、動いているときだけ写す
        float speed = 0f;
        if (body != null && !body.isKinematic)
        {
            Vector3 v = body.linearVelocity;
            speed = new Vector2(v.x, v.z).magnitude;
        }
        if (previewSpeed > 0f) speed = Mathf.Max(speed, previewSpeed);
        float target = Mathf.Clamp01(speed / Mathf.Max(0.01f, bobFullSpeed));
        bobAmount = Mathf.Lerp(bobAmount, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
        float y = Mathf.Max(0f, bobSource.localPosition.y - bobBase) * bobScale * bobAmount;
        Model.localPosition = new Vector3(0f, y, 0f);
        Model.localRotation = bobSource.localRotation * modelRotation;
    }
}
