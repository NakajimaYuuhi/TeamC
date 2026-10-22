using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    // スポーンオブジェクト
    [SerializeField] private GameObject _spawnObject;

    // スポーン間隔
    [SerializeField] private float _spawnInterval = 5f;
    private float _spawnTimer = 0f; // カウンター

    // スポーン位置(複数)
    [SerializeField] private Transform[] _spawnPoints;

    // 初期状態
    private NPC_Controller.NPCStateType _initialNPCState = NPC_Controller.NPCStateType.Browsing_BasedOnNode;

    // 初期ノードインデックス
    [SerializeField] private int _initialSpawnIndex;

    // NPCGenerator
    [SerializeField] private NodeEdgeGenerator _nodeedgeGenerator;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // スポーン処理
        // スポーン間隔のカウントダウン
        _spawnTimer -= Time.deltaTime;

        if (_spawnTimer <= 0f)
        {
            // スポーン処理
            // スポーンポイント全てに生成する
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                SpawnNPC(_spawnPoints[i]);
            }
            // タイマーをリセット
            _spawnTimer = _spawnInterval;
        }
    }

    // NPCをスポーンするメソッド
    private void SpawnNPC(Transform spawnPoint)
    {
        if (_spawnPoints.Length == 0)
        {
            Debug.LogWarning("No spawn points assigned.");
            return;
        }
 
        // NPCを生成
        GameObject npcInstance = Instantiate(_spawnObject, spawnPoint.position, spawnPoint.rotation);
        // NPC_Controllerを取得して初期状態を設定
        NPC_Controller npcController = npcInstance.GetComponent<NPC_Controller>();
        if (npcController != null)
        {
            // 状態設定
            npcController.SetState(_initialNPCState);
            // ノード設定
            npcController.SetNode(_nodeedgeGenerator.GetNode(_initialSpawnIndex));
        }
        else
        {
            Debug.LogWarning("Spawned object does not have an NPC_Controller component.");
        }
    }
}
