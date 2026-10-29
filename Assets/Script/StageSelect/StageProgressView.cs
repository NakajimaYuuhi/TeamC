using UnityEngine;

//ステージセレクトの右上のクリア数を、保存したクリア状況から出す
//テスト用に、クリアの記録を消すメニューも持つ
public class StageProgressView : MonoBehaviour
{
    [Tooltip("右上のクリア数")]
    [SerializeField] ClearCountImage clearCount;
    [Tooltip("ステージの数")]
    [SerializeField] int totalStages = 5;

    void Start()
    {
        if (clearCount != null) clearCount.Cleared = StageProgress.ClearedCount(totalStages);
    }

    [ContextMenu("クリアの記録を消す")]
    void ResetProgress()
    {
        StageProgress.ResetAll(totalStages);
        Debug.Log("[ステージセレクト] クリアの記録を消しました");
    }

    [ContextMenu("すべてクリアにする")]
    void ClearAll()
    {
        for (int i = 0; i < totalStages; i++) StageProgress.MarkCleared(i);
        Debug.Log("[ステージセレクト] すべてクリアにしました");
    }
}
