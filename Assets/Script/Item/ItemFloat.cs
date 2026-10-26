using UnityEngine;

public class ItemFloat : MonoBehaviour
{
    [SerializeField, Tooltip("動きの速度")]
    private float _floatSpeed = 0.1f;

    [SerializeField, Tooltip("回転速度")]
    private float _rotSpeed = 5.0f;

    [SerializeField, Tooltip("動きの大きさ")]
    private float _floatMagnitude = 0.2f;

    [SerializeField, Tooltip("アイテムエフェクト")]
    private GameObject _effect;

    private float _currentRotation;
    private float _currentRate;

    private GameObject _effectObj;

    private Vector3 _basePos;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _currentRate = 0.0f;
        _currentRotation = 0.0f;
        _basePos = transform.position;
        _effectObj = Instantiate(_effect, transform);
    }

    // Update is called once per frame
    void Update()
    {
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
