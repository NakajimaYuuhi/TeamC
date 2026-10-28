using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ItemData
{
    public ItemSkill.SkillType Type;
    public GameObject model;
    public GameObject effect;
};


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

    [SerializeField]
    private List<ItemData> _skills = new List<ItemData>();

    [SerializeField, InspectorName("ランダムでアイテムの種類を変えるか")]
    private bool _isTypeRandom = true;

    [SerializeField, InspectorName("ランダムじゃない場合何を使用するか")]
    private ItemSkill.SkillType _type = ItemSkill.SkillType.ToPurpose;


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

        var obj = Instantiate(_item, SpawnPos, Quaternion.identity);
        
        var type = _isTypeRandom ? Random.Range(0, (int)ItemSkill.SkillType.TypeCount) : (int)_type;
        obj.GetComponent<ItemExecute>().Initialize(_skills[type]);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireCube(transform.position,_spawnAreaSize);
    }
}
