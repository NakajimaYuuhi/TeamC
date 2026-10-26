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
using Unity.VisualScripting;

[RequireComponent(typeof(NpcPhysicsLodManager))]
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

    [SerializeField, Tooltip("ゲームのデータ")]
    private GameData _gameData;

    [SerializeField, Tooltip("目的地の表示")]
    private GameObject _purposeViewPrefab;
    private GameObject _purposeView;

    [Header("===== フロー管理変数 =====")]
    [SerializeField, Tooltip("目的地の名前を表示する時間")]
    private float _purposeViewTime;

    [SerializeField, Tooltip("迷惑率が減少する速度")]
    private float _annoyanceDecrease = 3.0f;

    [SerializeField]
    private bool _isSpawnerBase;

    [SerializeField]
    private bool _isFinishTypeTimer;

    private DestinationData _purposeData;
    private FlowState _flowState = FlowState.PurposeView;

    private NpcPhysicsLodManager _npcPhysicsLodManager;
    private UITimer _uiTimer;


    // 迷惑度
    private float _annoyanceLevel;
    private StageData _stageData;

    // SOから参照
    private float _failedTime; // 失敗するまでの時間

    private int _purposeTimes; // 目的の個数
    private int _currentPurposeTimes; // 現在の目的達成数
    private float _resultTime; // 最終結果時間
    private bool _isClear; // クリアしたかの情報受け渡し用

    void Awake()
    {
        Initialize(_gameData.GetSelectStageData());
    }

    
    void Update()
    {
        _uiTimer.RemainingTime -= Time.deltaTime;

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

        _stageData = data;
        // プレイヤー生成
        _player = _playerPrefab ? Instantiate(_playerPrefab, _stageData.PlayerInitPos, Quaternion.identity) : null;

        _npcPhysicsLodManager = GetComponent<NpcPhysicsLodManager>();
        _npcPhysicsLodManager.SetTarget(_player.GetComponentInChildren<PlayerMove>().transform);

        _ui = _uiPrefab ? Instantiate(_uiPrefab) : null;
        _uiTimer = new UITimer();
        var ingameUI = _ui.GetComponentInChildren<InGameUI>();
        ingameUI?.SetUITimer(_uiTimer);

        // プレイヤーにデータ配布
        // auto playerComp = _player?.GetComponent<>();
        // playerComp?.Initialize(data.PlayerData);

        _purposeTimes = data.ClearCount;
        _currentPurposeTimes = 0;
        _resultTime = 0.0f;
        _isSpawnerBase = _stageData.IsSpawnerBase;
        Instantiate(_stageData.MapData.MapPrefab ?? null);
        SetPurpose();

        // 敵生成
        if(!_isSpawnerBase)
        {
            for(int i = 0;i < _stageData.EnemyCount;++i)
            {
                // Mapの生成位置データリストをもとに生成していく
                var area = _stageData.MapData.GetArea(Random.Range(0, _stageData.MapData.Areas.Count));
            
                Vector3 SpawnCenter = area.Center;
                Vector3 SpawnBoxSize = area.Size;

                float posX = Random.Range(SpawnCenter.x - (SpawnBoxSize.x * 0.5f), SpawnCenter.x + (SpawnBoxSize.x * 0.5f));
                float posZ = Random.Range(SpawnCenter.z - (SpawnBoxSize.z * 0.5f), SpawnCenter.z + (SpawnBoxSize.z * 0.5f));

                Vector3 SpawnPos = new Vector3(posX, 1.2f, posZ);

                Quaternion rot = Random.rotation;
                rot.x = rot.z = 0.0f;
                var npc = Instantiate(_enemyPrefab,SpawnPos,rot);
                var npcCtrl = npc.GetComponentInChildren<NPC_Controller>();
                if (npcCtrl != null) _npcPhysicsLodManager.Register(npcCtrl);
                string state = area.Tag == NpcSpawnAreaTag.WalkArea ? "Browsing_BasedOnNode" : area.Tag == NpcSpawnAreaTag.StandArea ? "Standing" : "Grouping";
                if(transform.TryGetComponent<NodeEdgeGenerator>(out NodeEdgeGenerator neg))
                {
                    npcCtrl.SetNodeData(gameObject);
                    npcCtrl.SetNode(neg.GetNode(Random.Range(0,6)));
                }
                npcCtrl?.SetState(state);
            }
        }
    }

    private void PurposeView()
    {
        if(_uiTimer.RemainingTime > _purposeViewTime)
        {
            _flowState = FlowState.Execution;
            _uiTimer.RemainingTime = _failedTime;
            InGameUI ingameUI = _ui.GetComponentInChildren<InGameUI>();
            ingameUI.TimerStart();
        }
    }

    private void Execution()
    {
        if(_purposeData.IsWithinAchievementRange(_player.transform.position)) CollectPurpose();

        InGameUI ingameUI = _ui.GetComponentInChildren<InGameUI>();

        InGameUI.InGameUI_PlayerData uiData;
        uiData.stamina = 1.0f;
        uiData.nuisance = _annoyanceLevel * 100.0f;
        ingameUI.SetInGameUI_PlayerData(uiData);

        if (_annoyanceLevel >= 1.0f)
        {
            _flowState = FlowState.Track;
            // プレイヤーを追跡状態にする
        }

        if (_failedTime != 0.0f && _uiTimer.RemainingTime > _failedTime)
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

        InGameUI ingameUI = _ui.GetComponentInChildren<InGameUI>();

        InGameUI.InGameUI_PlayerData uiData;
        uiData.stamina = 1.0f;
        uiData.nuisance = _annoyanceLevel * 100.0f;
        ingameUI.SetInGameUI_PlayerData(uiData);

        if (_failedTime != 0.0f && _uiTimer.RemainingTime > _failedTime)
        {
            ToResult(false);
        }
    }

    public void SetPurpose()
    {
        _purposeData = _stageData.PurposePick();

        _failedTime = _stageData.FailedTime;
        _uiTimer.RemainingTime = _failedTime;

        _purposeView = Instantiate(_purposeViewPrefab, _purposeData.DestinationPosition, Quaternion.identity);
        _purposeView.transform.localScale = new Vector3(_purposeData.AchievementSizeX, 20.0f, _purposeData.AchievementSizeZ);

        var playerComp = _player?.GetComponentInChildren<GoalArrow>();
        playerComp?.SetDirection(_purposeData.DestinationPosition);

        InGameUI ingameUI = _ui.GetComponentInChildren<InGameUI>();
        
        InGameUI.InGameUIData uiData;
        uiData.stageNum = 0;
        uiData.identity = _purposeData.DestinationName;
        uiData.destination = "Test";
        ingameUI.SetInGameUIData(uiData);

        InGameUIPopup popup = _ui.GetComponentInChildren<InGameUIPopup>();
        popup.Play();
    }

    public void CollectPurpose()
    {
        Destroy(_purposeView);
        _resultTime += (_failedTime - _uiTimer.RemainingTime);
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
