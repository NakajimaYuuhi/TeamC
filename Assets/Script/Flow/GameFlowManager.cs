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

    //新UI
    private GameHud _hud;
    //距離と仲間の人数を取るためのもの
    private Transform _playerBody;
    private FormationLeader _formationLeader;
    public GameHud Hud => _hud;

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

        //リザルト行くときにUIのUpdateを止める
        if (_flowState != FlowState.ResultView) UpdateHud();
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
        _playerBody = _player.GetComponentInChildren<PlayerMove>().transform;
        _npcPhysicsLodManager.SetTarget(_playerBody);

        _ui = _uiPrefab ? Instantiate(_uiPrefab) : null;
        _uiTimer = new UITimer();

        _hud = _ui ? _ui.GetComponentInChildren<GameHud>(true) : null;
        if (_hud == null) Debug.LogWarning("[GameFlowManager] UIプレハブの中に GameHud が見つかりません");

        // プレイヤーにデータ配布
        // auto playerComp = _player?.GetComponent<>();
        // playerComp?.Initialize(data.PlayerData);

        _purposeTimes = data.ClearCount;
        _currentPurposeTimes = 0;
        _resultTime = 0.0f;
        _isSpawnerBase = _stageData.IsSpawnerBase;
        Instantiate(_stageData.MapData.MapPrefab ?? null);

        var flowField = FindFirstObjectByType<FlowField>();
        if (flowField != null)
        {
            if (_player.TryGetComponent<FlowEmitter>(out FlowEmitter flowEmitter))
            {
                flowEmitter.SetField(flowField);
            }
        }

        var flowAnalizer = FindFirstObjectByType<FlowLaneAnalyzer>();
        if (flowField != null)
        {
            if (_player.TryGetComponent<FormationLeader>(out FormationLeader formationLeader))
            {
                formationLeader.SetAnalizer(flowAnalizer);
            }
        }
        //プレイヤーに追従するNPCの数を取得
        _formationLeader = _player.GetComponentInChildren<FormationLeader>();

        SetPurpose();

        // 敵生成
        if (!_isSpawnerBase)
        {
            for (int i = 0; i < _stageData.EnemyCount; ++i)
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
                var npc = Instantiate(_enemyPrefab, SpawnPos, rot);
                var npcCtrl = npc.GetComponentInChildren<NPC_Controller>();
                if (npcCtrl != null) _npcPhysicsLodManager.Register(npcCtrl);
                string state = area.Tag == NpcSpawnAreaTag.WalkArea ? "Browsing_BasedOnNode" : area.Tag == NpcSpawnAreaTag.StandArea ? "Standing" : "Grouping";
                if (transform.TryGetComponent<NodeEdgeGenerator>(out NodeEdgeGenerator neg))
                {
                    npcCtrl.SetNodeData(gameObject);
                    npcCtrl.SetNode(neg.GetNode(Random.Range(0, 6)));
                }
                npcCtrl?.SetState(state);
            }
        }
    }

    private void PurposeView()
    {
        if (_uiTimer.RemainingTime > _purposeViewTime)
        {
            _flowState = FlowState.Execution;
            _uiTimer.RemainingTime = _failedTime;
        }
    }

    private void Execution()
    {
        if (_purposeData.IsWithinAchievementRange(_player.transform.position)) CollectPurpose();

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
        if (_annoyanceLevel <= 0.0f)
        {
            // プレイヤーを元の状態に戻す

            _flowState = FlowState.Execution;
        }

        if (_failedTime != 0.0f && _uiTimer.RemainingTime > _failedTime)
        {
            ToResult(false);
        }
    }

    //残り時間・目的地までの距離・仲間の人数を、毎フレームHUDに渡す
    private void UpdateHud()
    {
        if (_hud == null) return;
        _hud.SetTime(_uiTimer.RemainingTime);
        if (_playerBody != null)
        {
            Vector3 d = _purposeData.DestinationPosition - _playerBody.position;
            d.y = 0.0f;
            _hud.SetDistance(d.magnitude);
        }
        _hud.SetFollowers(GetFollowerCount());
    }

    //ついてきている人の人数
    private int GetFollowerCount()
    {
        //隊形の席に入っている人数
        return _formationLeader != null ? _formationLeader.MemberCount : 0;
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
        if (_annoyanceLevel > 1.0f)
        {
            _flowState = FlowState.Track;
            _annoyanceLevel = 1.0f;
        }
    }
    public void ToResult(bool clear)
    {
        var presentation = GetComponent<CollectPurposePresentation>();

        _flowState = FlowState.ResultView;
        _isClear = clear;
        GameData.ResultData data = new GameData.ResultData();
        data.IsClear = clear;
        data.pastTime = _resultTime;
        _gameData.ApplyResult(data);
        StartCoroutine(presentation.Play());
    }

}
