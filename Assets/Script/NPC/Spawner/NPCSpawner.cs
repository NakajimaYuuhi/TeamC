using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    [Header("スポーンデータ（SO）")]
    [SerializeField] private SpawnData _spawnData;

    [Header("スポーン位置(複数)")]
    [SerializeField] private Transform[] _spawnPoints;

    [Header("NPC初期設定")]
    [SerializeField] private NPC_Controller.NPCStateType _initialNPCState = NPC_Controller.NPCStateType.Browsing_BasedOnNode;
    [SerializeField] private int _initialSpawnIndex;
    [SerializeField] private NodeEdgeGenerator _nodeedgeGenerator;

    private float _spawnTimer = 0f;
    private bool _isFirstSpawn = true;
    private bool _isFinished = false;

    private void Start()
    {
        if (_spawnData == null)
        {
            Debug.LogWarning("[NPCSpawner] SpawnData が設定されていません。");
            return;
        }

        // 初回インターバルをSOから取得してセット
        _spawnTimer = _spawnData.GetNextInterval();
    }

    private void Update()
    {
        // 終了済み、またはデータ未割り当てなら処理しない
        if (_isFinished || _spawnData == null) return;

        // 次のスポーンが可能か確認（Onceなら初回完了後に停止）
        if (!_spawnData.CanSpawn(_isFirstSpawn))
        {
            _isFinished = true;
            return;
        }

        // カウントダウン
        _spawnTimer -= Time.deltaTime;

        if (_spawnTimer <= 0f)
        {
            // 全スポーンポイントに生成
            for (int i = 0; i < _spawnPoints.Length; i++)
            {
                if (_spawnPoints[i] != null)
                {
                    SpawnNPC(_spawnPoints[i]);
                }
            }

            _isFirstSpawn = false;

            // 一括生成（Once）の場合はここで終了
            if (!_spawnData.CanSpawn(_isFirstSpawn))
            {
                _isFinished = true;
                return;
            }

            // 次の待機時間をSOから再取得（ランダムなら毎回再抽選される）
            _spawnTimer = _spawnData.GetNextInterval();
        }
    }

    // NPCをスポーンするメソッド
    private void SpawnNPC(Transform spawnPoint)
    {
        // プレハブの取得先を SO からに変更
        GameObject prefab = _spawnData.SpawnPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[NPCSpawner] SpawnData に SpawnPrefab が設定されていません。");
            return;
        }

        // NPCを生成
        GameObject npcInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // 初期設定の流し込み
        NPC_Controller npcController = npcInstance.GetComponent<NPC_Controller>();
        if (npcController != null)
        {
            npcController.SetState(_initialNPCState);

            if (_nodeedgeGenerator != null)
            {
                var targetNode = _nodeedgeGenerator.GetNode(_initialSpawnIndex);
                //if (targetNode != null)
                //{
                npcController.SetNode(targetNode);
                //}
            }
        }
    }
}