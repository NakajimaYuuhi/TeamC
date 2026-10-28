using System;
using UnityEngine;
using DG.Tweening;

//タイトル画面の動きで使う
//・Run   : 0→1 の値を時間で動かし、毎フレーム apply に渡す
//・Keys  : キーフレームの並びから、その時点の値を求める
public static class TitleTween
{
    public static Tween Run(float duration, Ease ease, Action<float> apply)
    {
        return DOTween.To(() => 0f, t => apply(t), 1f, duration).SetEase(ease);
    }

    //times は 0～1 の昇順。eases[i] は times[i] から times[i+1] までの区間に使う
    public static float Keys(float t, float[] times, float[] values, Ease[] eases)
    {
        if (t <= times[0]) return values[0];
        int last = times.Length - 1;
        if (t >= times[last]) return values[last];
        for (int i = 0; i < last; i++)
        {
            if (t > times[i + 1]) continue;
            float local = (t - times[i]) / (times[i + 1] - times[i]);
            Ease e = eases != null && i < eases.Length ? eases[i] : Ease.Linear;
            return DOVirtual.EasedValue(values[i], values[i + 1], local, e);
        }
        return values[last];
    }

    public static CanvasGroup Group(Component c)
    {
        if (c == null) return null;
        return c.TryGetComponent(out CanvasGroup g) ? g : c.gameObject.AddComponent<CanvasGroup>();
    }
}
