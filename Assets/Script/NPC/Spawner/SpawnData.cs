using UnityEngine;

[CreateAssetMenu(fileName = "SpawnData", menuName = "Scriptable Objects/SpawnData")]
public class SpawnData : ScriptableObject
{
    public enum SpawnIntervalType
    {
        Once,           // 一括（最初だけ）
        FixedInterval,  // 一定間隔
        RandomInterval  // ランダムインターバル
    }

    [Header("生成プレハブ")]
    [SerializeField] private GameObject _spawnPrefab;

    [Header("生成パターンの切り替え")]
    [SerializeField] private SpawnIntervalType _intervalType = SpawnIntervalType.FixedInterval;

    [Header("各種パラメータ（パターンに応じて使用）")]
    [Tooltip("一括生成時の初回待機秒数（即座なら0）")]
    [SerializeField] private float _initialDelay = 0f;

    [Tooltip("一定間隔の場合のスポーン秒数")]
    [SerializeField] private float _fixedInterval = 3f;

    [Tooltip("ランダム間隔の場合の最小秒数")]
    [SerializeField] private float _minInterval = 2f;
    [Tooltip("ランダム間隔の場合の最大秒数")]
    [SerializeField] private float _maxInterval = 5f;

    public GameObject SpawnPrefab => _spawnPrefab;
    public SpawnIntervalType IntervalType => _intervalType;

    /// <summary>
    /// 次のスポーンまでの秒数を計算して返す
    /// </summary>
    public float GetNextInterval()
    {
        switch (_intervalType)
        {
            case SpawnIntervalType.Once:
                return _initialDelay;

            case SpawnIntervalType.FixedInterval:
                return _fixedInterval;

            case SpawnIntervalType.RandomInterval:
                return Random.Range(_minInterval, _maxInterval);

            default:
                return _fixedInterval;
        }
    }

    /// <summary>
    /// 次のスポーンを実行可能か判定（Once の場合は初回のみ true）
    /// </summary>
    public bool CanSpawn(bool isFirstSpawn)
    {
        if (_intervalType == SpawnIntervalType.Once)
        {
            return isFirstSpawn;
        }
        return true;
    }
}