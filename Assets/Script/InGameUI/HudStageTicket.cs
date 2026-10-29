using UnityEngine;
using UnityEngine.UI;

//整理券のステージ番号、ステージ名、目標を、遊んでいるステージに合わせて切り替える
public class HudStageTicket : MonoBehaviour
{
    [Tooltip("選んだステージの番号を読む")]
    [SerializeField] GameData gameData;
    [SerializeField] Image stageLabel;
    [Tooltip("ticket_stage_1～5 を順番に")]
    [SerializeField] Sprite[] stageSprites;
    [SerializeField] Image stageName;
    [Tooltip("ticket_name_1～5 を順番に")]
    [SerializeField] Sprite[] nameSprites;
    [SerializeField] Image goal;
    [Tooltip("ticket_goal_1～5 を順番に")]
    [SerializeField] Sprite[] goalSprites;

    void Start()
    {
        if (gameData == null) return;
        int stage = gameData.SelectStageNo;
        Set(stageLabel, stageSprites, stage);
        Set(stageName, nameSprites, stage);
        Set(goal, goalSprites, stage);
    }

    static void Set(Image img, Sprite[] sprites, int i)
    {
        if (img == null || sprites == null || i < 0 || i >= sprites.Length || sprites[i] == null) return;
        img.sprite = sprites[i];
        img.SetNativeSize();
    }
}
