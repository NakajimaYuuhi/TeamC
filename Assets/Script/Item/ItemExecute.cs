using UnityEngine;

public class ItemExecute : MonoBehaviour
{
    [SerializeField, InspectorName("拾う前のエフェクト")]
    private GameObject _popEffect;

    [SerializeField, Tooltip("設置判定")]
    private GameObject _executeRangeObj;

    [SerializeField, Tooltip("")]
    private GameObject _groundEffect;

    [SerializeField]
    private GameObject _breakEffect;

    [SerializeField, Tooltip("効果の発動時間")]
    private float _executeTime = 2.0f;

    [SerializeField]
    private float _executeSize = 2.0f;

    private ItemSkill.SkillType _type;
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
            _currentExecuteTime += Time.deltaTime;
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

    public void Initialize(ItemData data)
    {
        _type = data.Type;
        _groundEffect = data.effect;
        var itemFloat = Instantiate(data.model, transform).AddComponent<ItemFloat>();
        itemFloat.Initialize(_popEffect);
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
        _executeRangeObj.GetComponent<ItemSkill>().SetType(_type);
        if (_groundEffect != null)
        {
            _currentEffect = Instantiate(_groundEffect, transform.position, Quaternion.identity);
            if(_currentEffect.TryGetComponent<ObjectEffect>(out ObjectEffect efk))
            {
                efk.SetScale(_executeSize * 0.5f);
            }
            else
            {
                _executeRangeObj.transform.localScale = Vector3.one * _executeSize;
            }
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
