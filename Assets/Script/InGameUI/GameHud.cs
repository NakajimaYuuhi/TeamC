using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//ゲーム中の画面の表示だけを担当する
//・残り時間・距離・仲間の人数・持っているアイテム・アイテムモードを、下の関数で渡すと画面に出る
//・ゲームの中身(時間を減らす、アイテムを拾う・投げる など)はここでは決めない。ゲーム側から呼ぶ
public class GameHud : MonoBehaviour
{
    [Header("整理券(左上)")]
    [Tooltip("整理券の親(TicketRoot)。アイテムモード中は少し薄くなる")]
    [SerializeField] CanvasGroup ticket;
    [Tooltip("アイテムモード中の整理券の濃さ")]
    [SerializeField, Range(0f, 1f)] float ticketAlphaInItemMode = 0.55f;
    [Tooltip("残り時間の数字(5つの欄: 百・十・一・点・十分の一)")]
    [SerializeField] HudDigits timeDigits;
    [Tooltip("距離の数字(4つの欄)")]
    [SerializeField] HudDigits distanceDigits;
    [Tooltip("距離の上限(m)")]
    [SerializeField] int maxDistance = 1000;
    [Tooltip("残り時間がこの秒数以下になると、数字が朱になる")]
    [SerializeField] float hurrySeconds = 10f;

    [Header("仲間(整理券のいちばん下の段)")]
    [Tooltip("仲間の人数の数字(3つの欄。999人まで)")]
    [SerializeField] HudDigits followerDigits;
    [Tooltip("人のアイコンを並べる箱(CrewBox)")]
    [SerializeField] HudFollowerIcons followerIcons;

    [Header("アイテム欄(右下)。0 が次に投げるもの(大きい枠)")]
    [Tooltip("各枠の親(ItemSlot0～2)。拾ったとき・投げたときに弾む")]
    [SerializeField] RectTransform[] slots = new RectTransform[3];
    [Tooltip("各枠のアイコンの Image")]
    [SerializeField] Image[] slotIcons = new Image[3];
    [Tooltip("何も持っていないときに大きい枠に出す「なし」")]
    [SerializeField] GameObject emptyLabel;

    [Header("説明の札(アイテム欄の上)")]
    [SerializeField] RectTransform card;
    [SerializeField] Image cardName;
    [SerializeField] Image cardDescription;

    [Header("操作の一覧(左下)")]
    [SerializeField] GameObject hintNormal;
    [SerializeField] GameObject hintItemMode;

    [Header("アイテムモード")]
    [Tooltip("画面のふちの山吹の枠(ModeFrame)")]
    [SerializeField] GameObject modeFrame;
    [Tooltip("上の真ん中の「アイテムモード」の札(ModeTag)")]
    [SerializeField] RectTransform modeTag;
    [Tooltip("大きい枠の左の「ホイールで切り替え」(SwitchHint)。アイテムモード中、2つ以上持っているときだけ出す")]
    [SerializeField] GameObject switchHint;
    [Tooltip("アイテムモード中に、大きい枠と説明の札を浮かせる量(px)")]
    [SerializeField] float liftInItemMode = 15f;

    [Header("数字の色(数字の画像は白。ここで色を付ける)")]
    [SerializeField] Color digitNormal = new Color32(0x1E, 0x1B, 0x26, 0xFF);
    [SerializeField] Color digitHurry = new Color32(0xE5, 0x43, 0x2E, 0xFF);

    public const int Capacity = 3;
    public bool ItemMode => itemMode;
    public int ItemCount => items.Count;

    readonly List<HudItemData> items = new List<HudItemData>();
    bool itemMode;
    bool wasHurry, colored;
    Vector2 slot0Pos, cardPos, tagPos;
    bool ready;

    void Awake() => Init();

    void Init()
    {
        if (ready) return;
        ready = true;
        if (slots.Length > 0 && slots[0]) slot0Pos = slots[0].anchoredPosition;
        if (card) cardPos = card.anchoredPosition;
        if (modeTag) tagPos = modeTag.anchoredPosition;
        //数字の画像は白。色はここで付ける
        if (timeDigits) SetDigitColor(timeDigits, digitNormal);
        if (distanceDigits) SetDigitColor(distanceDigits, digitNormal);
        if (followerDigits) SetDigitColor(followerDigits, digitNormal);
        SetFollowers(0);
        ApplyItemMode(false, false);
        RefreshItems();
    }

    // ---------- 整理券 ----------

    //残り時間(秒)。小数1けたで出す(999.9 まで)
    public void SetTime(float seconds)
    {
        float s = Mathf.Clamp(seconds, 0f, 999.9f);
        if (timeDigits)
        {
            timeDigits.SetText(s.ToString("0.0"));
            bool hurry = s <= hurrySeconds;
            if (hurry != wasHurry || !colored)
            {
                SetDigitColor(timeDigits, hurry ? digitHurry : digitNormal);
                wasHurry = hurry;
                colored = true;
            }
        }
    }

    //ゴールまでの距離(m)。0～maxDistance
    public void SetDistance(float meters)
    {
        int m = Mathf.Clamp(Mathf.RoundToInt(meters), 0, maxDistance);
        if (distanceDigits) distanceDigits.SetText(m.ToString());
    }

    //ついてきている人の人数。数字と人のアイコンで出す
    public void SetFollowers(int count)
    {
        int n = Mathf.Clamp(count, 0, 999);
        if (followerDigits) followerDigits.SetText(n.ToString());
        if (followerIcons) followerIcons.SetCount(n);
    }

    static void SetDigitColor(HudDigits d, Color c)
    {
        foreach (var img in d.GetComponentsInChildren<Image>(true)) img.color = c;
    }

    // ---------- アイテム ----------

    //拾ったとき。いっぱい(3つ)なら false を返して何もしない
    public bool AddItem(HudItemData item)
    {
        Init();
        if (item == null || items.Count >= Capacity) return false;
        items.Add(item);
        RefreshItems();
        Punch(slots[items.Count - 1], 0.15f);
        return true;
    }

    //投げたとき。次に投げるもの(大きい枠)を取り出す。持っていなければ null
    public HudItemData TakeItem()
    {
        Init();
        if (items.Count == 0) return null;
        var first = items[0];
        items.RemoveAt(0);
        RefreshItems();
        Punch(slots[0], -0.1f);
        return first;
    }

    //持ち物をまとめて入れ替える(ステージ開始時など)
    public void SetItems(IList<HudItemData> list)
    {
        Init();
        items.Clear();
        if (list != null)
            for (int i = 0; i < list.Count && items.Count < Capacity; i++)
                if (list[i] != null) items.Add(list[i]);
        RefreshItems();
    }

    //次に投げるアイテムを切り替えたとき。並びを入れ替えて、大きい枠と説明の札を弾ませる
    public void SwitchItems(IList<HudItemData> list)
    {
        SetItems(list);
        Punch(slots.Length > 0 ? slots[0] : null, 0.15f);
        Punch(card, 0.06f);
    }
    //アイテムが無いときに使おうとしたら、欄を揺らして知らせる
    public void ShakeEmpty()
    {
        if (!slots[0]) return;
        var rt = slots[0];
        rt.DOComplete();
        //DOTween の UI モジュールを使わずに揺らす
        DOTween.Shake(() => (Vector3)rt.anchoredPosition, v => rt.anchoredPosition = v, 0.26f, new Vector3(10f, 0f, 0f), 18, 0f)
            .SetTarget(rt).SetLink(rt.gameObject);
    }

    void RefreshItems()
    {
        for (int i = 0; i < slotIcons.Length; i++)
        {
            var icon = slotIcons[i];
            if (!icon) continue;
            bool has = i < items.Count && items[i].icon;
            icon.enabled = has;
            icon.sprite = has ? items[i].icon : null;
            if (has) icon.preserveAspect = true;
        }
        if (emptyLabel) emptyLabel.SetActive(items.Count == 0);
        RefreshSwitchHint();

        //説明の札は、次に投げるものの名前と説明。持っていなければ札ごと隠す
        bool show = items.Count > 0;
        if (card) card.gameObject.SetActive(show);
        if (show)
        {
            SetImage(cardName, items[0].nameImage);
            SetImage(cardDescription, items[0].descriptionImage);
        }
    }

    static void SetImage(Image img, Sprite s)
    {
        if (!img) return;
        img.enabled = s != null;
        img.sprite = s;
        if (s) img.SetNativeSize();
    }

    //枠を弾ませる。amount がプラスなら膨らみ、マイナスならへこむ
    static void Punch(RectTransform rt, float amount)
    {
        if (!rt) return;
        rt.DOComplete();
        rt.DOPunchScale(Vector3.one * amount, 0.25f, 6, 0.8f).SetLink(rt.gameObject);
    }

    // ---------- アイテムモード ----------

    public void SetItemMode(bool on) => ApplyItemMode(on, true);

    //切り替えの案内は、アイテムモード中で切り替える相手がいる(2つ以上)ときだけ
    void RefreshSwitchHint()
    {
        if (switchHint) switchHint.SetActive(itemMode && items.Count >= 2);
    }

    void ApplyItemMode(bool on, bool animate)
    {
        itemMode = on;
        if (modeFrame) modeFrame.SetActive(on);
        if (hintNormal) hintNormal.SetActive(!on);
        if (hintItemMode) hintItemMode.SetActive(on);
        RefreshSwitchHint();

        if (ticket)
        {
            DOTween.Kill(ticket);
            float a = on ? ticketAlphaInItemMode : 1f;
            if (animate) DOTween.To(() => ticket.alpha, v => ticket.alpha = v, a, 0.2f).SetTarget(ticket).SetLink(ticket.gameObject);
            else ticket.alpha = a;
        }

        float lift = on ? liftInItemMode : 0f;
        Move(slots.Length > 0 ? slots[0] : null, slot0Pos + new Vector2(0f, lift), animate);
        Move(card, cardPos + new Vector2(0f, lift), animate);

        if (modeTag)
        {
            modeTag.gameObject.SetActive(on);
            if (on && animate)
            {
                //上からぽんと出る
                modeTag.DOKill();
                modeTag.anchoredPosition = tagPos + new Vector2(0f, 45f);
                DOTween.To(() => modeTag.anchoredPosition, v => modeTag.anchoredPosition = v, tagPos, 0.22f)
                    .SetEase(Ease.OutBack).SetTarget(modeTag).SetLink(modeTag.gameObject);
            }
            else modeTag.anchoredPosition = tagPos;
        }
    }

    static void Move(RectTransform rt, Vector2 to, bool animate)
    {
        if (!rt) return;
        DOTween.Kill(rt, false);
        if (animate)
            DOTween.To(() => rt.anchoredPosition, v => rt.anchoredPosition = v, to, 0.18f)
                .SetEase(Ease.OutCubic).SetTarget(rt).SetLink(rt.gameObject);
        else rt.anchoredPosition = to;
    }
}
