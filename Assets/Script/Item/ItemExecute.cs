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

    private float _currentExecuteTime;
    private bool _isExecute;
    private bool _isThrow;

    [SerializeField]
    private GameObject _throwEffect;

    private GameObject _currentEffect;

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
            _currentExecuteTime += Time.deltaTime;
            if(_currentExecuteTime > _executeTime )
            {
                if(_currentEffect != null)
                    Destroy(_currentEffect);
                if(_breakEffect != null)
                    Destroy(Instantiate(_breakEffect),1.0f);
                Destroy(gameObject);
            }
        }
    }

    public void Throw()
    {
        _isThrow = true;
        if (_throwEffect != null)
            _currentEffect = Instantiate(_throwEffect, transform);
    }

    public void Execute()
    {
        _isExecute = true;
        if (_currentEffect != null) Destroy(_currentEffect);
        _executeRangeObj?.SetActive(true);
        if (_groundEffect != null)
            Instantiate(_groundEffect, transform);
        _currentExecuteTime = 0.0f;
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
