using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEditor;

public class StageSelectBackGround : MonoBehaviour
{
    public enum BackGroundType
    {
        SlideShow,
        Movie,
        Prefab
    };
    [SerializeField]
    private GameObject _moviePrefab;

    [SerializeField]
    private GameObject _slideshowPrefab;

    [SerializeField]
    private GameObject _prefabPrefab;

    [SerializeField]
    private RenderTexture _renderTexture;

    [SerializeField]
    private GameData _gamedata;

    [SerializeField]
    private float _slideSpeed = 1.0f;

    [SerializeField]
    private float _animSpeed = 1.0f;

    private float _currentTime;

    private GameObject _playerObject;
    private bool _isSlidePlaying;
    private bool _isSlideAnim;

    private bool _isPrefabPlaying;

    private int _currentPage;
    private int _nextPage;

    private Image _current;
    private Image _next;

    private void Start()
    {
        
    }

    private void Update()
    {
        if (_isSlidePlaying)
        {
            _currentTime += Time.deltaTime;
            if (_isSlideAnim)
            {
                if (_currentTime > _animSpeed)
                {
                    _current.sprite = _next.sprite;
                    _next.sprite = _gamedata.GetSelectStageData().Textures[_nextPage];
                    _current.rectTransform.anchoredPosition = new Vector3(0.0f, 0.0f, 0.0f);
                    _next.rectTransform.anchoredPosition = new Vector3(1920.0f, 0.0f, 0.0f);
                    _isSlideAnim = false;
                    _currentTime = 0.0f;
                }
                else
                {
                    float rate = _currentTime / _animSpeed;
                    _current.rectTransform.anchoredPosition = new Vector3(-1920.0f * rate, 0.0f, 0.0f);
                    _next.rectTransform.anchoredPosition = new Vector3(1920.0f * (1 - rate), 0.0f, 0.0f);
                }

            }
            else
            {
                if (_currentTime > _slideSpeed)
                {
                    _currentPage = _nextPage;
                    _nextPage++;
                    if (_gamedata.GetSelectStageData().Textures.Count <= _nextPage)
                    {
                        _nextPage = 0;
                    }
                    _isSlideAnim = true;
                    _currentTime = 0.0f;
                }
            }
        }
    }

    public void Play()
    {

        switch (_gamedata.GetSelectStageData().BackGroundType)
        {
            case BackGroundType.SlideShow:
                PlaySlide();
                break;
            case BackGroundType.Movie:
                PlayMovie();
                break;
            case BackGroundType.Prefab:
                PlayPrefab();
                break;
        }
    }

    private void PlaySlide()
    {

        _playerObject = Instantiate(_slideshowPrefab);
        _currentPage = 0;
        _nextPage = 1;

        // コンポーネントを取得して最初のページを設定
        _current = _playerObject.transform.Find("Canvas/Current").GetComponent<Image>();
        _next = _playerObject.transform.Find("Canvas/Next").GetComponent<Image>();
        _current.sprite = _gamedata.GetSelectStageData().Textures[_currentPage];
        _next.sprite = _gamedata.GetSelectStageData().Textures[_nextPage];

        _currentTime = 0.0f;

        _isSlidePlaying = _gamedata.GetSelectStageData().Textures.Count > 1;
    }

    private void PlayMovie()
    {
        _playerObject = Instantiate(_moviePrefab);
        var moviePlayer = _playerObject.GetComponentInChildren<VideoPlayer>();
        moviePlayer.gameObject.SetActive(true);
        moviePlayer.clip = _gamedata.GetSelectStageData().MovieClip;

    }

    private void PlayPrefab()
    {

    }

    public void Stop()
    {
        _isPrefabPlaying = false;
        _isSlidePlaying = false;
        Destroy(_playerObject);

    }
}
