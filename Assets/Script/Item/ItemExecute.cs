using UnityEditor.SceneManagement;
using UnityEngine;

public class ItemExecute : MonoBehaviour
{
    [SerializeField, Tooltip("Ý’u”»’è")]
    private GameObject _executeRangeObj;

    [SerializeField, Tooltip("")]
    private GameObject _groundEffect;

    [SerializeField]
    private GameObject _breakEffect;

    [SerializeField, Tooltip("Œø‰Ê‚Ì”­“®ŽžŠÔ")]
    private float _executeTime = 2.0f;

    [SerializeField]
    private float _executeSize = 2.0f;

    private float _currentExecuteTime;
    private bool _isExecute;
    private bool _isThrow;

    [SerializeField]
    private GameObject _throwEffect;

    private GameObject _currentEffect;

    private Vector3 _exeCutePosition;

    private void Start()
    {
        _executeRangeObj?.SetActive(false);
        _isExecute = false;
        _isThrow = false;
    }

    private void Update()
    {
        if(_isExecute)
        {
            _currentExecuteTime += Time.fixedDeltaTime;
            if(_currentExecuteTime > _executeTime )
            {
                if(_currentEffect != null)
                    Destroy(_currentEffect);
                if(_breakEffect != null)
                    Destroy(Instantiate(_breakEffect,transform.position,Quaternion.identity),1.0f);
                Destroy(gameObject);
            }
        }
    }

    public void OnThrown()
    {
        _isThrow = true;
        if (_throwEffect != null)
            _currentEffect = Instantiate(_throwEffect, transform);
        var itemFloat = GetComponentInChildren<ItemFloat>();
        itemFloat.enabled = false;
        gameObject.tag = "Untagged";
    }

    public void Execute()
    {
        _isExecute = true;
        _currentExecuteTime = 0.0f;
        if (_currentEffect != null) Destroy(_currentEffect);
        _executeRangeObj?.SetActive(true);
        if (_groundEffect != null)
        {
            _currentEffect = Instantiate(_groundEffect, transform.position, Quaternion.identity);
            _executeRangeObj.transform.localScale = Vector3.one * _executeSize;
            var particles = _currentEffect.GetComponentsInChildren<Transform>();
            foreach(var particle in  particles)
            {
                particle.localScale = Vector3.one * _executeSize;

            }
        }

        Vector3 exePosition = transform.position;
        exePosition.y = 1.0f;
        transform.position = exePosition;
        Destroy(GetComponent<Rigidbody>());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Ground") return;
        if (!_isThrow) return;
        Execute();
    }

    private void OnDestroy()
    {
        Destroy(_currentEffect);
    }
}
