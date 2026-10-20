/**
 * File: GameData.cs
 * 内容：ゲームで保持する情報をまとめているSOData
 * 作成者：寺田
 */
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

[CreateAssetMenu(fileName = "GameFloawData", menuName = "Scriptable Objects/GameFloawData")]
public class GameData : ScriptableObject
{
    public struct ResultData
    {
        public float pastTime;
        public bool IsClear;
    };

    public enum PlayerType
    {
        Normal,
    };


    [SerializeField, Tooltip("ステージのデータ")]
    public List<StageData> StageDatas = new List<StageData>();

    [SerializeField, Tooltip("プレイヤーの種類データ")]
    public List<ScriptableObject> playerTypeDatas = new List<ScriptableObject>();

    private int _selectStageNo;
    public int SelectStageNo => _selectStageNo;

    private int _playerType = 0;

    private ResultData _resultData;
    public ResultData Result => _resultData;

    public void ChangeSelectNo(int val)
    {
        _selectStageNo += val;
    }

    public void ChangeSelectPlayer(int val)
    {
        _playerType += val;
    }

    public StageData GetSelectStageData()
    {
        StageDatas[_selectStageNo].PlayerData = playerTypeDatas[_playerType];
        return StageDatas[_selectStageNo];
    }

    public void ApplyResult(ResultData data)
    {
        _resultData = data;
    }

}
