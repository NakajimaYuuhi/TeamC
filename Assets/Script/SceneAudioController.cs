using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneAudioController : MonoBehaviour
{
    public static SceneAudioController Instance { get; private set; }

    [Header("画面別BGM")]
    [SerializeField, InspectorName("タイトル")]
    private AudioClip titleBgm;
    [SerializeField, InspectorName("ステージ選択")]
    private AudioClip stageSelectBgm;
    [SerializeField, InspectorName("ゲーム中")]
    private AudioClip inGameBgm;
    [SerializeField, InspectorName("リザルト")]
    private AudioClip resultBgm;

    [Header("効果音")]
    [SerializeField, InspectorName("タイトルの選択音")]
    private AudioClip titleSelectionSound;
    [SerializeField, InspectorName("ステージ選択の選択音")]
    private AudioClip stageSelectionSound;

    [SerializeField, InspectorName("リザルトの選択音")]
    private AudioClip resultSelectionSound;
    [SerializeField, InspectorName("ステージクリア音")]
    private AudioClip stageClearSound;
    [SerializeField, InspectorName("決定音")]
    private AudioClip confirmSound;
    [SerializeField, InspectorName("戻る音")]
    private AudioClip backSound;
    [SerializeField, InspectorName("ロック時効果音")]
    private AudioClip lockedSound;
    [SerializeField, InspectorName("アンロック時効果音")]
    private AudioClip unlockedSound;
    [SerializeField, InspectorName("アイテム投擲音")]
    private AudioClip throwItemSound;
    [SerializeField, InspectorName("アイテム着地音")]
    private AudioClip putItemSound;
    [SerializeField, InspectorName("アイテム動作音（ループ）")]
    private AudioClip itemIngSound;

    [Header("音量")]
    [SerializeField, Range(0f, 1f), InspectorName("BGM音量")]
    private float bgmVolume = 0.2f;
    [SerializeField, Range(0f, 1f), InspectorName("SE音量（UI・クリア・ステップ）")]
    private float selectionVolume = 0.85f;
    [SerializeField, Range(0f, 1f), InspectorName("アイテム動作ループ音量")]
    private float itemLoopVolume = 0.25f;

    private AudioSource bgmSource;
    private AudioSource selectionSource;
    private bool stageClearPlayed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<SceneAudioController>("Audio/SceneAudio");
        if (prefab != null)
        {
            var controller = Instantiate(prefab);
            controller.name = "SoundManager";
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = bgmVolume;
        selectionSource = gameObject.AddComponent<AudioSource>();
        selectionSource.playOnAwake = false;
        selectionSource.spatialBlend = 0f;
        selectionSource.volume = selectionVolume;

        if (lockedSound == null) lockedSound = Resources.Load<AudioClip>("Audio/SE/locked");
        if (unlockedSound == null) unlockedSound = Resources.Load<AudioClip>("Audio/SE/unlocked");
        if (throwItemSound == null) throwItemSound = Resources.Load<AudioClip>("Audio/SE/throw");
        if (putItemSound == null) putItemSound = Resources.Load<AudioClip>("Audio/SE/put_item");
        if (itemIngSound == null) itemIngSound = Resources.Load<AudioClip>("Audio/SE/item_ing");

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "PlayTest") stageClearPlayed = false;
        AudioClip clip;
        switch (scene.name)
        {
            case "Title": clip = titleBgm; break;
            case "StageSelect": clip = stageSelectBgm; break;
            case "PlayTest": clip = inGameBgm; break;
            case "Result": clip = resultBgm; break;
            default:
                if (mode == LoadSceneMode.Additive) return;
                clip = null;
                break;
        }

        if (bgmSource.clip == clip) return;
        bgmSource.Stop();
        bgmSource.clip = clip;
        if (clip != null) bgmSource.Play();
    }

    public void PlayTitleSelection()
    {
        if (titleSelectionSound != null) selectionSource.PlayOneShot(titleSelectionSound);
    }

    public void PlayStageSelection()
    {
        if (stageSelectionSound != null) selectionSource.PlayOneShot(stageSelectionSound);
    }

    public void PlayResultSelection()
    {
        if (resultSelectionSound != null) selectionSource.PlayOneShot(resultSelectionSound);
    }

    public void PlayConfirm()
    {
        if (confirmSound != null) selectionSource.PlayOneShot(confirmSound);
    }

    public void PlayBack()
    {
        if (backSound != null) selectionSource.PlayOneShot(backSound);
    }

    public void PlayLocked()
    {
        if (lockedSound == null) lockedSound = Resources.Load<AudioClip>("Audio/SE/locked");
        if (lockedSound != null) selectionSource.PlayOneShot(lockedSound);
    }

    public void PlayUnlocked()
    {
        if (unlockedSound == null) unlockedSound = Resources.Load<AudioClip>("Audio/SE/unlocked");
        if (unlockedSound != null) selectionSource.PlayOneShot(unlockedSound);
    }

    public void PlayThrowItem()
    {
        if (throwItemSound == null) throwItemSound = Resources.Load<AudioClip>("Audio/SE/throw");
        if (throwItemSound != null) selectionSource.PlayOneShot(throwItemSound);
    }

    public void PlayPutItem()
    {
        if (putItemSound == null) putItemSound = Resources.Load<AudioClip>("Audio/SE/put_item");
        if (putItemSound != null) selectionSource.PlayOneShot(putItemSound);
    }

    public AudioSource PlayItemLoop(GameObject target)
    {
        if (itemIngSound == null) itemIngSound = Resources.Load<AudioClip>("Audio/SE/item_ing");
        if (itemIngSound == null || target == null) return null;
        var source = target.AddComponent<AudioSource>();
        source.clip = itemIngSound;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = itemLoopVolume;
        source.Play();
        return source;
    }

    public void PlayStep(AudioClip clip, float volume)
    {
        if (clip != null) selectionSource.PlayOneShot(clip, volume);
    }

    public void PlayStageClear()
    {
        if (stageClearPlayed) return;
        stageClearPlayed = true;
        bgmSource.Stop();
        bgmSource.clip = null;
        if (stageClearSound != null) selectionSource.PlayOneShot(stageClearSound);
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Instance = null;
    }
}
