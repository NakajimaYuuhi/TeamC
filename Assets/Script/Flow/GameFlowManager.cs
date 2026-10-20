/**
 * File:GameFlowManager.cs
 * 
 * 内容：ゲームの流れを管理し、必要オブジェクトの生成などを行う
 * 
 * 作成者:寺田
 */
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    enum FlowState
    {
        PurposeView, // 目的提示
        Execution, // 目的遂行
        Track, // 警備員から逃げる
        ResultView, // 結果表示
    };


    [Header("===== 参照 ======")]
    [SerializeField, Tooltip("プレイヤーのプレハブ")]
    private GameObject _playerPrefab;
    private GameObject _player;

    [SerializeField, Tooltip("敵のプレイハブ")]
    private GameObject _enemyPrefab;

    [SerializeField, Tooltip("UIのプレハブ")]
    private GameObject _uiPrefab;
    private GameObject _ui;

    [SerializeField, Tooltip("警備員のプレハブ")]
    private GameObject _securityPrefab;

    [SerializeField, Tooltip("リザルトデータ")]
    private ScriptableObject _reusltData;

    [SerializeField,Tooltip("ゲームのデータ")]
    private GameData _gameData;

    [Header("===== フロー管理変数 =====")]
    [SerializeField, Tooltip("目的地の名前を表示する時間")]
    private float _purposeViewTime;

    [SerializeField, Tooltip("迷惑率が減少する速度")]
    private float _annoyanceDecrease = 3.0f;

    private ScriptableObject _purposeData;
    private FlowState _flowState = FlowState.PurposeView;

    // 迷惑度
    private float _annoyanceLevel;
    private StageData _stageData;

    // SOから参照
    private float _failedTime; // 失敗するまでの時間
    private float _currentTimer; // 現在の経過時間

    private int _purposeTimes; // 目的の個数
    private int _currentPurposeTimes; // 現在の目的達成数
    private bool _isClear;
   
    void Awake()
    {
        // プレイヤー生成
        _player = _playerPrefab ? Instantiate(_playerPrefab) : null;

        _ui = _uiPrefab ? Instantiate(_uiPrefab) : null;

        Initialize(_gameData.GetSelectStageData());
    }

    
    void Update()
    {
        _currentTimer += Time.deltaTime;

        switch (_flowState)
        {
            case FlowState.PurposeView:
                PurposeView();
                break;
            case FlowState.Execution:
                Execution();
                break;
            case FlowState.Track:
                Track();
                break;
            case FlowState.ResultView:
                break;
        }
    }

    /// <summary>
    /// SOから受け取ったデータを変数に流す
    /// </summary>
    /// <param name="data"></param>
    public void Initialize(StageData data)
    {
        // プレイヤーにデータ配布
        // auto playerComp = _player?.GetComponent<>();
        //playerComp?.Initialize(data.PlayerData);

        _stageData = data;
        _purposeTimes = data.ClearCount;
        SetPurpose();

        // 敵生成
    }

    private void PurposeView()
    {
        if(_currentTimer > _purposeViewTime)
        {
            _flowState = FlowState.Execution;
        }
    }

    private void Execution()
    {
        // if(_purposeData.目的地にいるかの判定) CollectPurpose();

        if (_annoyanceLevel >= 1.0f)
        {
            _flowState = FlowState.Track;
            // プレイヤーを追跡状態にする
        }

        if (_currentTimer > _failedTime)
        {
            ToResult(false);
        }
    }


    private void Track()
    {
        // if(_purposeData.目的地にいるかの判定) CollectPurpose();
        _annoyanceLevel -= Time.deltaTime / _annoyanceDecrease;
        if(_annoyanceLevel <= 0.0f)
        {
            // プレイヤーを元の状態に戻す

            _flowState = FlowState.Execution;
        }

        if(_currentTimer < _failedTime)
        {
            ToResult(false);
        }
    }

    public void SetPurpose()
    {
        _purposeData = _stageData.PurposePick();

        // auto playerComp = _player?.GetComponent<>();
        // playerComp?.SetPurpose(_purposeData.座標のやつ);
    }

    public void CollectPurpose()
    {
        if (++_currentPurposeTimes >= _purposeTimes)
        {
            ToResult(true);
        }
        else
        {
            _flowState = FlowState.PurposeView;
            SetPurpose();
        }

    }

    public void AddAnnoyanceLevel(float val)
    {
        _annoyanceLevel += val;
        if(_annoyanceLevel > 1.0f)
        {
            _flowState = FlowState.Track;
            _annoyanceLevel = 1.0f;
        }
    }


    public void ToResult(bool clear)
    {
        _flowState = FlowState.ResultView;
        _isClear = clear;
        
        SceneManager.LoadScene("Result", LoadSceneMode.Additive);
    }
}
