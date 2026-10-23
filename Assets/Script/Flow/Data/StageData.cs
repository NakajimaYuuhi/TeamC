/**
 * File: StageData.cs
 * 内容：ステージの情報をまとめているSOData
 * 作成者：寺田
 */
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
    public NpcSpawnAreaData MapData;

    [SerializeField, Tooltip("プレイヤーのパラメータ")]
    public PlayerData PlayerData;

    [SerializeField, Tooltip("存在する敵の種類リスト")]
    public List<ScriptableObject> EnemyDetas = new List<ScriptableObject>();

    [SerializeField, Tooltip("ブースステージのマップデータ")]
    public List<DestinationData> PurposeDatas = new List<DestinationData>();

    [Header("===== ステージの設定 =====")]
    [SerializeField, Tooltip("プレイヤーの初期位置")]
    public Vector3 PlayerInitPos;

    [SerializeField,Tooltip("ステージの種類")]
    public StageType Type = StageType.Booth;

    [SerializeField, Tooltip("一回の目的の時間")]
    public float FailedTime = 10.0f;

    [SerializeField, Tooltip("警備員の人数")]
    public int _securityCount = 3;

    [Header("====== ステージ進行データ =======")]

    [SerializeField, Tooltip("失敗条件はタイマーか")]
    public bool IsTimerFailed = true;

    [SerializeField,Tooltip("スポナーベースでNPCをスポーンするか否か")]
    public bool IsSpawnerBase = false;

    [SerializeField, Tooltip("一回でクリアではない場合何回でクリアになるか(0で無限)")]
    public int ClearCount = 1;

    [SerializeField, Tooltip("ベースの敵の数")]
    public int EnemyCount = 1000;

    [SerializeField, Tooltip("難易度でかける倍率")]
    [Range(1.0f,1.5f)] public float DiffMulti;


    [SerializeField, Tooltip("回数ごとに人が増える倍率")]
    [Min(0.0f)] public float PhaseMulti = 0.0f;

    private int lastIdx = -99;
    public DestinationData PurposePick()
    {
        int currentIdx = 0;
        if (PurposeDatas.Count > 1)
        {
            do
            {
                currentIdx = Random.Range(0, PurposeDatas.Count);
            } while (currentIdx == lastIdx);
            lastIdx = currentIdx;
        }
        return PurposeDatas[currentIdx];
    }
}
