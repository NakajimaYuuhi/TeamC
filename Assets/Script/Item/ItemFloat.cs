using Unity.Hierarchy;
using UnityEngine;

public class ItemFloat : MonoBehaviour
{
    [SerializeField, Tooltip("動きの速度")]
    private float _floatSpeed = 2.0f;

    [SerializeField, Tooltip("回転速度")]
    private float _rotSpeed = 20.0f;

    [SerializeField, Tooltip("動きの大きさ")]
    private float _floatMagnitude = 0.2f;

    [SerializeField, Tooltip("アイテムエフェクト")]
    private GameObject _effect;

    [SerializeField]
    private float _lifeTime = 20.0f;

    private float _currentLifeTime;

    private float _currentRotation;
    private float _currentRate;

    private GameObject _effectObj;

    private Vector3 _basePos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Initialize(GameObject effect)
    {
        _currentRate = 0.0f;
        _currentRotation = 0.0f;
        _currentLifeTime = 0.0f;
        _basePos = transform.position;
        _effectObj = Instantiate(effect, transform);
    }

    // Update is called once per frame
    void Update()
    {
        _currentLifeTime += Time.deltaTime;
        if (_currentLifeTime > _lifeTime)
            Destroy(gameObject.transform.parent.gameObject);

        _currentRate += Time.fixedDeltaTime * _floatSpeed;
        _currentRotation += Time.deltaTime * _rotSpeed;

        Vector3 floatPos = _basePos;
        floatPos.y += Mathf.Sin(_currentRate) * _floatMagnitude;
        transform.position = floatPos;

        Vector3 newRot = transform.eulerAngles;
        newRot.y = _currentRotation;
        transform.eulerAngles = newRot;
    }

    private void OnDestroy()
    {
        Destroy(_effectObj);
    }
}
