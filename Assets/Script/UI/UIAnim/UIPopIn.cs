using UnityEngine;
using DG.Tweening;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

//UI を OutBack で表示するコンポーネント
//付けたオブジェクトが表示される(SetActive(true))たびに自動で再生される
//スクリプトからは Show() / Hide() でも出し入れできる
[RequireComponent(typeof(RectTransform))]
public class UIPopIn : MonoBehaviour
{
    [Header("動き")]
    [SerializeField] float duration = 0.45f;
    [SerializeField] float delay = 0f;
    [Tooltip("行き過ぎる量。大きいほど弾む(標準 1.70158)")]
    [SerializeField] float overshoot = 1.7f;
    [Tooltip("どの大きさから出てくるか(0 = 点から / 1 = 大きさは変えない)")]
    [SerializeField] float fromScale = 0f;
    [Tooltip("どの角度から出てくるか(度)。Z を -360 にすると1回転しながら出る")]
    [SerializeField] Vector3 fromRotation = new Vector3(0, 0, -180);

    [Header("オプション")]
    [Tooltip("一緒にフェードインする(CanvasGroup を自動で付ける)")]
    [SerializeField] bool fade = true;
    [Tooltip("ポーズ中(timeScale = 0)でも動かす")]
    [SerializeField] bool ignoreTimeScale = true;
    [Tooltip("表示されたら自動で再生する")]
    [SerializeField] bool playOnEnable = true;
    [Tooltip("P キーで再生する(動きの確認用)")]
    [SerializeField] bool playWithPKey = true;

    Vector3 baseScale;
    Vector3 baseRotation;
    CanvasGroup group;
    Sequence seq;

    void Awake()
    {
        baseScale = transform.localScale;
        baseRotation = transform.localEulerAngles;
        if (fade && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        //チェックが入っていたら表示時に自動再生
        if (playOnEnable) Show();
    }

    void OnDisable()
    {
        seq?.Kill();
    }

    void Update()
    {
        //P キーを押すたびに最初から再生
        if (playWithPKey && PressedP()) Show();
    }

    //新旧どちらの入力方式でも P キーの押下を判定する
    static bool PressedP()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.P);
#endif
    }

    //OutBack で回転・拡大しながら表示する
    public void Show()
    {
        //このスクリプトがついているオブジェクトが非アクティブだったら
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);//アクティブにする
            if (playOnEnable) return; // OnEnable から Show() が呼ばれるので二重に再生しない
        }

        seq?.Kill();//アニメーション再生中なら強制終了
        transform.localScale = baseScale * fromScale;
        transform.localEulerAngles = baseRotation + fromRotation;
        if (group) group.alpha = 0f;

        seq = DOTween.Sequence()
            .AppendInterval(delay)
            .Append(transform.DOScale(baseScale, duration).SetEase(Ease.OutBack, overshoot))
            // FastBeyond360: 360度を超える回転も指定どおりに回す(最短ルートで止まらない)
            .Join(transform.DOLocalRotate(baseRotation, duration, RotateMode.FastBeyond360).SetEase(Ease.OutBack, overshoot))
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject);
        if (group) seq.Join(FadeTo(1f, duration * 0.5f));
    }

    //InBack で回転・縮小しながら消す(最後に非表示になる)
    public void Hide()
    {
        seq?.Kill();
        seq = DOTween.Sequence()
            .Append(transform.DOScale(baseScale * fromScale, duration * 0.7f).SetEase(Ease.InBack, overshoot))
            .Join(transform.DOLocalRotate(baseRotation + fromRotation, duration * 0.7f, RotateMode.FastBeyond360).SetEase(Ease.InBack, overshoot))
            .SetUpdate(ignoreTimeScale)
            .SetLink(gameObject)
            .OnComplete(() => gameObject.SetActive(false));
        if (group) seq.Join(FadeTo(0f, duration * 0.7f));
    }

    //CanvasGroup のフェード(DOTween の UI モジュールがなくても動くよう汎用の To で書く)
    Tween FadeTo(float alpha, float time)
    {
        return DOTween.To(() => group.alpha, a => group.alpha = a, alpha, time);
    }
}
