using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

//タイトルの「あそびかた」。幕が閉じたところで開き、ページを左右でめくる
//決定で次のページ、最後のページで決定かキャンセルを押すと閉じて、タイトルの登場をもう一度流す
public class HowToScreen : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("開いているあいだ、メニューの入力を止める")]
    [SerializeField] TitleScreen title;
    [Tooltip("あそびかた全体。開くときに濃くする")]
    [SerializeField] CanvasGroup group;
    [Tooltip("ページ。1ページ目から順に")]
    [SerializeField] RectTransform[] pages;

    [Header("入力")]
    [SerializeField] InputActionReference navigateAction;
    [SerializeField] InputActionReference submitAction;
    [SerializeField] InputActionReference cancelAction;

    [Header("動き")]
    [Tooltip("開く、閉じるの秒数")]
    [SerializeField] float fadeTime = 0.2f;
    [Tooltip("めくるときにページがすべってくる距離")]
    [SerializeField] float slide = 60f;
    [Tooltip("めくるときの秒数")]
    [SerializeField] float slideTime = 0.22f;

    bool open;
    int page;
    float lastX;
    Vector2[] basePos;

    void Awake()
    {
        basePos = new Vector2[pages.Length];
        for (int i = 0; i < pages.Length; i++) basePos[i] = pages[i].anchoredPosition;
        SetVisible(false, true);
    }

    void OnEnable()
    {
        foreach (var r in new[] { navigateAction, submitAction, cancelAction })
            if (r != null && r.action != null) r.action.Enable();
    }

    //タイトルの onHowToPlay から呼ぶ
    public void Open()
    {
        if (open) return;
        open = true;
        if (title) title.Locked = true;
        lastX = 0f;
        Show(0, 0);
        SetVisible(true, false);
    }

    void Update()
    {
        if (!open) return;
        float x = navigateAction != null && navigateAction.action != null ? navigateAction.action.ReadValue<Vector2>().x : 0f;
        if (Mathf.Abs(x) > 0.5f && Mathf.Abs(lastX) <= 0.5f) Flip(x > 0f ? 1 : -1);
        lastX = x;
        if (Pressed(cancelAction)) Close();
        else if (Pressed(submitAction))
        {
            if (page + 1 < pages.Length) Flip(1);
            else Close();
        }
    }

    static bool Pressed(InputActionReference r) => r != null && r.action != null && r.action.WasPressedThisFrame();

    void Flip(int dir)
    {
        int next = Mathf.Clamp(page + dir, 0, pages.Length - 1);
        if (next == page) return;
        SceneAudioController.Instance?.PlayTitleSelection();
        Show(next, dir);
    }

    //ページを切り替える。dir の向きから、すべり込んでくる
    void Show(int index, int dir)
    {
        page = index;
        for (int i = 0; i < pages.Length; i++) pages[i].gameObject.SetActive(i == index);
        var p = pages[index];
        DOTween.Kill(p);
        p.anchoredPosition = basePos[index] + new Vector2(slide * dir, 0f);
        var to = basePos[index];
        if (dir != 0) DOTween.To(() => p.anchoredPosition, v => p.anchoredPosition = v, to, slideTime).SetEase(Ease.OutCubic).SetTarget(p).SetLink(p.gameObject);
    }

    void Close()
    {
        open = false;
        SceneAudioController.Instance?.PlayBack();
        SetVisible(false, false);
        StartCoroutine(Return());
    }

    //閉じたのと同じ決定でメニューが動かないように、1フレームあとで戻してからタイトルを出し直す
    IEnumerator Return()
    {
        yield return null;
        if (title)
        {
            title.Locked = false;
            //幕の演出がまだ続いていれば、そちらが登場を流す
            if (!title.IsBusy) title.PlayIntro();
        }
    }

    void SetVisible(bool on, bool instant)
    {
        if (!group) return;
        DOTween.Kill(group);
        group.blocksRaycasts = on;
        group.interactable = on;
        if (instant) { group.alpha = on ? 1f : 0f; return; }
        DOTween.To(() => group.alpha, a => group.alpha = a, on ? 1f : 0f, fadeTime).SetTarget(group).SetLink(group.gameObject);
    }
}
