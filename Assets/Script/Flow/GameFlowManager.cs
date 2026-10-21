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
using System.Data.Common;

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

    [SerializeField,Tooltip("ゲームのデータ")]
    private GameData _gameData;

    [SerializeField, Tooltip("目的地の表示")]
    private GameObject _purposeViewPrefab;
    private GameObject _purposeView;

    [SerializeField, Tooltip("Canvass")]
    private GameObject _canvas;

    [Header("===== フロー管理変数 =====")]
    [SerializeField, Tooltip("目的地の名前を表示する時間")]
    private float _purposeViewTime;

    [SerializeField, Tooltip("迷惑率が減少する速度")]
    private float _annoyanceDecrease = 3.0f;

    private DestinationData _purposeData;
    private FlowState _flowState = FlowState.PurposeView;

    // 迷惑度
    private float _annoyanceLevel;
    private StageData _stageData;

    // SOから参照
    private float _failedTime; // 失敗するまでの時間
    private float _currentTimer; // 現在の経過時間

    private int _purposeTimes; // 目的の個数
    private int _currentPurposeTimes; // 現在の目的達成数
    private float _resultTime; // 最終結果時間
    private bool _isClear; // クリアしたかの情報受け渡し用
   
    void Awake()
    {
        // プレイヤー生成
        _player = _playerPrefab ? Instantiate(_playerPrefab,new Vector3(0.0f,2.0f,0.0f),Quaternion.identity) : null;

        _ui = _uiPrefab ? Instantiate(_uiPrefab,_canvas.transform) : null;

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
        // playerComp?.Initialize(data.PlayerData);
        _stageData = data;
        _purposeTimes = data.ClearCount;
        _currentPurposeTimes = 0;
        _resultTime = 0.0f;
        Instantiate(_stageData.MapData.MapPrefab ?? null);
        SetPurpose();

        // 敵生成
        for(int i = 0;i < _stageData.EnemyCount;++i)
        {
            // Mapの生成位置データリストをもとに生成していく
            var area = _stageData.MapData.GetArea(Random.Range(0, _stageData.MapData.Areas.Count));
            
            Vector3 SpawnCenter = area.Center;
            Vector2 SpawnBoxSize = area.Size;

            float posX = Random.Range(SpawnCenter.x - (SpawnBoxSize.x * 0.5f), SpawnCenter.x + (SpawnBoxSize.x * 0.5f));
            float posZ = Random.Range(SpawnCenter.z - (SpawnBoxSize.y * 0.5f), SpawnCenter.z + (SpawnBoxSize.y * 0.5f));

            Vector3 SpawnPos = new Vector3(posX, 1.2f, posZ);

            var npc = Instantiate(_enemyPrefab,SpawnPos,Quaternion.identity);
            var npcCtrl = npc.GetComponentInChildren<NPC_Controller>();
            string state = area.Tag == NpcSpawnAreaTag.WalkArea ? "Browsing" : area.Tag == NpcSpawnAreaTag.StandArea ? "Standing" : "Grouping";
            // npcCtrl?.SetState(state);
        }

    }

    private void PurposeView()
    {
        if(_currentTimer > _purposeViewTime)
        {
            _flowState = FlowState.Execution;
            _currentTimer = 0.0f;
        }
    }

    private void Execution()
    {
        if(_purposeData.IsWithinAchievementRange(_player.transform.position)) CollectPurpose();

        if (!_ui.TryGetComponent<InGameUI>(out InGameUI ingameUI))
        {

        }
        InGameUI.InGameUI_PlayerData uiData;
        uiData.stamina = 1.0f;
        uiData.nuisance = _annoyanceLevel * 100.0f;
        ingameUI.SetInGameUI_PlayerData(uiData);

        if (_annoyanceLevel >= 1.0f)
        {
            _flowState = FlowState.Track;
            // プレイヤーを追跡状態にする
        }

        if (_failedTime == 0.0f && _currentTimer > _failedTime)
        {
            ToResult(false);
        }
    }


    private void Track()
    {
        if (_purposeData.IsWithinAchievementRange(_player.transform.position)) CollectPurpose();
        _annoyanceLevel -= Time.deltaTime / _annoyanceDecrease;
        if(_annoyanceLevel <= 0.0f)
        {
            // プレイヤーを元の状態に戻す

            _flowState = FlowState.Execution;
        }

        if (!_ui.TryGetComponent<InGameUI>(out InGameUI ingameUI))
        {

        }
        InGameUI.InGameUI_PlayerData uiData;
        uiData.stamina = 1.0f;
        uiData.nuisance = _annoyanceLevel * 100.0f;
        ingameUI.SetInGameUI_PlayerData(uiData);

        if (_failedTime == 0.0f && _currentTimer > _failedTime)
        {
            ToResult(false);
        }
    }

    public void SetPurpose()
    {
        _purposeData = _stageData.PurposePick();

        _failedTime = _stageData.FailedTime;
        _currentTimer = 0.0f;

        _purposeView = Instantiate(_purposeViewPrefab, _purposeData.DestinationPosition, Quaternion.identity);
        _purposeView.transform.localScale = new Vector3(_purposeData.AchievementSizeX, 5.0f, _purposeData.AchievementSizeZ);

        // auto playerComp = _player?.GetComponent<>();
        // playerComp?.SetPurpose(_purposeData.座標のやつ);

        if (!_ui.TryGetComponent<InGameUI>(out InGameUI ingameUI))
        {

        }
        InGameUI.InGameUIData uiData;
        uiData.stageNum = 0;
        uiData.destination = "Test";
        uiData.identity = "Test";
        uiData.remainingTime = _failedTime;
        ingameUI.SetInGameUIData(uiData);

        // if(_ui.TrygetComponent<XXXX>(out XXXX PurposeUI))
        // {
        //      PurposeUi.目標地点を表示するアニメーション開始フック
        // }
    }

    public void CollectPurpose()
    {
        Destroy(_purposeView);
        _resultTime += _currentTimer;
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
        GameData.ResultData data = new GameData.ResultData();
        data.IsClear = clear;
        data.pastTime = _resultTime;
        _gameData.ApplyResult(data);
        SceneManager.LoadScene("Result", LoadSceneMode.Additive);
    }
}
