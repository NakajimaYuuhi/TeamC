/**
 * File: GameData.cs
 * 内容：ゲームで保持する情報をまとめているSOData
 * 作成者：寺田
 */
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GameFloawData", menuName = "Scriptable Objects/GameFloawData")]
public class GameData : ScriptableObject
{
    public struct ResultData
    {
        public float pastTime;
    };

    public enum PlayerType
    {
        Normal,
    };


    [SerializeField, Tooltip("ステージのデータ")]
    public List<StageData> StageDatas = new List<StageData>();

    //[SerializeField, Tooltip("プレイヤーの種類データ")]
    //public List<PlayerTypeData> playerTypeDatas = new List<PlayerTypeData>();

    private int _selectStageNo;
    public int SelectStageNo => _selectStageNo;

    private int _playerType = 0;

    private ResultData _resultData;

    public void ChangeSelectNo(int val)
    {
        _selectStageNo += val;
    }

    public StageData GetSelectStageData()
    {
        return StageDatas[SelectStageNo];
    }

    public void ApplyResult(ResultData data)
    {
        _resultData = data;
    }

}
