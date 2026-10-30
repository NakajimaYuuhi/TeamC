using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using static ResultTween;

//リザルト画面
//GameData のステージ番号とクリアタイムを読み、演出つきで表示する
public class ResultScreen : ResultScreenBase
{
    //流れに乗った人数。GameData に欄がないため GameFlowManager が入れる
    public static int LastFollowers;

    [Header("データ")]
    [SerializeField, Tooltip("ゲームのデータ。ステージ番号とクリアタイムをここから読む。空ならテストの値を使う")]
    GameData gameData;
    [SerializeField, Tooltip("ランクの境目")]
    ResultRankTable rankTable;

    [Header("整理券")]
    [SerializeField] RectTransform ticket;
    [SerializeField, Tooltip("STAGE n CLEAR の Image")]
    Image clearLabel;
    [SerializeField, Tooltip("result_clear_1～5 を順番に")]
    Sprite[] clearSprites;
    [SerializeField, Tooltip("ステージ名の Image")]
    Image stageName;
    [SerializeField, Tooltip("result_name_1～5 を順番に")]
    Sprite[] nameSprites;
    [SerializeField, Tooltip("券の中で順に出すもの。並べた順に出る")]
    ResultPopItem[] popItems;
    [SerializeField, Tooltip("タイムの数字。5欄、右そろえ")]
    HudDigits timeDigits;
    [SerializeField, Tooltip("タイムの数字と秒の親。空いた欄のぶん左へ詰める")]
    RectTransform timeRoot;
    [SerializeField, Tooltip("タイムの数字1欄の幅")]
    float timeSlotWidth = 114f;
    [SerializeField, Tooltip("流れに乗った人数。3欄、左そろえ")]
    HudDigits flowDigits;

    [Header("ハンコ")]
    [SerializeField] Image stamp;
    [SerializeField, Tooltip("result_stamp_S, A, B, C の順")]
    Sprite[] stampSprites = new Sprite[4];
    [SerializeField, Tooltip("インクのしぶき。複製して使うので非表示にしておく")]
    Image inkDot;
    [SerializeField, Tooltip("ランクの色。S, A, B, C の順")]
    Color[] rankColors =
    {
        new Color32(0xD9, 0x9A, 0x00, 0xFF),
        new Color32(0xD6, 0x33, 0x7E, 0xFF),
        new Color32(0x2F, 0x7F, 0xD8, 0xFF),
        new Color32(0x8A, 0x8F, 0x9E, 0xFF),
    };

    [Header("次のランクまで")]
    [SerializeField] Image nextTag;
    [SerializeField, Tooltip("result_next_S, A, B の順")]
    Sprite[] nextSprites = new Sprite[3];
    [SerializeField, Tooltip("S のときに出す result_best")]
    Sprite bestSprite;
    [SerializeField, Tooltip("あと何秒かの数字。4欄、右そろえ、99.9 まで")]
    HudDigits nextDigits;

    [Header("GameData が空のときのテスト値")]
    [SerializeField] int testStage = 0;
    [SerializeField] float testTime = 52.1f;
    [SerializeField] int testFlow = 14;

    //ランクごとのハンコの傾き
    static readonly float[] StampAngle = { 12f, 8f, -6f, -18f };

    readonly List<GameObject> dots = new List<GameObject>();
    Vector2 ticketPos, tagPos, timeRootPos;
    Vector2[] popPos;

    protected override void Awake()
    {
        base.Awake();
        //境目が未設定なら仮の境目を使う
        if (rankTable == null) rankTable = ScriptableObject.CreateInstance<ResultRankTable>();
        ticketPos = ticket.anchoredPosition;
        tagPos = nextTag.rectTransform.anchoredPosition;
        timeRootPos = timeRoot.anchoredPosition;
        popPos = new Vector2[popItems.Length];
        for (int i = 0; i < popItems.Length; i++) popPos[i] = popItems[i].target.anchoredPosition;
        if (inkDot != null) inkDot.gameObject.SetActive(false);
    }

    void Start()
    {
        if (gameData != null)
        {
            //ステージセレクトで次のステージを解放するため、クリアを記録する
            StageProgress.MarkCleared(gameData.SelectStageNo);
            //ステージセレクトに出す、いちばんいい記録を残す
            float t = gameData.Result.pastTime;
            StageProgress.SaveBest(gameData.SelectStageNo, t, rankTable.GetRank(gameData.SelectStageNo, t));
            Show(gameData.SelectStageNo, gameData.Result.pastTime, LastFollowers);
        }
        else Show(testStage, testTime, testFlow);
    }

    /// <summary>リザルトを表示する。stage は 0 始まり、time は秒</summary>
    public void Show(int stage, float time, int followers)
    {
        BeginSequence();
        foreach (var d in dots) Destroy(d);
        dots.Clear();

        int rank = rankTable.GetRank(stage, time);
        SaveRank(rank);

        if (clearSprites != null && stage < clearSprites.Length) clearLabel.sprite = clearSprites[stage];
        if (nameSprites != null && stage < nameSprites.Length)
        {
            stageName.sprite = nameSprites[stage];
            stageName.SetNativeSize();
        }

        InsertTicketDrop(seq, ticket, ticketPos);
        InsertPops(seq, popItems, popPos, 0.42f);

        //タイムと人数を数え上げる
        SetTime(0f);
        seq.Insert(0.76f, DOTween.To(() => 0f, SetTime, time, 0.8f).SetEase(Ease.OutCubic));
        flowDigits.SetText("0");
        if (followers > 0)
            seq.Insert(1.12f, DOTween.To(() => 0, v => flowDigits.SetText(v.ToString()), followers, followers * 0.03f).SetEase(Ease.Linear));

        //ハンコを押す
        stamp.sprite = stampSprites[rank];
        InsertStamp(seq, stamp.rectTransform, StampAngle[rank], 1.70f);
        seq.Insert(1.91f, Shake(ticket, 0.26f, rank == 0 ? 30f : 18f));
        InsertInkDots(stamp.rectTransform.anchoredPosition, rankColors[rank], 1.93f);

        //ひとつ上のランクまでの秒数。S は最高ランクの札
        SetNextTag(stage, rank, time);
        InsertRise(seq, nextTag.rectTransform, tagPos, 2.15f);

        InsertButtons(2.45f);
    }

    void SetTime(float t)
    {
        string s = Mathf.Min(t, 999.9f).ToString("0.0", CultureInfo.InvariantCulture);
        timeDigits.SetText(s);
        //右そろえで空いた欄のぶん左へ詰める
        int empty = Mathf.Max(0, 5 - s.Length);
        timeRoot.anchoredPosition = timeRootPos - new Vector2(empty * timeSlotWidth, 0f);
    }

    void SetNextTag(int stage, int rank, float time)
    {
        bool best = rank == 0;
        nextTag.sprite = best ? bestSprite : nextSprites[rank - 1];
        nextTag.SetNativeSize();
        nextDigits.gameObject.SetActive(!best);
        if (best) return;
        float left = Mathf.Clamp(time - rankTable.GetLimit(stage, rank - 1), 0.1f, 99.9f);
        nextDigits.SetText(left.ToString("0.0", CultureInfo.InvariantCulture));
        SetColor(nextDigits, rankColors[rank - 1]);
    }

    //ハンコのまわりにインクを散らす
    void InsertInkDots(Vector2 center, Color color, float delay)
    {
        if (inkDot == null) return;
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f, r = 276f + (i % 3) * 42f, size = 18f + (i % 3) * 9f;
            var dot = Instantiate(inkDot, inkDot.transform.parent);
            dot.gameObject.SetActive(true);
            dot.color = color;
            var rt = dot.rectTransform;
            rt.anchoredPosition = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            rt.sizeDelta = new Vector2(size, size);
            rt.localScale = Vector3.zero;
            seq.Insert(delay + i * 0.015f, rt.DOScale(1f, 0.16f));
            dots.Add(dot.gameObject);
        }
    }

    void SaveRank(int rank)
    {
        if (gameData == null) return;
        var r = gameData.Result;
        r.rank = (GameData.ScoreRank)rank;
        gameData.ApplyResult(r);
    }
}
