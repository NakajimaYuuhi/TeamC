using DG.Tweening;
using UnityEngine;

//ステージのカード1枚のロック。カードのいちばん上に付ける
//前のステージをクリアするまで、シャッターと南京錠をかけて、あそぶボタンを解放の札に変える
//解放されて初めて見たときだけ、鍵が外れてシャッターが上がる演出を流す
public class StageLock : MonoBehaviour
{
    [Tooltip("このカードのステージ番号。0 始まり")]
    [SerializeField] int stageIndex;

    [Header("部品")]
    [Tooltip("シャッター。ステージの絵を隠す。RectMask2D の付いた親の中に置く")]
    [SerializeField] RectTransform shutter;
    [Tooltip("南京錠")]
    [SerializeField] RectTransform padlock;
    [Tooltip("あそぶボタンのかわりに出す札")]
    [SerializeField] RectTransform lockPlate;
    [Tooltip("あそぶボタン")]
    [SerializeField] GameObject playButton;
    [Tooltip("ステージの絵。ロック中は消す。となりのカードが薄く出るとき、シャッターごしに透けて見えないように")]
    [SerializeField] GameObject preview;

    [Header("解放の演出")]
    [Tooltip("ステージセレクトを開いてから演出を始めるまでの秒数")]
    [SerializeField] float unlockDelay = 0.6f;
    [Tooltip("シャッターが上がる秒数")]
    [SerializeField] float shutterTime = 0.52f;

    public bool IsLocked { get; private set; }
    public int StageIndex => stageIndex;

    Vector2 shutterPos, padlockPos, platePos;
    Sequence seq;

    void Awake()
    {
        shutterPos = shutter.anchoredPosition;
        padlockPos = padlock.anchoredPosition;
        platePos = lockPlate.anchoredPosition;
    }

    void Start()
    {
        bool unlocked = StageProgress.IsUnlocked(stageIndex);
        if (unlocked && StageProgress.IsNewlyUnlocked(stageIndex))
        {
            SetLocked(true);
            PlayUnlock(unlockDelay);
            StageProgress.MarkUnlockSeen(stageIndex);
        }
        else
        {
            SetLocked(!unlocked);
        }
    }

    /// <summary>ロック中か解放後の姿にすぐ切り替える</summary>
    public void SetLocked(bool locked)
    {
        seq?.Kill();
        IsLocked = locked;
        shutter.gameObject.SetActive(locked);
        padlock.gameObject.SetActive(locked);
        lockPlate.gameObject.SetActive(locked);
        if (playButton != null) playButton.SetActive(!locked);
        if (preview != null) preview.SetActive(!locked);
        shutter.anchoredPosition = shutterPos;
        padlock.anchoredPosition = padlockPos;
        padlock.localEulerAngles = Vector3.zero;
        lockPlate.anchoredPosition = platePos;
        lockPlate.localScale = Vector3.one;
        Group(padlock).alpha = 1f;
        Group(lockPlate).alpha = 1f;
        if (playButton != null) playButton.transform.localScale = Vector3.one;
    }

    /// <summary>鍵が外れてシャッターが上がり、札があそぶボタンに変わる</summary>
    public void PlayUnlock(float delay = 0f)
    {
        SetLocked(true);
        IsLocked = false;
        var lockGroup = Group(padlock);
        var plateGroup = Group(lockPlate);
        float h = shutter.rect.height;

        seq = DOTween.Sequence().SetLink(gameObject);
        seq.AppendInterval(delay);
        //鍵がガチャガチャ揺れる
        seq.Append(padlock.DOPunchRotation(new Vector3(0f, 0f, 16f), 0.4f, 8, 0.6f));
        seq.InsertCallback(delay + 0.4f, () =>
        {
            SceneAudioController.Instance?.PlayUnlocked();
        });
        //鍵が外れて落ちる
        seq.Append(DOTween.To(() => padlock.anchoredPosition, v => padlock.anchoredPosition = v, padlockPos + new Vector2(0f, -620f), 0.6f).SetEase(Ease.InQuad));
        seq.Join(padlock.DOLocalRotate(new Vector3(0f, 0f, -40f), 0.6f).SetEase(Ease.InQuad));
        seq.Join(DOTween.To(() => lockGroup.alpha, v => lockGroup.alpha = v, 0f, 0.6f).SetEase(Ease.InQuad));
        //シャッターが上がる。上がり始めたら絵を出す
        seq.InsertCallback(delay + 0.62f, () => { if (preview != null) preview.SetActive(true); });
        seq.Insert(delay + 0.62f, DOTween.To(() => shutter.anchoredPosition, v => shutter.anchoredPosition = v, shutterPos + new Vector2(0f, h + 12f), shutterTime).SetEase(Ease.InOutCubic));
        //札が消えて、あそぶボタンが出る
        float t = delay + 0.62f + shutterTime;
        seq.Insert(t, DOTween.To(() => plateGroup.alpha, v => plateGroup.alpha = v, 0f, 0.16f));
        seq.InsertCallback(t + 0.12f, () =>
        {
            if (playButton == null) return;
            playButton.SetActive(true);
            playButton.transform.localScale = Vector3.one * 0.6f;
        });
        if (playButton != null)
            seq.Insert(t + 0.12f, playButton.transform.DOScale(1f, 0.32f).SetEase(Ease.OutBack));
        seq.OnComplete(() =>
        {
            shutter.gameObject.SetActive(false);
            padlock.gameObject.SetActive(false);
            lockPlate.gameObject.SetActive(false);
        });
    }

    /// <summary>ロック中に決定を押したとき。南京錠と札を揺らして入れないことを伝える</summary>
    public void Rattle()
    {
        if (!IsLocked) return;
        SceneAudioController.Instance?.PlayLocked();
        padlock.DOComplete();
        lockPlate.DOComplete();
        padlock.DOPunchRotation(new Vector3(0f, 0f, 18f), 0.35f, 10, 0.6f).SetLink(gameObject);
        DOTween.Punch(() => (Vector3)lockPlate.anchoredPosition, v => lockPlate.anchoredPosition = v, new Vector3(18f, 0f, 0f), 0.3f, 10, 0.6f)
            .SetTarget(lockPlate).SetLink(gameObject);
    }

    static CanvasGroup Group(Component c)
    {
        var g = c.GetComponent<CanvasGroup>();
        return g != null ? g : c.gameObject.AddComponent<CanvasGroup>();
    }
}
