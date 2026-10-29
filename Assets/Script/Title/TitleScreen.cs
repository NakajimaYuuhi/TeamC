using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;
using UnityEngine.InputSystem;
using T = TitleTween;

//タイトル画面の動きを担当する。ロゴの動きは TitleLogoが担当
//・登場: 背景が出る → ロゴ → 画面がゆれる → メニューが左から順に出る
//・えらぶ: 選んだ項目がぐいっと伸び、上下の項目が押されてよろける
//・けってい: 押し込み＋白く光る → 全部左へはける → 朱色の幕が画面を覆う → イベントを呼ぶ
//・入力は Input Actions から読む
public class TitleScreen : MonoBehaviour
{
    [System.Serializable] public class IndexEvent : UnityEvent<int> { }

    [Header("背景")]
    [Tooltip("背景の親")]
    [SerializeField] CanvasGroup background;
    [Tooltip("大きな「押」の透かし")]
    [SerializeField] RectTransform watermark;
    [Tooltip("透かしが動く量(px)")]
    [SerializeField] Vector2 watermarkDrift = new Vector2(-103.5f, -90f);
    [Tooltip("透かしが片道にかける秒数")]
    [SerializeField] float watermarkTime = 9f;
    [Tooltip("「参る！」の着地でゆらす親")]
    [SerializeField] RectTransform shaker;

    [Header("ロゴ")]
    [SerializeField] TitleLogo logo;

    [Header("メニュー")]
    [Tooltip("各項目。子に Bg / Flash / Label / Arrow を置く")]
    [SerializeField] RectTransform[] items;
    [SerializeField] float widthOn = 630f;
    [SerializeField] float widthOff = 570f;
    [Tooltip("文字の左の位置")]
    [SerializeField] float labelXOn = 150f;
    [SerializeField] float labelXOff = 123f;
    [SerializeField] Color bgOn = new Color32(0xFF, 0xCF, 0x3F, 0xFF);
    [SerializeField] Color bgOff = new Color32(0x1E, 0x1B, 0x26, 0xCC);
    [SerializeField] Color labelOn = new Color32(0x1E, 0x1B, 0x26, 0xFF);
    [SerializeField] Color labelOff = new Color32(0xF2, 0xF3, 0xF7, 0xFF);
    [SerializeField] int startIndex = 0;

    [Header("けっていの幕")]
    [Tooltip("画面を覆う朱色の幕")]
    [SerializeField] RectTransform wipe;
    [Tooltip("幕の文字")]
    [SerializeField] Image wipeLabel;
    [Tooltip("項目ごとの幕の文字。上の items と同じ順")]
    [SerializeField] Sprite[] wipeSprites;
    [Tooltip("オンなら幕が開いて登場からもう一度再生する")]
    [SerializeField] bool replayAfterDecide = true;

    [Header("入力")]
    [Tooltip("上下の選択")]
    [SerializeField] InputActionReference navigateAction;
    [Tooltip("決定")]
    [SerializeField] InputActionReference submitAction;
    [Tooltip("押しっぱなしにしたとき、次に進み始めるまでの秒数")]
    [SerializeField] float repeatDelay = 0.4f;
    [Tooltip("押しっぱなし中に1つ進む間隔(秒)")]
    [SerializeField] float repeatInterval = 0.15f;

    [Header("イベント")]
    [Tooltip("幕が画面を覆ったとき。決定した項目の番号(0～)が渡される")]
    [SerializeField] IndexEvent onDecided = new IndexEvent();
    [Tooltip("「はじめる」で幕が覆ったとき")]
    [SerializeField] UnityEvent onStart = new UnityEvent();
    [Tooltip("「あそびかた」で幕が覆ったとき")]
    [SerializeField] UnityEvent onHowToPlay = new UnityEvent();
    [Tooltip("「ゲームを終了」で幕が覆ったとき")]
    [SerializeField] UnityEvent onQuit = new UnityEvent();

    public IndexEvent OnDecided => onDecided;
    public int SelectedIndex => selected;

    class Item
    {
        public RectTransform rect;
        public Vector2 pos;
        public CanvasGroup group;
        public Image bg, flash, label, arrow;
        public Vector2 arrowPos;
    }

    readonly List<Item> menu = new List<Item>();
    readonly List<Tween> idle = new List<Tween>();
    Sequence flow;
    Vector2 watermarkPos;
    int selected;
    bool busy = true; // 登場・決定の演出中は入力を受けない
    int heldDir;
    float repeatTimer;

    void OnEnable()
    {
        //アクションは他の画面と共有なので、ここでは有効にするだけで無効にはしない
        foreach (var r in new[] { navigateAction, submitAction })
            if (r != null && r.action != null) r.action.Enable();
        if (navigateAction == null || submitAction == null)
            Debug.LogWarning("[タイトル] Input Actions が未設定の欄があります(Navigate / Submit)");
    }

    void Start()
    {
        foreach (var rt in items)
        {
            var it = new Item
            {
                rect = rt,
                pos = rt.anchoredPosition,
                group = T.Group(rt),
                bg = Find(rt, "Bg"),
                flash = Find(rt, "Flash"),
                label = Find(rt, "Label"),
                arrow = Find(rt, "Arrow"),
            };
            if (it.arrow) it.arrowPos = it.arrow.rectTransform.anchoredPosition;
            menu.Add(it);
        }
        if (watermark) watermarkPos = watermark.anchoredPosition;
        PlayIntro();
    }

    void OnDisable()
    {
        flow?.Kill();
        StopIdle();
    }

    //子の中から名前で探す
    static Image Find(RectTransform root, string name)
    {
        Image found = null;
        int count = 0;
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (img.transform == root) continue;
            string n = img.name.Trim();
            int paren = n.IndexOf(" (");
            if (paren > 0) n = n.Substring(0, paren);
            if (!string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (found == null) found = img;
            count++;
        }
        if (found == null)
            Debug.LogWarning($"[タイトル] {root.name} の中に {name} (Image) が見つかりません。名前と Image がついているかを確認してください", root);
        else if (count > 1)
            Debug.LogWarning($"[タイトル] {root.name} の中に {name} が {count} 個あります。動かすのは上の1つだけなので、余分なものを消してください", found);
        return found;
    }

    void Update()
    {
        if (busy) return;
        ReadInput(out int move, out bool ok);
        if (move != 0) Move(move);
        if (ok) Decide();
    }

    // ---------- 登場 ----------

    public void PlayIntro()
    {
        busy = true;
        flow?.Kill();
        StopIdle();
        if (wipe) wipe.anchoredPosition = new Vector2(-WipeWidth(), 0f);
        if (shaker) shaker.anchoredPosition = Vector2.zero;
        selected = Mathf.Clamp(startIndex, 0, Mathf.Max(0, menu.Count - 1));
        ApplySelection(false);
        foreach (var it in menu)
        {
            it.rect.anchoredPosition = it.pos;
            it.rect.localScale = Vector3.one;
            it.rect.localEulerAngles = Vector3.zero;
            it.group.alpha = 0f;
            if (it.flash) SetAlpha(it.flash, 0f);
        }
        if (background) background.alpha = 0f;

        flow = DOTween.Sequence().SetLink(gameObject);
        if (background) flow.Insert(0f, T.Run(0.4f, Ease.OutQuad, t => background.alpha = t));
        if (logo) logo.PlayIntro();
        //「参る！」の着地で画面がゆれる
        if (shaker) flow.Insert(0.68f, T.Run(0.22f, Ease.Linear, Shake));
        //メニューが上から順に左から出る
        for (int i = 0; i < menu.Count; i++)
        {
            var it = menu[i];
            flow.Insert(0.7f + i * 0.08f, T.Run(0.42f, Ease.OutCubic, t =>
            {
                it.rect.anchoredPosition = it.pos + new Vector2(Mathf.Lerp(-690f, 0f, t), 0f);
                it.group.alpha = t;
            }));
        }
        flow.InsertCallback(1.2f, () =>
        {
            busy = false;
            StartIdle();
        });
    }

    static readonly float[] ShakeT = { 0f, 0.25f, 0.5f, 0.75f, 1f };
    static readonly float[] ShakeX = { 0f, -9f, 7.5f, -4.5f, 0f };
    static readonly float[] ShakeY = { 0f, -6f, 4.5f, -3f, 0f };

    void Shake(float t)
    {
        shaker.anchoredPosition = new Vector2(T.Keys(t, ShakeT, ShakeX, null), T.Keys(t, ShakeT, ShakeY, null));
    }

    // ---------- 待機 ----------

    void StartIdle()
    {
        StopIdle();
        if (watermark)
            idle.Add(T.Run(watermarkTime, Ease.InOutSine, t => watermark.anchoredPosition = watermarkPos + watermarkDrift * t)
                .SetLoops(-1, LoopType.Yoyo).SetLink(gameObject));
        PulseArrow();
        if (logo) logo.StartIdle();
    }

    void StopIdle()
    {
        foreach (var tw in idle) tw.Kill();
        idle.Clear();
        if (logo) logo.StopIdle();
    }

    //選択中の 矢印 が右へ小さく行き来する
    Tween arrowTween;
    void PulseArrow()
    {
        arrowTween?.Kill();
        foreach (var it in menu) if (it.arrow) it.arrow.rectTransform.anchoredPosition = it.arrowPos;
        if (menu.Count == 0 || !menu[selected].arrow) return;
        var a = menu[selected].arrow.rectTransform;
        var basePos = menu[selected].arrowPos;
        arrowTween = T.Run(0.5f, Ease.InOutSine, t => a.anchoredPosition = basePos + new Vector2(7.5f * t, 0f))
            .SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        idle.Add(arrowTween);
    }

    // ---------- えらぶ ----------

    public void Select(int index)
    {
        if (busy || menu.Count == 0) return;
        Move(Mathf.Clamp(index, 0, menu.Count - 1) - selected);
    }

    void Move(int dir)
    {
        if (dir == 0) return;
        int next = selected + dir;
        if (next < 0 || next >= menu.Count)
        {
            //端ではその場で小さく弾んで「これ以上ない」を伝える
            var edge = menu[selected];
            float d = Mathf.Sign(dir);
            T.Run(0.24f, Ease.OutQuad, t =>
            {
                float y = T.Keys(t, new[] { 0f, 0.33f, 0.66f, 1f }, new[] { 0f, d * 9f, -d * 4.5f, 0f }, null);
                edge.rect.anchoredPosition = edge.pos + new Vector2(0f, y);
            }).SetLink(gameObject);
            return;
        }
        next = Mathf.Clamp(next, 0, menu.Count - 1);
        selected = next;
        ApplySelection(true);
        PulseArrow();

        //上下の項目が押されてよろける
        foreach (int side in new[] { -1, 1 })
        {
            int k = next + side;
            if (k < 0 || k >= menu.Count) continue;
            var n = menu[k];
            float s = side;
            T.Run(0.3f, Ease.OutQuad, t =>
            {
                float x = T.Keys(t, JostleT, JostleX, null);
                float y = T.Keys(t, JostleT, new[] { 0f, -s * 10.5f, s * 3f, 0f }, null);
                float r = T.Keys(t, JostleT, new[] { 0f, -s * 1.2f, s * 0.4f, 0f }, null);
                n.rect.anchoredPosition = n.pos + new Vector2(x, y);
                n.rect.localEulerAngles = new Vector3(0, 0, r);
            }).SetLink(gameObject);
        }
        //選んだ項目はぐいっと前へ
        var chosen = menu[next];
        T.Run(0.26f, Ease.OutQuad, t =>
        {
            float x = T.Keys(t, new[] { 0f, 0.5f, 1f }, new[] { -12f, 9f, 0f }, null);
            chosen.rect.anchoredPosition = chosen.pos + new Vector2(x, 0f);
        }).SetLink(gameObject);
    }

    static readonly float[] JostleT = { 0f, 0.3f, 0.7f, 1f };
    static readonly float[] JostleX = { 0f, -15f, 3f, 0f };

    //色・幅・文字の位置を選択に合わせる
    void ApplySelection(bool animate)
    {
        for (int i = 0; i < menu.Count; i++)
        {
            var it = menu[i];
            bool on = i == selected;
            if (it.bg) it.bg.color = on ? bgOn : bgOff;
            if (it.label) it.label.color = on ? labelOn : labelOff;
            if (it.arrow)
            {
                it.arrow.enabled = on;
                it.arrow.color = labelOn;
            }

            float w = on ? widthOn : widthOff;
            float lx = on ? labelXOn : labelXOff;
            var rt = it.rect;
            var lt = it.label ? it.label.rectTransform : null;
            DOTween.Kill(rt);
            if (animate)
            {
                float fromW = rt.sizeDelta.x;
                float fromX = lt ? lt.anchoredPosition.x : 0f;
                T.Run(on ? 0.26f : 0.18f, on ? Ease.OutBack : Ease.OutCubic, t =>
                {
                    rt.sizeDelta = new Vector2(Mathf.LerpUnclamped(fromW, w, t), rt.sizeDelta.y);
                    if (lt) lt.anchoredPosition = new Vector2(Mathf.LerpUnclamped(fromX, lx, t), lt.anchoredPosition.y);
                }).SetTarget(rt).SetLink(rt.gameObject);
            }
            else
            {
                rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
                if (lt) lt.anchoredPosition = new Vector2(lx, lt.anchoredPosition.y);
            }
        }
    }

    // ---------- けってい ----------

    public void Decide()
    {
        if (busy || menu.Count == 0) return;
        busy = true;
        StopIdle();
        int index = selected;
        var el = menu[index];
        //押し込み＋白く光る
        T.Run(0.3f, Ease.OutQuad, t => el.rect.localScale = Vector3.one * T.Keys(t, new[] { 0f, 0.25f, 0.65f, 1f }, new[] { 1f, 0.93f, 1.03f, 1f }, null))
            .SetLink(gameObject);
        if (el.flash) T.Run(0.32f, Ease.OutQuad, t => SetAlpha(el.flash, Mathf.Lerp(0.85f, 0f, t))).SetLink(gameObject);

        flow?.Kill();
        flow = DOTween.Sequence().SetLink(gameObject);
        const float go = 0.28f;
        //ほかの項目 → 選んだ項目の順に左へはける
        for (int i = 0; i < menu.Count; i++)
        {
            var it = menu[i];
            float delay = it == el ? 0.16f : i * 0.04f;
            flow.Insert(go + delay, T.Run(0.26f, Ease.InCubic, t =>
            {
                it.rect.anchoredPosition = it.pos + new Vector2(Mathf.Lerp(0f, -690f, t), 0f);
                it.group.alpha = 1f - t;
            }));
        }
        if (logo) flow.InsertCallback(go, () => logo.PlayExit(0.18f));
        //幕が覆う
        if (wipeLabel && wipeSprites != null && index < wipeSprites.Length && wipeSprites[index])
        {
            wipeLabel.sprite = wipeSprites[index];
            wipeLabel.SetNativeSize();
        }
        float w = WipeWidth();
        if (wipe) flow.Insert(go + 0.42f, T.Run(0.42f, Ease.InOutCubic, t => wipe.anchoredPosition = new Vector2(Mathf.Lerp(-w, 0f, t), 0f)));
        flow.InsertCallback(go + 0.84f, () => Decided(index));
        if (replayAfterDecide)
        {
            if (wipe) flow.Insert(go + 0.84f + 1.7f, T.Run(0.42f, Ease.InOutCubic, t => wipe.anchoredPosition = new Vector2(Mathf.Lerp(0f, w, t), 0f)));
            flow.InsertCallback(go + 0.84f + 1.7f + 0.3f, PlayIntro);
        }
    }

    void Decided(int index)
    {
        Debug.Log($"[タイトル] 決定: {index} ({menu[index].rect.name})");
        onDecided.Invoke(index);
        if (index == 0) onStart.Invoke();
        else if (index == 1) onHowToPlay.Invoke();
        else if (index == 2) onQuit.Invoke();
    }

    float WipeWidth()
    {
        //幕は画面より少し大きく動かして、端が見えないようにする
        float w = wipe && wipe.rect.width > 0f ? wipe.rect.width : 1920f;
        return w * 1.05f;
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    // ---------- 入力(Input Actions) ----------

    void ReadInput(out int move, out bool ok)
    {
        move = 0;
        ok = submitAction != null && submitAction.action != null && submitAction.action.WasPressedThisFrame();

        //Navigate の上下を「1つずつ」の入力に変える(上 = 番号が小さい方)。押しっぱなしなら一定間隔でくり返す
        float y = navigateAction != null && navigateAction.action != null ? navigateAction.action.ReadValue<Vector2>().y : 0f;
        int dir = y > 0.5f ? -1 : (y < -0.5f ? 1 : 0);
        if (dir == 0)
        {
            heldDir = 0;
        }
        else if (dir != heldDir)
        {
            move = dir;
            heldDir = dir;
            repeatTimer = repeatDelay;
        }
        else
        {
            repeatTimer -= Time.unscaledDeltaTime;
            if (repeatTimer <= 0f)
            {
                move = dir;
                repeatTimer = repeatInterval;
            }
        }
    }

    public void GameStart()
    {
        UIChaosGatherTransition.Instance?.LoadScene("StageSelect");
    }

    public void GameQuit()
    {
        Application.Quit();
    }

}
