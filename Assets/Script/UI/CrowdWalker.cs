using UnityEngine;

public class CrowdWalker : MonoBehaviour
{
    private Camera _camera;
    private CrowdFlowSpawner _owner;
    private Vector3 _baseScale;

    private float _viewportX;
    private float _viewportY;
    private float _distance;
    private float _speed;
    private int _direction;

    private bool _initialized;

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    public void Initialize(Camera targetCamera,CrowdFlowSpawner owner,
        float viewPortY,float distance,
        float speed,int direction,float scaleMultiplier)
    {
        _camera = targetCamera;
        _owner = owner;
        _viewportY = viewPortY;
        _distance = distance;
        _speed = speed;
        _direction = direction;

        _viewportX = direction > 0 ? -0.35f : 1.35f;

        transform.localScale = _baseScale * scaleMultiplier;
        transform.rotation = Quaternion.LookRotation(
            _camera.transform.right * _direction,
            _camera.transform.up
            );

        transform.position = _camera.ViewportToWorldPoint(
            new Vector3(_viewportX, _viewportY, distance)
            );

        foreach(Animator animator in GetComponentsInChildren<Animator>())
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        _initialized = true;
    }

    private void Update()
    {
        if(!_initialized || _camera == null ) return;

        _viewportX += _direction * _speed * Time.unscaledDeltaTime;

        transform.position = _camera.ViewportToWorldPoint(
            new Vector3(_viewportX,_viewportY, _distance)
            );
        transform.rotation = Quaternion.LookRotation(
            _camera.transform.right * _direction,
            _camera.transform.up
            );

        if (_direction > 0 && _viewportX > 1.5f )
        {
            Finish();
        }
        else if(_direction < 0 && _viewportX < -0.5f)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if(!_initialized) return;

        _initialized = false;

        if (_owner != null)
            _owner.NotifyFinished(this);

        Destroy(gameObject);
    }
}
