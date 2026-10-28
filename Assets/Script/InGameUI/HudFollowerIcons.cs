using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//ついてきている人を、人のアイコンで並べる(1人 = 1つ)
//・この RectTransform の箱にすべて入るよう、人数が増えるほどアイコンを小さくして詰める
//・並びは左上から右へ。行は箱の下にそろえる(人混みが下からふくらむ見た目)
//・アイコンの Image は必要なぶんだけ自動で作る
public class HudFollowerIcons : MonoBehaviour
{
    [Tooltip("人のアイコンの画像(crew_icon)")]
    [SerializeField] Sprite icon;
    [Tooltip("いちばん大きいときのアイコンの大きさ(crew_icon の大きさ)")]
    [SerializeField] Vector2 iconSize = new Vector2(36, 46);
    [Tooltip("アイコンどうしのすき間(px)")]
    [SerializeField] float gap = 1.5f;
    [Tooltip("これより小さくはしない(いちばん大きいときに対する割合)")]
    [SerializeField, Range(0.1f, 1f)] float minScale = 0.25f;
    [Tooltip("最初に作っておくアイコンの数")]
    [SerializeField] int prewarm = 20;

    readonly List<Image> pool = new List<Image>();
    RectTransform box;
    int count = -1;

    void Awake()
    {
        box = (RectTransform)transform;
        for (int i = 0; i < prewarm; i++) Get(i).enabled = false;
    }

    public void SetCount(int n)
    {
        if (!box) Awake();
        n = Mathf.Max(0, n);
        if (n == count) return;
        int before = Mathf.Max(0, count);
        count = n;

        float W = box.rect.width, H = box.rect.height;
        //すべて入るいちばん大きい縮み方を探す
        float s = 1f;
        for (; s > minScale; s -= 0.02f)
        {
            int perRow = Mathf.FloorToInt((W + gap) / (iconSize.x * s + gap));
            int rowsFit = Mathf.FloorToInt((H + gap) / (iconSize.y * s + gap));
            if (perRow * rowsFit >= n) break;
        }
        float w = iconSize.x * s, h = iconSize.y * s;
        int per = Mathf.Max(1, Mathf.FloorToInt((W + gap) / (w + gap)));
        int rows = Mathf.CeilToInt(n / (float)per);
        float top = H - rows * h - Mathf.Max(0, rows - 1) * gap;

        for (int i = 0; i < Mathf.Max(n, pool.Count); i++)
        {
            if (i >= n) { if (i < pool.Count) pool[i].enabled = false; continue; }
            var img = Get(i);
            img.enabled = true;
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2((i % per) * (w + gap), -(top + (i / per) * (h + gap)));
            //新しく増えた人は、ぽんと出る
            if (i >= before)
            {
                rt.DOComplete();
                rt.localScale = Vector3.one;
                rt.DOPunchScale(Vector3.one * 0.35f, 0.25f, 6, 0.8f).SetLink(rt.gameObject);
            }
        }
    }

    Image Get(int i)
    {
        while (pool.Count <= i)
        {
            var go = new GameObject("Crew" + pool.Count, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            var img = go.GetComponent<Image>();
            img.sprite = icon;
            img.raycastTarget = false;
            img.preserveAspect = true;
            pool.Add(img);
        }
        return pool[i];
    }
}
