using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static ResultTween;

//リザルトとゲームオーバーで共通のボタンと入力
//演出中に決定を押すと最後まで飛ばす
public abstract class ResultScreenBase : MonoBehaviour, IResultButtonOwner
{
    [Header("ボタン")]
    [SerializeField] ResultButton retryButton;
    [SerializeField] ResultButton backButton;
    [SerializeField, Tooltip("リトライで読み込むシーン")]
    string retryScene = "PlayTest";
    [SerializeField, Tooltip("ステージセレクトのシーン")]
    string stageSelectScene = "StageSelect";
    [SerializeField, Tooltip("ここに何か入れると、シーンを読み込むかわりにこれを呼ぶ")]
    UnityEvent onRetry;
    [SerializeField] UnityEvent onBack;

    [Header("入力")]
    [SerializeField, Tooltip("UI/Navigate")]
    InputActionReference navigateAction;
    [SerializeField, Tooltip("UI/Submit")]
    InputActionReference submitAction;

    protected Sequence seq;
    ResultButton[] buttons;
    Vector2[] buttonPos;
    bool inputReady, pressed;
    int selected, lastNav;

    protected virtual void Awake()
    {
        buttons = new[] { retryButton, backButton };
        buttonPos = new Vector2[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].Owner = this;
            buttonPos[i] = Rt(buttons[i]).anchoredPosition;
        }
    }

    void OnEnable()
    {
        if (navigateAction != null) navigateAction.action.Enable();
        if (submitAction != null) submitAction.action.Enable();
    }

    //演出を作り直す前に呼ぶ
    protected void BeginSequence()
    {
        seq?.Kill();
        inputReady = false;
        pressed = false;
        seq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
    }

    //ボタンを下から出す。最初はリトライを選択
    protected void InsertButtons(float delay)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var g = Group(buttons[i].gameObject);
            var rt = Rt(buttons[i]);
            g.alpha = 0f;
            rt.anchoredPosition = buttonPos[i] - new Vector2(0f, 90f);
            float t = delay + i * 0.09f;
            seq.Insert(t, Fade(g, 1f, 0.15f));
            seq.Insert(t, Move(rt, buttonPos[i], 0.3f).SetEase(Ease.OutBack));
        }
        Select(0);
        seq.OnComplete(() => inputReady = true);
    }

    void Update()
    {
        if (pressed) return;
        bool submit = submitAction != null && submitAction.action.WasPressedThisFrame();
        if (!inputReady)
        {
            if (submit) Skip();
            return;
        }

        //上下で選ぶ。押しっぱなしでは1つだけ動く
        float y = navigateAction != null ? navigateAction.action.ReadValue<Vector2>().y : 0f;
        int nav = y > 0.5f ? -1 : y < -0.5f ? 1 : 0;
        if (nav != 0 && nav != lastNav) Select(Mathf.Clamp(selected + nav, 0, buttons.Length - 1));
        lastNav = nav;

        if (submit) Press(selected);
    }

    public void OnHover(ResultButton b)
    {
        if (!inputReady || pressed) return;
        int i = System.Array.IndexOf(buttons, b);
        if (i >= 0 && i != selected) Select(i);
    }

    public void OnClick(ResultButton b)
    {
        if (pressed) return;
        if (!inputReady)
        {
            Skip();
            return;
        }
        int i = System.Array.IndexOf(buttons, b);
        if (i >= 0) Press(i);
    }

    void Skip()
    {
        if (seq != null && seq.IsActive()) seq.Complete(true);
    }

    void Select(int i)
    {
        if (inputReady && selected != i) SceneAudioController.Instance?.PlayResultSelection();
        selected = i;
        for (int j = 0; j < buttons.Length; j++) buttons[j].SetSelected(j == i);
    }

    void Press(int i)
    {
        pressed = true;
        SceneAudioController.Instance?.PlayConfirm();
        buttons[i].PlayPress();
        DOVirtual.DelayedCall(0.15f, () =>
        {
            if (i == 0) Go(onRetry, retryScene);
            else Go(onBack, stageSelectScene);
        }).SetUpdate(true).SetLink(gameObject);
    }

    static void Go(UnityEvent ev, string scene)
    {
        if (ev != null && ev.GetPersistentEventCount() > 0)
        {
            ev.Invoke();
            return;
        }
        //チームの画面切り替えがあれば使う
        if (CrowdSceneTransition.Instance != null) CrowdSceneTransition.Instance.LoadScene(scene);
        else SceneManager.LoadScene(scene);
    }
}
