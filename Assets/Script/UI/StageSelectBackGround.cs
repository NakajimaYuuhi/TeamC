using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

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
    private VideoPlayer _moviePlayer;
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
        var clip = _gamedata.GetSelectStageData().MovieClip;
        if (_moviePrefab == null || clip == null) return;

        _playerObject = Instantiate(_moviePrefab);
        _moviePlayer = _playerObject.GetComponentInChildren<VideoPlayer>(true);
        if (_moviePlayer == null)
        {
            Stop();
            return;
        }
        _moviePlayer.gameObject.SetActive(true);
        _moviePlayer.playOnAwake = false;
        _moviePlayer.source = VideoSource.VideoClip;
        _moviePlayer.clip = clip;
        _moviePlayer.isLooping = true;
        _moviePlayer.audioOutputMode = VideoAudioOutputMode.None;
        _moviePlayer.waitForFirstFrame = true;
        if (_renderTexture != null) _moviePlayer.targetTexture = _renderTexture;
        _moviePlayer.prepareCompleted += OnMoviePrepared;
        _moviePlayer.Prepare();
    }

    private void OnMoviePrepared(VideoPlayer player)
    {
        if (player != _moviePlayer) return;
        player.prepareCompleted -= OnMoviePrepared;
        player.Play();
    }

    private void PlayPrefab()
    {

    }

    public void Stop()
    {
        _isPrefabPlaying = false;
        _isSlidePlaying = false;
        if (_moviePlayer != null)
        {
            _moviePlayer.prepareCompleted -= OnMoviePrepared;
            _moviePlayer.Stop();
            _moviePlayer = null;
        }
        if (_playerObject != null) _playerObject.SetActive(false);
        Destroy(_playerObject);
        _playerObject = null;

    }
    private void OnDestroy()
    {
        Stop();
    }
}
