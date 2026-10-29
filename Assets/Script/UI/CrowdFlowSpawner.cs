using System.Collections.Generic;
using UnityEngine;

public class CrowdFlowSpawner : MonoBehaviour
{
    [Header("===== References =====")]
    [SerializeField]
    private Camera _transitionCamera;
    [SerializeField]
    private GameObject _crowdPrefab;

    [Header("===== Spawn Settings =====")]
    [SerializeField, Min(0.01f)]
    private float _minSpawnInterval = 0.08f;

    [SerializeField, Min(0.01f)]
    private float _maxSpawnInterval = 0.16f;
    [SerializeField, Min(1.0f)]
    private int _maxActivedCrowds = 32;

    [Header("===== Movement Settings =====")]
    [SerializeField]
    private Vector2 _speedRange = new Vector2(0.0f, 1.4f);

    [SerializeField]
    private Vector2 _distanceRange = new Vector2(2.5f, 5.5f);

    [SerializeField]
    private Vector2 _scaleRange = new Vector2(0.9f, 1.2f);

    [SerializeField]
    private Vector2 _viewportYRange = new Vector2(0.02f, 0.72f);

    [Header("===== Layer =====")]
    [SerializeField]
    private string _crowdLayerName = "TransitionCrowd";

    private readonly List<CrowdWalker> _activateCrowds = 
        new List<CrowdWalker>();

    private bool _isSpawning;
    private float _nextSpawnTime;
    private int _crowdLayer = -1;

    public int ActiveCount => _activateCrowds.Count;

    private void Awake()
    {
        _crowdLayer = LayerMask.NameToLayer(_crowdLayerName);

        if(_crowdLayer < 0)
        {
            Debug.LogError(
               $"Layer '{_crowdLayerName}' does not exist. " +
               "Create it in Tags and Layers."
           );
        }
    }

    void Update()
    {
        _activateCrowds.RemoveAll(crowd => crowd == null);

        if (!_isSpawning) return;

        if (Time.unscaledTime < _nextSpawnTime)
            return;

        _nextSpawnTime = Time.unscaledTime + 
            Random.Range(_minSpawnInterval, _maxSpawnInterval);

        if (_activateCrowds.Count >= _maxActivedCrowds)
            return;

        SpawnCrowd();
    }

    public void BeginFlow()
    {
        _isSpawning = true;
        _nextSpawnTime = Time.unscaledTime;
    }

    public void StopFlow()
    {
        _isSpawning = false;
    }

    public void NotifyFinished(CrowdWalker walker)
    {
        _activateCrowds.Remove(walker);
    }

    private void SpawnCrowd()
    {
        if(_crowdPrefab == null || _transitionCamera == null) return;

        if (_crowdLayer < 0)
            return;

        GameObject instance = Instantiate(_crowdPrefab,transform);

        SetLayerRecursively(instance.transform,_crowdLayer);

        CrowdWalker walker = instance.GetComponent<CrowdWalker>();

        if(walker == null)
        {
            Destroy(instance);
            return;
        }

        int direction = Random.value < 0.5f ? 1 : -1;

        float viewportY = Random.Range(
           _viewportYRange.x,
           _viewportYRange.y
       );

        float distance = Random.Range(
            _distanceRange.x,
            _distanceRange.y
        );

        float speed = Random.Range(
            _speedRange.x,
            _speedRange.y
        );

        float scale = Random.Range(
            _scaleRange.x,
            _scaleRange.y
        );

        walker.Initialize(
            _transitionCamera,
            this,
            viewportY,
            distance,
            speed,
            direction,
            scale
            );

        _activateCrowds.Add(walker);
    }

    private void SetLayerRecursively(Transform target,int layer)
    {
        target.gameObject.layer = layer;

        foreach(Transform child in target)
        {
            SetLayerRecursively(child, layer);
        }
    }

    public void ClearAllCrowds()
    {
        _isSpawning = false;

        foreach(CrowdWalker crowd in _activateCrowds)
        {
            if (crowd != null)
                Destroy(crowd.gameObject);
        }

        _activateCrowds.Clear();
    }
}
