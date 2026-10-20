using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "GameFloawData", menuName = "Scriptable Objects/GameFloawData")]
public class GameData : ScriptableObject
{
    public struct ResultData
    {
        public float pastTime;
    };


    [SerializeField, Tooltip("ステージのデータ")]
    public List<StageData> StageDatas = new List<StageData>();

    private int _selectStageNo;
    public int SelectStageNo => _selectStageNo;

    private ResultData _reusltData;

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
