using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StageData", menuName = "Scriptable Objects/StageData")]
public class StageData : ScriptableObject
{
    public enum StageType
    {
        Booth,
        Train,
        Elevator,
        Koubai,
    };

    [Header("====== 参照 ======")]
    [SerializeField, Tooltip("ステージを構成するデータ")]
    public ScriptableObject MapData;

    [SerializeField, Tooltip("プレイヤーのパラメータ")]
    public ScriptableObject PlayerData;

    [SerializeField, Tooltip("存在する敵の種類リスト")]
    public List<ScriptableObject> EnemyDetas = new List<ScriptableObject>();

    [SerializeField, Tooltip("ブースステージのマップデータ")]
    public List<ScriptableObject> PurposeDatas = new List<ScriptableObject>();

    [Header("===== ステージの設定 =====")]
    [SerializeField,Tooltip("ステージの種類")]
    public StageType Type = StageType.Booth;

    [SerializeField, Tooltip("一回の目的の時間")]
    public float FailedTime = 10.0f;

    [SerializeField, Tooltip("警備員の人数")]
    public int _securityCount = 3;

    [Header("====== ステージ進行データ =======")]

    [SerializeField, Tooltip("一回でクリアではない場合何回でクリアになるか(0で無限)")]
    public int ClearCount = 1;

    [SerializeField, Tooltip("ベースの敵の数")]
    public int EnemyCount = 1000;

    [SerializeField, Tooltip("難易度でかける倍率")]
    [Range(1.0f,1.5f)] public float DiffMulti;


    [SerializeField, Tooltip("回数ごとに人が増える倍率")]
    [Min(0.0f)] public float PhaseMulti = 0.0f;


    public ScriptableObject PurposePick()
    {
        return PurposeDatas[Random.Range(0,PurposeDatas.Count)];
    }
}
