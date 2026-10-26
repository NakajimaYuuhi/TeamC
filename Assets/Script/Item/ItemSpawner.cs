using UnityEngine;
using System.Collections.Generic;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField]
    private GameObject _item;

    [SerializeField]
    private Vector3 _spawnAreaSize = new Vector3(10.0f,0.1f,10.0f);

    [SerializeField]
    private float _spawnInterval = 2.0f;

    [SerializeField]
    private int _maxFieldItem = 5;

    private float _currentInterval;

    private void Start()
    {
        _currentInterval = 0.0f;
    }

    private void Update()
    {
        _currentInterval += Time.deltaTime;

        if (_currentInterval > _spawnInterval)
            SpawnItem();
    }

    private void SpawnItem()
    {
        _currentInterval = 0.0f;
        if (GameObject.FindObjectsByType<ItemFloat>(FindObjectsSortMode.None).Length > _maxFieldItem)
            return;

        Vector3 SpawnPos = Vector3.zero;
        Vector3 halfSize = _spawnAreaSize * 0.5f;
        SpawnPos.x = Random.Range(
            transform.position.x - halfSize.x, 
            transform.position.x + halfSize.x);
        SpawnPos.y = 1.0f;
        SpawnPos.z = Random.Range(
            transform.position.z - halfSize.z,
            transform.position.z + halfSize.z);

        Instantiate(_item, SpawnPos, Quaternion.identity);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireCube(transform.position,_spawnAreaSize);
    }
}
