using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

//タイトルの右上の「データリセット」。R キーか、ボタンを押すと確認を出す
//確認は いいえ を選んだ状態で開く。左右でえらび、決定で実行、キャンセルで閉じる
public class TitleReset : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("確認のあいだ、メニューの入力を止める")]
    [SerializeField] TitleScreen title;
    [Tooltip("右上のボタン")]
    [SerializeField] RectTransform button;
    [Tooltip("確認の画面。暗い幕と板と はい いいえ の親")]
    [SerializeField] CanvasGroup dialog;
    [Tooltip("確認の板。開くときにはずませる")]
    [SerializeField] RectTransform panel;
    [SerializeField] Image yes;
    [SerializeField] Image no;
    [SerializeField] Sprite yesOn, yesOff, noOn, noOff;
    [Tooltip("リセットしたあとに出す札")]
    [SerializeField] CanvasGroup done;

    [Header("入力")]
    [SerializeField] InputActionReference navigateAction;
    [SerializeField] InputActionReference submitAction;
    [SerializeField] InputActionReference cancelAction;

    [Header("消すもの")]
    [Tooltip("ステージの数")]
    [SerializeField] int stageCount = 5;

    bool open;
    bool yesSelected;
    float lastX;

    void Awake()
    {
        SetOpen(false, true);
        if (done) done.alpha = 0f;
        var b = button ? button.GetComponent<Button>() : null;
        if (b) b.onClick.AddListener(Open);
    }

    void OnEnable()
    {
        foreach (var r in new[] { navigateAction, submitAction, cancelAction })
            if (r != null && r.action != null) r.action.Enable();
    }

    void Update()
    {
        if (!open)
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Open();
            return;
        }
        //左右でえらぶ。倒したときだけ1回動かす
        float x = navigateAction != null && navigateAction.action != null ? navigateAction.action.ReadValue<Vector2>().x : 0f;
        if (Mathf.Abs(x) > 0.5f && Mathf.Abs(lastX) <= 0.5f) Select(x < 0f, true);
        lastX = x;
        if (Pressed(cancelAction)) Close(false);
        else if (Pressed(submitAction)) Close(yesSelected);
    }

    static bool Pressed(InputActionReference r) => r != null && r.action != null && r.action.WasPressedThisFrame();

    //確認を開く。いいえを選んでおく
    public void Open()
    {
        if (open || (title != null && (title.IsBusy || title.Locked))) return;
        open = true;
        if (title) title.Locked = true;
        lastX = 0f;
        Select(false, false);
        SetOpen(true, false);
        SceneAudioController.Instance?.PlayConfirm();
    }

    void Select(bool selectYes, bool sound)
    {
        if (sound && selectYes != yesSelected) SceneAudioController.Instance?.PlayTitleSelection();
        yesSelected = selectYes;
        if (yes) yes.sprite = selectYes ? yesOn : yesOff;
        if (no) no.sprite = selectYes ? noOff : noOn;
        var on = selectYes ? yes : no;
        if (on)
        {
            on.rectTransform.DOKill();
            on.rectTransform.localScale = Vector3.one * 1.08f;
            on.rectTransform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(on.gameObject);
        }
    }

    void Close(bool reset)
    {
        open = false;
        if (reset) SceneAudioController.Instance?.PlayConfirm();
        else SceneAudioController.Instance?.PlayBack();
        if (reset)
        {
            StageProgress.ResetAll(stageCount);
            ShowDone();
        }
        SetOpen(false, false);
        //閉じたのと同じ決定でメニューが動かないように、1フレームあとで戻す
        StartCoroutine(Unlock());
    }

    IEnumerator Unlock()
    {
        yield return null;
        if (title) title.Locked = false;
    }

    void SetOpen(bool on, bool instant)
    {
        if (!dialog) return;
        dialog.DOKill();
        dialog.blocksRaycasts = on;
        dialog.interactable = on;
        if (instant) { dialog.alpha = on ? 1f : 0f; return; }
        DOTween.To(() => dialog.alpha, a => dialog.alpha = a, on ? 1f : 0f, 0.15f).SetTarget(dialog).SetLink(dialog.gameObject);
        if (on && panel)
        {
            panel.DOKill();
            panel.localScale = Vector3.one * 0.85f;
            panel.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(panel.gameObject);
        }
    }

    void ShowDone()
    {
        if (!done) return;
        done.DOKill();
        done.alpha = 1f;
        DOTween.Sequence().AppendInterval(1.4f).Append(DOTween.To(() => done.alpha, a => done.alpha = a, 0f, 0.4f)).SetTarget(done).SetLink(done.gameObject);
    }
}
