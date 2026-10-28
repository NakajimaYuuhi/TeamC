using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using T = TitleTween;

//タイトルロゴの動きだけを担当する
//・登場: 「押して」が左から滑り込み、「参る！」が大きい所からドンと着地、星と線が飛び出す
//・待機: 1.6秒ごとに線が動く、ロゴ全体が右へ押されて「押」がつぶれる。星はゆらしてる、「参る！」はときどき跳ねる
//・退場: 決定のとき左へはける
public class TitleLogo : MonoBehaviour
{
    [Header("ロゴ部品")]
    [Tooltip("ロゴ全体の親")]
    [SerializeField] RectTransform logoRoot;
    [Tooltip("「押して」の親")]
    [SerializeField] RectTransform line1;
    [Tooltip("「押」")]
    [SerializeField] RectTransform oshi;
    [Tooltip("「参る！」")]
    [SerializeField] RectTransform line2;
    [Tooltip("衝撃の星")]
    [SerializeField] RectTransform star;
    [Tooltip("線")]
    [SerializeField] RectTransform tsukkomi;

    [Header("押される拍子")]
    [Tooltip("線が飛ぶ間隔(秒)")]
    [SerializeField] float beat = 1.6f;
    [Tooltip("押されたときにロゴが右へ動く量(px)")]
    [SerializeField] float pushDistance = 13.5f;
    [Tooltip("「押」がつぶれたときの大きさ(横, 縦)")]
    [SerializeField] Vector2 squash = new Vector2(0.82f, 1.1f);

    [Header("その他の待機の動き")]
    [Tooltip("「参る！」が跳ねる間隔(秒)")]
    [SerializeField] float bounceInterval = 3.2f;
    [Tooltip("星がゆれる片道の秒数")]
    [SerializeField] float starSwayTime = 0.9f;

    //配置したときの位置・大きさ
    Vector2 rootPos, line1Pos, line2Pos;
    Vector3 oshiScale, line2Scale, starScale, tsukkomiScale;
    Vector3 starRot;
    CanvasGroup line1Group, line2Group, starGroup, tsukkomiGroup;
    bool ready;

    readonly List<Tween> idle = new List<Tween>();
    Sequence intro, exit;

    void Awake() => Init();

    void Init()
    {
        if (ready) return;
        ready = true;
        rootPos = logoRoot.anchoredPosition;
        line1Pos = line1.anchoredPosition;
        line2Pos = line2.anchoredPosition;
        oshiScale = oshi.localScale;
        line2Scale = line2.localScale;
        starScale = star.localScale;
        starRot = star.localEulerAngles;
        tsukkomiScale = tsukkomi.localScale;
        line1Group = T.Group(line1);
        line2Group = T.Group(line2);
        starGroup = T.Group(star);
        tsukkomiGroup = T.Group(tsukkomi);
    }

    void OnDisable() => KillAll();

    // ---------- 登場 ----------

    //登場の動きを最初から再生する。終わるまで約1秒
    public void PlayIntro()
    {
        Init();
        KillAll();
        ResetPose();

        //最初は全部隠しておく
        line1Group.alpha = 0f;
        line2Group.alpha = 0f;
        starGroup.alpha = 0f;
        tsukkomiGroup.alpha = 0f;

        intro = DOTween.Sequence().SetLink(gameObject);
        //「押して」: 左から滑り込む
        intro.Insert(0.10f, T.Run(0.45f, Ease.OutBack, t =>
        {
            line1.anchoredPosition = line1Pos + new Vector2(Mathf.LerpUnclamped(-840f, 0f, t), 0f);
            line1Group.alpha = Mathf.Clamp01(t);
        }));
        //「参る！」: 2.6倍から着地
        intro.Insert(0.35f, T.Run(0.31f, Ease.InQuad, t =>
        {
            line2.localScale = line2Scale * Mathf.LerpUnclamped(2.6f, 1f, t);
            line2Group.alpha = t;
        }));
        //ツッコミ線: ぽんと出る
        intro.Insert(0.52f, T.Run(0.22f, Ease.OutBack, t =>
        {
            tsukkomi.localScale = tsukkomiScale * t;
            tsukkomiGroup.alpha = Mathf.Clamp01(t);
        }));
        //星: 回りながら出る
        intro.Insert(0.68f, T.Run(0.36f, Ease.OutBack, t =>
        {
            star.localScale = starScale * t;
            star.localEulerAngles = starRot + new Vector3(0, 0, Mathf.LerpUnclamped(40f, 0f, t));
            starGroup.alpha = 1f;
        }));
    }

    // ---------- 待機 ----------

    public void StartIdle()
    {
        Init();
        StopIdle();

        //星: ゆっくり大きく傾いて戻る
        idle.Add(T.Run(starSwayTime, Ease.InOutSine, t =>
        {
            star.localScale = starScale * Mathf.Lerp(1f, 1.06f, t);
            star.localEulerAngles = starRot + new Vector3(0, 0, Mathf.Lerp(0f, -8f, t));
        }).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject));

        //押される拍子
        idle.Add(T.Run(beat, Ease.Linear, PushBeat).SetLoops(-1, LoopType.Restart).SetLink(gameObject));

        //「参る！」: ときどき跳ねる
        idle.Add(DOTween.Sequence()
            .AppendInterval(bounceInterval)
            .Append(T.Run(0.42f, Ease.OutQuad, Bounce))
            .SetLoops(-1).SetLink(gameObject));
    }

    public void StopIdle()
    {
        foreach (var tw in idle) tw.Kill();
        idle.Clear();
        if (ready) ResetPose();
    }

    static readonly float[] PushT = { 0f, 0.36f, 0.46f, 0.58f, 0.85f, 1f };
    static readonly float[] PushX = { 0f, 0f, 1f, 0.78f, 0f, 0f };
    static readonly Ease[] PushE = { Ease.Linear, Ease.OutBack, Ease.InOutSine, Ease.InOutSine, Ease.Linear };

    static readonly float[] OshiT = { 0f, 0.37f, 0.43f, 0.56f, 0.68f, 1f };
    static readonly Ease[] OshiE = { Ease.Linear, Ease.OutCubic, Ease.OutBack, Ease.InOutSine, Ease.Linear };

    static readonly float[] TkT = { 0f, 0.37f, 0.45f, 0.65f, 1f };
    static readonly float[] TkS = { 1f, 0.6f, 1.35f, 1f, 1f };
    static readonly float[] TkA = { 1f, 0.5f, 1f, 1f, 1f };
    static readonly Ease[] TkE = { Ease.Linear, Ease.OutBack, Ease.OutCubic, Ease.Linear };

    //t = 拍子の中の位置(0～1)。ツッコミ線が縮んで溜め → 飛び出す瞬間にロゴが押され、「押」がつぶれる
    void PushBeat(float t)
    {
        logoRoot.anchoredPosition = rootPos + new Vector2(T.Keys(t, PushT, PushX, PushE) * pushDistance, 0f);

        float[] sx = { 1f, 1f, squash.x, 1.05f, 1f, 1f };
        float[] sy = { 1f, 1f, squash.y, 0.97f, 1f, 1f };
        oshi.localScale = new Vector3(oshiScale.x * T.Keys(t, OshiT, sx, OshiE), oshiScale.y * T.Keys(t, OshiT, sy, OshiE), oshiScale.z);

        tsukkomi.localScale = tsukkomiScale * T.Keys(t, TkT, TkS, TkE);
        tsukkomiGroup.alpha = T.Keys(t, TkT, TkA, TkE);
    }

    static readonly float[] BounceT = { 0f, 0.35f, 0.7f, 1f };
    static readonly float[] BounceY = { 0f, 21f, 0f, 0f };
    static readonly float[] BounceS = { 1f, 1.06f, 0.97f, 1f };

    void Bounce(float t)
    {
        line2.anchoredPosition = line2Pos + new Vector2(0f, T.Keys(t, BounceT, BounceY, null));
        line2.localScale = line2Scale * T.Keys(t, BounceT, BounceS, null);
    }

    // ---------- 退場 ----------

    //左へはける。delay 秒後に始まる
    public void PlayExit(float delay)
    {
        Init();
        StopIdle();
        exit?.Kill();
        exit = DOTween.Sequence().SetLink(gameObject);
        exit.Insert(delay, T.Run(0.3f, Ease.InCubic, t =>
        {
            line1.anchoredPosition = line1Pos + new Vector2(Mathf.Lerp(0f, -930f, t), 0f);
            line1Group.alpha = 1f - t;
        }));
        exit.Insert(delay + 0.05f, T.Run(0.3f, Ease.InCubic, t =>
        {
            line2.anchoredPosition = line2Pos + new Vector2(Mathf.Lerp(0f, -930f, t), 0f);
            line2Group.alpha = 1f - t;
        }));
        exit.Insert(delay, T.Run(0.22f, Ease.OutQuad, t =>
        {
            starGroup.alpha = 1f - t;
            tsukkomiGroup.alpha = 1f - t;
        }));
    }

    // ---------- 共通 ----------

    void ResetPose()
    {
        logoRoot.anchoredPosition = rootPos;
        line1.anchoredPosition = line1Pos;
        line2.anchoredPosition = line2Pos;
        oshi.localScale = oshiScale;
        line2.localScale = line2Scale;
        star.localScale = starScale;
        star.localEulerAngles = starRot;
        tsukkomi.localScale = tsukkomiScale;
        tsukkomiGroup.alpha = 1f;
    }

    void KillAll()
    {
        intro?.Kill();
        exit?.Kill();
        intro = exit = null;
        foreach (var tw in idle) tw.Kill();
        idle.Clear();
    }
}
