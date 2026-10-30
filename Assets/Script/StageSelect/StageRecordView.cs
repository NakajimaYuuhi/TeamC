using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

//ステージセレクトのカードに、そのステージのいちばんいいランクとクリアタイムを出す
//まだクリアしていなければ、ハンコを消して「まだ記録なし」にする
public class StageRecordView : MonoBehaviour
{
    [Tooltip("ステージの番号。0 から")]
    [SerializeField] int stage;
    [Tooltip("ランクのハンコ")]
    [SerializeField] Image stamp;
    [Tooltip("ハンコの画像。S A B C の順")]
    [SerializeField] Sprite[] stampSprites = new Sprite[4];
    [Tooltip("「ベスト」か「まだ記録なし」を出す所")]
    [SerializeField] Image label;
    [SerializeField] Sprite bestSprite;
    [SerializeField] Sprite noneSprite;
    [Tooltip("タイムの数字")]
    [SerializeField] HudDigits time;
    [Tooltip("数字のうしろの「秒」")]
    [SerializeField] Image byo;

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        bool has = StageProgress.TryGetBest(stage, out float t, out int rank);
        if (stamp)
        {
            stamp.enabled = has && rank >= 0 && rank < stampSprites.Length;
            if (stamp.enabled) stamp.sprite = stampSprites[rank];
        }
        if (label)
        {
            label.sprite = has ? bestSprite : noneSprite;
            label.SetNativeSize();
        }
        if (time)
        {
            time.gameObject.SetActive(has);
            if (has) time.SetText(Mathf.Min(t, 999.9f).ToString("0.0", CultureInfo.InvariantCulture));
        }
        if (byo) byo.enabled = has;
    }
}
