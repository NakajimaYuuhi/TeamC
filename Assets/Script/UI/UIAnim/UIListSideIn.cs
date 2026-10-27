using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

//見本帳 A13「リストが流れ込む」
//メニューの親に付けると、子(ボタンなど)が上から順に、少しずつ遅れて横から滑り込む
//親が表示される(SetActive(true))たびに自動で再生される。スクリプトからは Show() / Hide()
[RequireComponent(typeof(RectTransform))]
public class UIListSlideIn : MonoBehaviour
{
    [Header("動き")]
    [Tooltip("どれだけ横から入ってくるか(マイナス = 左から / プラス = 右から)")]
    [SerializeField] float offsetX = -400f;
    [SerializeField] float duration = 0.4f;
    [Tooltip("1行ごとの遅れ(秒)")]
    [SerializeField] float stagger = 0.08f;
    [SerializeField] float delay = 0f;
    [SerializeField] Ease ease = Ease.OutCubic;

    [Header("対象")]
    [Tooltip("空なら、このオブジェクトの直下にある表示中の子すべてを上から順に使う")]
    [SerializeField] List<RectTransform> items = new List<RectTransform>();

    [Header("オプション")]
    [Tooltip("一緒にフェードインする(各行に CanvasGroup を自動で付ける)")]
    [SerializeField] bool fade = true;
    [Tooltip("ポーズ中(timeScale = 0)でも動かす。ポーズメニューならオン")]
    [SerializeField] bool ignoreTimeScale = true;
    [Tooltip("表示されたら自動で再生する")]
    [SerializeField] bool playOnEnable = true;
    [Tooltip("P キーで再生する(動きの確認用)")]
    [SerializeField] bool playWithPKey = true;

    readonly List<RectTransform> targets = new List<RectTransform>();
    readonly List<Vector2> basePositions = new List<Vector2>();
    readonly List<CanvasGroup> groups = new List<CanvasGroup>();
    Sequence seq;

    void OnEnable()
    {
        if (playOnEnable) Show();
    }

    void OnDisable()
    {
        //途中で閉じても、次に開いたとき正しい位置から始まるよう元に戻す
        seq?.Kill();
        RestoreBase();
    }

    void Update()
    {
        if (playWithPKey && PressedP()) Show();
    }

    //上から順に横から滑り込ませる
    public void Show()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            if (playOnEnable) return; // OnEnable から Show() が呼ばれるので二重に再生しない
        }

        seq?.Kill();
        RestoreBase();
        CollectTargets();

        seq = DOTween.Sequence().SetUpdate(ignoreTimeScale).SetLink(gameObject);
        for (int i = 0; i < targets.Count; i++)
        {
            var rt = targets[i];
            var basePos = basePositions[i];
            float at = delay + i * stagger;

            rt.anchoredPosition = basePos + new Vector2(offsetX, 0);
            seq.Insert(at, MoveTo(rt, basePos, duration).SetEase(ease));

            if (groups[i] != null)
            {
                groups[i].alpha = 0f;
                seq.Insert(at, FadeTo(groups[i], 1f, duration * 0.5f));
            }
        }
    }

    //下の行から順に横へはけさせ、最後に非表示にする
    public void Hide()
    {
        seq?.Kill();
        if (targets.Count == 0) CollectTargets();

        seq = DOTween.Sequence().SetUpdate(ignoreTimeScale).SetLink(gameObject);
        int n = targets.Count;
        for (int i = 0; i < n; i++)
        {
            float at = (n - 1 - i) * stagger * 0.6f;
            seq.Insert(at, MoveTo(targets[i], basePositions[i] + new Vector2(offsetX, 0), duration * 0.6f).SetEase(Ease.InCubic));
            if (groups[i] != null) seq.Insert(at, FadeTo(groups[i], 0f, duration * 0.6f));
        }
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    //動かす行と、その本来の位置を集める
    void CollectTargets()
    {
        //Layout Group で並べている場合は、先に並びを確定させてから位置を記録する
        if (TryGetComponent(out LayoutGroup _))
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

        targets.Clear();
        basePositions.Clear();
        groups.Clear();

        if (items.Count > 0)
        {
            foreach (var rt in items)
                if (rt != null && rt.gameObject.activeSelf) targets.Add(rt);
        }
        else
        {
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf && child is RectTransform rt) targets.Add(rt);
        }

        foreach (var rt in targets)
        {
            basePositions.Add(rt.anchoredPosition);
            CanvasGroup g = null;
            if (fade && !rt.TryGetComponent(out g)) g = rt.gameObject.AddComponent<CanvasGroup>();
            groups.Add(g);
        }
    }

    //記録しておいた本来の位置・透明度に戻す
    void RestoreBase()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == null) continue;
            targets[i].anchoredPosition = basePositions[i];
            if (groups[i] != null) groups[i].alpha = 1f;
        }
    }

    //DOTween の UI モジュールがなくても動くよう、汎用の To で書く
    static Tween MoveTo(RectTransform rt, Vector2 pos, float time)
    {
        return DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, pos, time);
    }

    static Tween FadeTo(CanvasGroup g, float alpha, float time)
    {
        return DOTween.To(() => g.alpha, a => g.alpha = a, alpha, time);
    }

    //新旧どちらの入力方式でも P キーの押下を判定する
    static bool PressedP()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
    }
}
