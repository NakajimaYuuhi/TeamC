using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

//タイトルの右に出すプレイヤー。ロゴの「参る！」の着地と同時に、手前から飛び込んでドンと着地する
//専用のカメラで映した絵を UI の RawImage に貼る。この部品はシーンの見えない所に置く
public class TitleHero : MonoBehaviour
{
    [Header("モデル")]
    [Tooltip("プレイヤーのモデル")]
    [SerializeField] GameObject playerModel;
    [Tooltip("着地で吹き飛ぶ人のモデル。順番に使う")]
    [SerializeField] GameObject[] npcModels;

    [Header("映す")]
    [Tooltip("カメラの絵を貼る所")]
    [SerializeField] RawImage view;
    [Tooltip("後ろの集中線")]
    [SerializeField] Image lines;
    [Tooltip("カメラの縦の画角。度")]
    [SerializeField] float fieldOfView = 44f;

    [Header("時間。ロゴの登場に合わせる")]
    [Tooltip("飛び込み始める秒数")]
    [SerializeField] float startTime = 0.35f;
    [Tooltip("着地する秒数。参る！ の着地と同じ")]
    [SerializeField] float landTime = 0.66f;

    [Header("集中線")]
    [Tooltip("ふだんの濃さ")]
    [SerializeField] float lineAlpha = 0.22f;
    [Tooltip("着地で足す濃さ")]
    [SerializeField] float lineBurst = 0.45f;
    [Tooltip("回る速さ。度/秒")]
    [SerializeField] float lineSpin = 6f;

    //吹き飛ぶ人の立つ所と、飛ぶ向き。x は横、z は奥
    static readonly Vector3[] HitPos = { new Vector3(-1.05f, 0, 0.4f), new Vector3(1.1f, 0, 0.5f), new Vector3(-0.5f, 0, 1.8f), new Vector3(0.6f, 0, 2.0f) };
    static readonly float[] HitSide = { -1f, 1f, -1f, 1f };

    class Part
    {
        public Transform t;
        public Quaternion baseRotation;
        public Vector3 right, forward;
    }

    class Hit
    {
        public Transform root, spin;
        public Vector3 pos;
        public float side;
    }

    Transform hero, lean;
    Part armL, armR, legL, legR, head, tailL, tailR;
    Hit[] hits;
    float playTime = -100f;
    bool built;

    void Awake()
    {
        Build();
        if (view) view.color = new Color(1f, 1f, 1f, 0f);
        if (lines) lines.color = new Color(1f, 1f, 1f, 0f);
    }

    // ---------- 外から呼ぶ ----------

    //登場を最初から流す。TitleScreen の登場と同じときに呼ぶ
    public void PlayIntro()
    {
        Build();
        DOTween.Kill(this);
        playTime = Time.time;
        if (view) view.color = Color.white;
    }

    //決定したときに消す
    public void PlayExit(float duration)
    {
        DOTween.Kill(this);
        if (view) DOTween.To(() => view.color.a, a => view.color = new Color(1f, 1f, 1f, a), 0f, duration).SetTarget(this).SetLink(gameObject);
        if (lines) DOTween.To(() => lines.color.a, a => lines.color = new Color(1f, 1f, 1f, a), 0f, duration).SetTarget(this).SetLink(gameObject);
    }

    // ---------- 組み立て ----------

    void Build()
    {
        if (built || playerModel == null) return;
        built = true;

        //カメラ。絵は RawImage に貼る
        var tex = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "TitleHero" };
        var cam = new GameObject("HeroCamera").AddComponent<Camera>();
        cam.transform.SetParent(transform, false);
        cam.transform.localPosition = new Vector3(0.3f, 1.1f, -3f);
        cam.transform.LookAt(transform.TransformPoint(new Vector3(0f, 0.8f, 0f)));
        cam.fieldOfView = fieldOfView;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 30f;
        //HDR だと背景の透明が消えるので切る
        cam.allowHDR = false;
        cam.targetTexture = tex;
        if (view) view.texture = tex;

        //プレイヤー。腰を中心に前へ傾けられるように台を重ねる
        hero = new GameObject("Hero").transform;
        hero.SetParent(transform, false);
        //カメラのほうを向き、少しロゴの側へ体をひねる
        hero.localRotation = Quaternion.Euler(0f, 200f, 0f);
        lean = new GameObject("Lean").transform;
        lean.SetParent(hero, false);
        lean.localPosition = new Vector3(0f, 0.43f, 0f);
        var model = Instantiate(playerModel, lean);
        model.transform.localPosition = new Vector3(0f, -0.43f, 0f);
        armL = Find(model.transform, "ArmL"); armR = Find(model.transform, "ArmR");
        legL = Find(model.transform, "LegL"); legR = Find(model.transform, "LegR");
        head = Find(model.transform, "Head");
        tailL = Find(model.transform, "BandTailL"); tailR = Find(model.transform, "BandTailR");

        //吹き飛ぶ人。回せるように体の真ん中に台を置く
        hits = new Hit[npcModels == null ? 0 : Mathf.Min(npcModels.Length, HitPos.Length)];
        for (int i = 0; i < hits.Length; i++)
        {
            var root = new GameObject("Npc" + i).transform;
            root.SetParent(transform, false);
            var spin = new GameObject("Spin").transform;
            spin.SetParent(root, false);
            spin.localPosition = new Vector3(0f, 0.9f, 0f);
            if (npcModels[i] != null) Instantiate(npcModels[i], spin).transform.localPosition = new Vector3(0f, -0.9f, 0f);
            hits[i] = new Hit { root = root, spin = spin, pos = HitPos[i], side = HitSide[i] };
            root.gameObject.SetActive(false);
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        hero.gameObject.SetActive(false);
    }

    //部品を名前で探し、プレイヤーの右と前を軸にして回せるようにする
    Part Find(Transform root, string partName)
    {
        var t = FindDeep(root, partName);
        if (t == null) return null;
        var parent = t.parent != null ? t.parent : root;
        return new Part
        {
            t = t,
            baseRotation = t.localRotation,
            right = parent.InverseTransformDirection(hero.right).normalized,
            forward = parent.InverseTransformDirection(hero.forward).normalized,
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

    //前へ上げる角度と、外へ開く角度。度
    static void Pose(Part p, float forward, float side = 0f)
    {
        if (p == null) return;
        p.t.localRotation = Quaternion.AngleAxis(-forward, p.right) * Quaternion.AngleAxis(side, p.forward) * p.baseRotation;
    }

    // ---------- 動き ----------

    void Update()
    {
        if (!built || hero == null) return;
        float t = Time.time - playTime;
        float after = t - landTime;
        hero.gameObject.SetActive(t >= startTime);
        if (t < startTime) { SetHits(t); SetLines(t, after); return; }

        //手前から飛び込んで、参る！ と同時に着地する
        float fly = Mathf.Clamp01((t - startTime) / Mathf.Max(0.01f, landTime - startTime));
        float k = 1f - fly * fly;
        hero.localPosition = new Vector3(0.35f * k, 0.6f * k, -2.3f * k);
        //着地でつぶれて、はね返る
        float squash = after < 0f ? 0f : Mathf.Exp(-after * 7f) * Mathf.Cos(after * 22f);
        hero.localScale = new Vector3(1f + squash * 0.12f, 1f - squash * 0.16f, 1f + squash * 0.12f);

        //構え。右腕を前に突き出し、ひざを曲げる。着地のあとは軽く息をする
        float breath = after > 0.6f ? Mathf.Sin((after - 0.6f) * 2.4f) : 0f;
        lean.localPosition = new Vector3(0f, 0.43f - (after > 0f ? 0.05f : 0f) + breath * 0.008f, 0f);
        lean.localRotation = Quaternion.AngleAxis(12.6f + breath * 0.9f, Vector3.right);
        Pose(armR, 77f + breath * 1.7f, -14f);
        Pose(armL, -31.5f, 20f);
        Pose(legR, 31.5f, 7f);
        Pose(legL, -26f, -7f);
        //頭は少しそらす
        Pose(head, 7f);
        //はちまきの端は後ろへなびき続ける
        float lift = after < 0f ? 17f : 0f;
        Pose(tailL, -(72f + lift + Mathf.Sin(t * 3f) * 9f), 14f + Mathf.Sin(t * 2.2f) * 5f);
        Pose(tailR, -(72f + lift + Mathf.Sin(t * 3f + 1f) * 9f), -14f - Mathf.Sin(t * 2.2f + 2f) * 5f);

        SetHits(t);
        SetLines(t, after);
    }

    //着地で回りながら吹き飛び、小さくなって消える
    void SetHits(float t)
    {
        float after = t - landTime;
        foreach (var h in hits)
        {
            bool show = t >= startTime - 0.05f && (after < 0f || after < 1.6f);
            h.root.gameObject.SetActive(show);
            if (!show) continue;
            if (after < 0f)
            {
                float pop = Mathf.Clamp01((t - startTime + 0.05f) / 0.15f);
                h.root.localPosition = h.pos;
                h.root.localRotation = Quaternion.Euler(0f, -h.side * 23f, 0f);
                h.spin.localRotation = Quaternion.identity;
                h.root.localScale = Vector3.one * 0.9f * pop;
                continue;
            }
            //ロゴにかぶらないように、左へ飛ぶ人は横を短く、上を高くする
            float f = after, side = h.side < 0f ? -2f : 4.5f, up = h.side < 0f ? 4f : 3.4f;
            h.root.localPosition = h.pos + new Vector3(side * f, up * f - 2.4f * f * f, -0.8f * f);
            h.spin.localRotation = Quaternion.Euler(f * 172f, 0f, h.side * f * 573f);
            h.root.localScale = Vector3.one * 0.9f * Mathf.Max(0f, 1f - Mathf.Max(0f, f - 0.9f) * 1.6f);
        }
    }

    //着地でぱっと濃くなり、あとはうすく残ってゆっくり回る
    void SetLines(float t, float after)
    {
        if (lines == null) return;
        if (after < 0f) { lines.color = new Color(1f, 1f, 1f, 0f); return; }
        float burst = Mathf.Max(0f, 1f - after / 0.5f);
        float exitAlpha = view != null ? view.color.a : 1f;
        lines.color = new Color(1f, 1f, 1f, (lineAlpha + lineBurst * burst) * exitAlpha);
        lines.rectTransform.localScale = Vector3.one * (1f + burst * 0.12f);
        lines.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * lineSpin);
    }
}
