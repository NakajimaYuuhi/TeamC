using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using static ResultTween;

//ゲームオーバー画面
//のこり時間が 0 になり、時間切れのハンコを押して、ゴールまでの距離を出す
public class GameOverScreen : ResultScreenBase
{
    //ゴールまでの距離。GameData に欄がないため GameFlowManager が入れる
    public static float LastDistance;

    [Header("データ")]
    [SerializeField, Tooltip("ゲームのデータ。ステージ番号をここから読む。空ならテストの値を使う")]
    GameData gameData;

    [Header("整理券")]
    [SerializeField] RectTransform ticket;
    [SerializeField, Tooltip("STAGE n の Image")]
    Image stageLabel;
    [SerializeField, Tooltip("go_stage_1～5 を順番に")]
    Sprite[] stageSprites;
    [SerializeField, Tooltip("ステージ名の Image")]
    Image stageName;
    [SerializeField, Tooltip("result_name_1～5 を順番に")]
    Sprite[] nameSprites;
    [SerializeField, Tooltip("券の中で順に出すもの。並べた順に出る")]
    ResultPopItem[] popItems;
    [SerializeField, Tooltip("のこり時間の数字。5欄、右そろえ")]
    HudDigits timeDigits;
    [SerializeField, Tooltip("のこり時間の数字と秒の親。空いた欄のぶん左へ詰める")]
    RectTransform timeRoot;
    [SerializeField, Tooltip("数字1欄の幅")]
    float timeSlotWidth = 114f;
    [SerializeField, Tooltip("この秒数から 0 まで減らして見せる")]
    float countFrom = 3f;
    [SerializeField, Tooltip("0 になったあとの数字の色")]
    Color timeUpColor = new Color32(0xE5, 0x43, 0x2E, 0xFF);
    [SerializeField, Tooltip("流れに乗った人数。3欄、左そろえ")]
    HudDigits flowDigits;

    [Header("時間切れのハンコ")]
    [SerializeField] Image stamp;
    [SerializeField, Tooltip("押したあとの傾き")]
    float stampAngle = 12f;

    [Header("ゴールまでの距離")]
    [SerializeField] Image distTag;
    [SerializeField, Tooltip("距離の数字。4欄、右そろえ、9999 まで")]
    HudDigits distDigits;
    [SerializeField, Tooltip("距離の数字の色")]
    Color distColor = new Color32(0xFF, 0xCF, 0x3F, 0xFF);

    [Header("GameData が空のときのテスト値")]
    [SerializeField] int testStage = 0;
    [SerializeField] int testFlow = 14;
    [SerializeField] float testDistance = 128f;

    Vector2 ticketPos, tagPos, timeRootPos;
    Vector2[] popPos;
    Color timeColor;

    protected override void Awake()
    {
        base.Awake();
        ticketPos = ticket.anchoredPosition;
        tagPos = distTag.rectTransform.anchoredPosition;
        timeRootPos = timeRoot.anchoredPosition;
        popPos = new Vector2[popItems.Length];
        for (int i = 0; i < popItems.Length; i++) popPos[i] = popItems[i].target.anchoredPosition;
        var first = timeDigits.GetComponentInChildren<Image>(true);
        timeColor = first != null ? first.color : Color.black;
    }

    void Start()
    {
        if (gameData != null) Show(gameData.SelectStageNo, ResultScreen.LastFollowers, LastDistance);
        else Show(testStage, testFlow, testDistance);
    }

    /// <summary>ゲームオーバーを表示する。stage は 0 始まり、distance は m</summary>
    public void Show(int stage, int followers, float distance)
    {
        BeginSequence();

        if (stageSprites != null && stage < stageSprites.Length) stageLabel.sprite = stageSprites[stage];
        if (nameSprites != null && stage < nameSprites.Length)
        {
            stageName.sprite = nameSprites[stage];
            stageName.SetNativeSize();
        }
        flowDigits.SetText(Mathf.Max(0, followers).ToString());
        distDigits.SetText(Mathf.Clamp(Mathf.CeilToInt(distance), 0, 9999).ToString());
        SetColor(distDigits, distColor);

        InsertTicketDrop(seq, ticket, ticketPos);
        InsertPops(seq, popItems, popPos, 0.42f);

        //のこり時間を 0 まで減らし、赤くする
        SetColor(timeDigits, timeColor);
        SetTime(countFrom);
        seq.Insert(0.5f, DOTween.To(() => countFrom, SetTime, 0f, 0.7f).SetEase(Ease.Linear));
        seq.InsertCallback(1.2f, () => SetColor(timeDigits, timeUpColor));

        //時間切れのハンコを押す
        InsertStamp(seq, stamp.rectTransform, stampAngle, 1.35f);
        seq.Insert(1.56f, Shake(ticket, 0.26f, 24f));

        //券がしょんぼり傾く
        seq.Insert(1.85f, Move(ticket, ticketPos + new Vector2(0f, -30f), 0.6f).SetEase(Ease.OutBack));
        seq.Insert(1.85f, ticket.DOLocalRotate(new Vector3(0f, 0f, -4f), 0.6f).SetEase(Ease.OutBack));

        InsertRise(seq, distTag.rectTransform, tagPos, 2.1f);
        InsertButtons(2.4f);
    }

    void SetTime(float t)
    {
        string s = Mathf.Clamp(t, 0f, 999.9f).ToString("0.0", CultureInfo.InvariantCulture);
        timeDigits.SetText(s);
        int empty = Mathf.Max(0, 5 - s.Length);
        timeRoot.anchoredPosition = timeRootPos - new Vector2(empty * timeSlotWidth, 0f);
    }
}
