using DG.Tweening;
using UnityEngine;

public enum ResultPopKind { Slide, Pop, Line, Up }

//券の中で順に出すもの1つ
[System.Serializable]
public class ResultPopItem
{
    [Tooltip("出す文字や点線")]
    public RectTransform target;
    [Tooltip("出かた。Slide=左からすべる Pop=ポンとはずむ Line=左から引く Up=下からふわっと")]
    public ResultPopKind kind = ResultPopKind.Up;
}

//リザルトとゲームオーバーで使う動き
//DOTween の UI モジュールを使わずに動かす
public static class ResultTween
{
    public static Tweener Move(RectTransform rt, Vector2 to, float time)
        => DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, to, time);

    public static Tweener Fade(CanvasGroup g, float to, float time)
        => DOTween.To(() => g.alpha, v => g.alpha = v, to, time);

    public static Tweener Shake(RectTransform rt, float time, float strength)
    {
        Vector2 basePos = Vector2.zero;
        Vector3 offset = Vector3.zero;
        return DOTween.Shake(() => offset, v => { offset = v; rt.anchoredPosition = basePos + (Vector2)v; }, time, strength, 14, 90f, true, true)
            .OnStart(() => basePos = rt.anchoredPosition)
            .OnComplete(() => rt.anchoredPosition = basePos);
    }

    //整理券が上から落ちてくる
    public static void InsertTicketDrop(Sequence seq, RectTransform ticket, Vector2 pos)
    {
        ticket.anchoredPosition = pos + new Vector2(0f, 1260f);
        ticket.localEulerAngles = new Vector3(0f, 0f, 8f);
        seq.Insert(0f, Move(ticket, pos, 0.52f).SetEase(Ease.OutBack));
        seq.Insert(0f, ticket.DOLocalRotate(new Vector3(0f, 0f, 2f), 0.52f).SetEase(Ease.OutBack));
    }

    //券の文字を上から順に出す
    public static void InsertPops(Sequence seq, ResultPopItem[] items, Vector2[] pos, float delay)
    {
        for (int i = 0; i < items.Length; i++)
        {
            InsertPop(seq, items[i], pos[i], delay);
            delay += 0.09f;
        }
    }

    static void InsertPop(Sequence seq, ResultPopItem item, Vector2 pos, float delay)
    {
        var rt = item.target;
        var g = Group(rt.gameObject);
        switch (item.kind)
        {
            case ResultPopKind.Slide:
                g.alpha = 0f;
                rt.anchoredPosition = pos - new Vector2(120f, 0f);
                seq.Insert(delay, Fade(g, 1f, 0.24f));
                seq.Insert(delay, Move(rt, pos, 0.24f).SetEase(Ease.OutCubic));
                break;
            case ResultPopKind.Pop:
                g.alpha = 0f;
                rt.localScale = Vector3.one * 0.4f;
                rt.localEulerAngles = new Vector3(0f, 0f, 6f);
                seq.Insert(delay, Fade(g, 1f, 0.16f));
                seq.Insert(delay, rt.DOScale(1f, 0.32f).SetEase(Ease.OutBack));
                seq.Insert(delay, rt.DOLocalRotate(Vector3.zero, 0.32f).SetEase(Ease.OutBack));
                break;
            case ResultPopKind.Line:
                rt.localScale = new Vector3(0f, 1f, 1f);
                seq.Insert(delay, rt.DOScaleX(1f, 0.24f).SetEase(Ease.OutCubic));
                break;
            default:
                g.alpha = 0f;
                rt.anchoredPosition = pos - new Vector2(0f, 36f);
                seq.Insert(delay, Fade(g, 1f, 0.24f));
                seq.Insert(delay, Move(rt, pos, 0.24f).SetEase(Ease.OutCubic));
                break;
        }
    }

    //ハンコを上からドンと押す。angle は押したあとの傾き
    public static void InsertStamp(Sequence seq, RectTransform st, float angle, float delay)
    {
        var g = Group(st.gameObject);
        g.alpha = 0f;
        st.localScale = Vector3.one * 3f;
        st.localEulerAngles = new Vector3(0f, 0f, angle + 30f);
        seq.Insert(delay, Fade(g, 1f, 0.05f));
        seq.Insert(delay, st.DOScale(0.92f, 0.208f).SetEase(Ease.InCubic));
        seq.Insert(delay, st.DOLocalRotate(new Vector3(0f, 0f, angle), 0.208f).SetEase(Ease.InCubic));
        seq.Insert(delay + 0.208f, st.DOScale(1f, 0.052f));
    }

    //下から浮かび上がる札
    public static void InsertRise(Sequence seq, RectTransform rt, Vector2 pos, float delay)
    {
        var g = Group(rt.gameObject);
        g.alpha = 0f;
        rt.anchoredPosition = pos - new Vector2(0f, 54f);
        seq.Insert(delay, Fade(g, 1f, 0.15f));
        seq.Insert(delay, Move(rt, pos, 0.3f).SetEase(Ease.OutBack));
    }

    public static void SetColor(HudDigits digits, Color c)
    {
        foreach (var img in digits.GetComponentsInChildren<UnityEngine.UI.Image>(true)) img.color = c;
    }

    public static CanvasGroup Group(GameObject go)
    {
        var g = go.GetComponent<CanvasGroup>();
        return g != null ? g : go.AddComponent<CanvasGroup>();
    }

    public static RectTransform Rt(Component c) => (RectTransform)c.transform;
}
