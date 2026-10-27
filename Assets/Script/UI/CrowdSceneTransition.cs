using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Runtime.CompilerServices;

public class CrowdSceneTransition : MonoBehaviour
{
    public static CrowdSceneTransition Instance {  get; private set; }

    [Header("===== Rendering =====")]
    [SerializeField]
    private Camera _transitionCamera;
    [SerializeField]
    private Canvas _transitionCanvas;
    [SerializeField]
    private RawImage _transitionImage;

    [SerializeField]
    private Camera _mainCamera;

    [Header("====== Crowd =======")]
    [SerializeField]
    private CrowdFlowSpawner _spawner;

    [Header("===== Timing ======")]
    [SerializeField, Min(0.0f)]
    private float _enterDuration = 1.5f;

    [SerializeField, Min(0.0f)]
    private float _minCoveredLoadTime = 0.75f;

    private bool _isTransitioning;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _transitionCamera.enabled = false;
        _transitionCanvas.enabled = false;

        _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _transitionCanvas.overrideSorting = true;
        _transitionCanvas.sortingOrder = 32767;

        if(_transitionCamera.targetTexture != null)
        {
            _transitionImage.texture = 
                _transitionCamera.targetTexture;
        }
    }

    private void LateUpdate()
    {
        if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null || _transitionCamera == null)
            return;

        _transitionCamera.transform.SetPositionAndRotation(
            _mainCamera.transform.position,
            _mainCamera.transform.rotation
            );

        _transitionCamera.orthographic = _mainCamera.orthographic;
        _transitionCamera.fieldOfView = _mainCamera.fieldOfView;
        _transitionCamera.orthographicSize = _mainCamera.orthographicSize;
        _transitionCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _transitionCamera.farClipPlane = _mainCamera.farClipPlane;
        _transitionCamera.lensShift = _mainCamera.lensShift;

        
    }

    public void LoadScene(string SceneName)
    {
        if (_isTransitioning)
            return;

        if (!Application.CanStreamedLevelBeLoaded(SceneName)) return;

        StartCoroutine(LoadSceneCoroutine(SceneName));
    }

    private IEnumerator LoadSceneCoroutine(string SceneName)
    {
        _isTransitioning = true;

        _transitionCamera.enabled = true;
        _transitionCanvas.enabled = true;

        _spawner.BeginFlow();

        yield return new WaitForSecondsRealtime(_enterDuration);

        AsyncOperation operation = 
            SceneManager.LoadSceneAsync(SceneName,LoadSceneMode.Single);

        if(operation == null)
        {
            yield return FinishTransition();
            yield break;
        }

        operation.allowSceneActivation = false;

        float loadStartTime = Time.realtimeSinceStartup;

        while(
            operation.progress < 0.9f ||
            Time.realtimeSinceStartup - loadStartTime < _minCoveredLoadTime)
        {
            yield return null;
        }

        operation.allowSceneActivation = true;

        while(!operation.isDone)
        {
            yield return null;
        }

        yield return FinishTransition();
    }

    private IEnumerator FinishTransition()
    {
        _spawner.StopFlow();

        while(_spawner.ActiveCount > 0)
        {
            yield return null;
        }

        _transitionCamera.enabled = false;
        _transitionCanvas.enabled = false;

        _isTransitioning = false;
    }
}
