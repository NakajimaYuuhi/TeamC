// 10/27 佐藤作成

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DG.Tweening;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//ステージ選択「カード」の動きだけを担当する
//・Card Root の直下に手で置いたカードを、上から順に左→右の並びとして扱う
//・選択中を中央に大きく、前後を左右に小さく薄く並べ、選択が変わると滑らせる
//・入力は Input Actions から読む(UI/Navigate で選択、UI/Submit で決定、UI/Cancel でもどる)
//・矢印ボタン・カード内のボタンはマウスやタッチでも押せる
//・決定・もどるの先は決めない。下のイベントに後からつなぐ
public class StageSelectCards : MonoBehaviour
{
    [System.Serializable] public class IndexEvent : UnityEvent<int> { }

    [SerializeField, InspectorName("ゲーム保持データ")]
    private GameData _gameData;

    [Header("カード")]
    [Tooltip("カードを並べた親")]
    [SerializeField] RectTransform cardRoot;
    [Tooltip("カードの中にこの名前の子があれば、選択中のカードだけ表示する")]
    [SerializeField] string focusObjectName = "FocusFrame";

    [Header("並び方")]
    [Tooltip("カードどうしの間隔。大きくすると左右のカードが外側に離れる")]
    [SerializeField] float spacing = 760f;
    [SerializeField] float sideScale = 0.72f;
    [SerializeField] float sideAlpha = 0.55f;
    [Tooltip("左右それぞれ何枚まで見せるか")]
    [SerializeField] int visibleSide = 1;
    [SerializeField] int startIndex = 0;

    [Header("動き")]
    [SerializeField] float moveTime = 0.3f;
    [SerializeField] Ease moveEase = Ease.OutCubic;
    [Tooltip("端でさらに押したときの揺れ幅(px)")]
    [SerializeField] float edgeShake = 36f;
    [Tooltip("決定したときに弾む大きさ")]
    [SerializeField] float decidePunch = 0.08f;

    [Header("ページの点")]
    [SerializeField] RectTransform dotsRoot;
    [SerializeField] Image dotPrefab;
    [SerializeField] Color dotOn = new Color32(0xFF, 0xCF, 0x3F, 0xFF);
    [SerializeField] Color dotOff = new Color32(0x6A, 0x64, 0x78, 0xFF);
    [SerializeField] Vector2 dotSizeOn = new Vector2(42, 18);
    [SerializeField] Vector2 dotSizeOff = new Vector2(18, 18);

    [Header("左右の矢印")]
    [Tooltip("左の矢印(PrevButton)")]
    [SerializeField] RectTransform prevArrow;
    [Tooltip("右の矢印(NextButton)")]
    [SerializeField] RectTransform nextArrow;
    [Tooltip("矢印が消える・出るまでの秒数")]
    [SerializeField] float arrowFadeTime = 0.15f;

    [Header("入力(Input Actions)")]
    [Tooltip("左右の選択")]
    [SerializeField] InputActionReference navigateAction;
    [Tooltip("決定")]
    [SerializeField] InputActionReference submitAction;
    [Tooltip("もどる")]
    [SerializeField] InputActionReference cancelAction;
    [Tooltip("押しっぱなしにしたとき、次に進み始めるまでの秒数")]
    [SerializeField] float repeatDelay = 0.4f;
    [Tooltip("押しっぱなし中に1枚進む間隔(秒)")]
    [SerializeField] float repeatInterval = 0.15f;

    [Header("イベント")]
    [Tooltip("選択が変わったとき。選ばれたカードの番号(0～)が渡される")]
    [SerializeField] IndexEvent onSelectionChanged = new IndexEvent();
    [Tooltip("決定の演出が終わったとき。決定したカードの番号(0～)が渡される")]
    [SerializeField] IndexEvent onDecided = new IndexEvent();
    [Tooltip("もどるを押したとき")]
    [SerializeField] UnityEvent onBack = new UnityEvent();

    public IndexEvent OnSelectionChanged => onSelectionChanged;
    public IndexEvent OnDecided => onDecided;
    public UnityEvent OnBack => onBack;
    public int SelectedIndex => selected;

    class Card
    {
        public RectTransform rect;
        public CanvasGroup group;
        public GameObject focus;
        public Button[] buttons;
    }

    readonly List<Card> cards = new List<Card>();
    readonly List<Image> dots = new List<Image>();
    int selected;
    bool busy; //決定の演出中は入力を受けない
    int heldDir;
    float repeatTimer;

    void OnEnable()
    {
        //アクションは他の画面と共有なので、ここでは有効にするだけで無効にはしない
        foreach (var r in new[] { navigateAction, submitAction, cancelAction })
            if (r != null && r.action != null) r.action.Enable();
        if (navigateAction == null || submitAction == null || cancelAction == null)
            Debug.LogWarning("[ステージセレクト] Input Actions が未設定の欄があります(Navigate / Submit / Cancel)");
    }

    void Start()
    {
        CollectCards();
        selected = Mathf.Clamp(startIndex, 0, Mathf.Max(0, cards.Count - 1));
        Layout(false);
    }

    void Update()
    {
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null) es.SetSelectedGameObject(null);

        if (busy) return;
        ReadInput(out int move, out bool ok, out bool back);
        if (move != 0) Move(move);
        if (ok) Decide();
        if (back) Back();
    }

    // ---------- ボタンからも呼べる操作 ----------

    public void Next() => Move(1);
    public void Prev() => Move(-1);

    public void Quit()
    {
        if (busy) return;
        busy = true;
        SceneAudioController.Instance?.PlayBack();
        UIChaosGatherTransition.Instance.LoadScene("Title");
    }

    //番号を指定して選ぶ
    public void Select(int index)
    {
        if (cards.Count == 0) return;
        index = Mathf.Clamp(index, 0, cards.Count - 1);
        if (index == selected) return;
        selected = index;
        SceneAudioController.Instance?.PlayStageSelection();
        Layout(true);
        onSelectionChanged.Invoke(selected);
    }

    //決定。選択中のカードを弾ませてから onDecided を呼ぶ
    public void Decide()
    {
        if (busy || cards.Count == 0) return;
        var card = cards[selected];
        busy = true;
        SceneAudioController.Instance?.PlayConfirm();
        card.rect.DOComplete(); // 移動中なら先に定位置へ
        DOTween.Complete(card.group);
        card.rect.DOPunchScale(Vector3.one * decidePunch, 0.2f, 6, 1f)
            .SetLink(card.rect.gameObject)
            .OnComplete(() =>
            {
                busy = false;
                Debug.Log($"[ステージセレクト] 決定: {selected} ({card.rect.name})");
                onDecided.Invoke(selected);
            });

        UIChaosGatherTransition.Instance.LoadScene("PlayTest");
    }

    public void Back()
    {
        if (busy) return;
        Debug.Log("[ステージセレクト] もどる");
        onBack.Invoke();
    }

    // ---------- カードの並び ----------

    void CollectCards()
    {
        cards.Clear();
        foreach (Transform child in cardRoot)
        {
            if (!child.gameObject.activeSelf || !(child is RectTransform rt)) continue;
            var card = new Card
            {
                rect = rt,
                group = rt.TryGetComponent(out CanvasGroup g) ? g : rt.gameObject.AddComponent<CanvasGroup>(),
                focus = string.IsNullOrEmpty(focusObjectName) ? null : rt.Find(focusObjectName)?.gameObject,
                buttons = rt.GetComponentsInChildren<Button>(true),
            };
            //カード内のボタン
            foreach (var b in card.buttons) b.onClick.AddListener(Decide);
            cards.Add(card);
        }

        foreach (var d in dots) if (d) Destroy(d.gameObject);
        dots.Clear();
        if (dotPrefab && dotsRoot)
            for (int i = 0; i < cards.Count; i++) dots.Add(Instantiate(dotPrefab, dotsRoot));
    }

    void Move(int dir)
    {
        if (cards.Count == 0) return;
        int next = Mathf.Clamp(selected + dir, 0, cards.Count - 1);
        if (next == selected)
        {
            //端に来たら横に揺らして選択できないことを伝えるアニメーション
            var rt = cards[selected].rect;
            rt.DOComplete();
            DOTween.Punch(() => (Vector3)rt.anchoredPosition, v => rt.anchoredPosition = v, new Vector3(-dir * edgeShake, 0, 0), 0.3f, 10, 0.5f)
                .SetTarget(rt).SetLink(rt.gameObject);
            return;
        }
        Select(next);
        _gameData.ChangeSelectNo(dir);
    }

    //ステージ。選択中を中央に、前後を左右へ並べる
    void Layout(bool animate)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            int offset = i - selected;
            bool center = offset == 0;
            bool visible = Mathf.Abs(offset) <= visibleSide;

            var pos = new Vector2(offset * spacing, 0);
            float scale = center ? 1f : sideScale;
            float alpha = center ? 1f : (visible ? sideAlpha : 0f);

            if (card.focus) card.focus.SetActive(center);
            foreach (var b in card.buttons) b.interactable = center;
            card.group.blocksRaycasts = center;

            card.rect.DOKill();
            DOTween.Kill(card.group);
            if (animate)
            {
                DOTween.To(() => card.rect.anchoredPosition, v => card.rect.anchoredPosition = v, pos, moveTime)
                    .SetEase(moveEase).SetTarget(card.rect).SetLink(card.rect.gameObject);
                card.rect.DOScale(scale, moveTime).SetEase(moveEase).SetLink(card.rect.gameObject);
                DOTween.To(() => card.group.alpha, a => card.group.alpha = a, alpha, moveTime * 0.8f)
                    .SetTarget(card.group).SetLink(card.rect.gameObject);
            }
            else
            {
                card.rect.anchoredPosition = pos;
                card.rect.localScale = Vector3.one * scale;
                card.group.alpha = alpha;
            }
        }
        //中央のカードを一番手前に描く
        if (cards.Count > 0) cards[selected].rect.SetAsLastSibling();
        UpdateDots();
        UpdateArrows(animate);
    }

    //端のカードでは、その先の矢印を消す
    void UpdateArrows(bool animate)
    {
        SetArrow(prevArrow, selected > 0, animate);
        SetArrow(nextArrow, selected < cards.Count - 1, animate);
    }

    void SetArrow(RectTransform arrow, bool show, bool animate)
    {
        if (arrow == null) return;
        var g = arrow.TryGetComponent(out CanvasGroup cg) ? cg : arrow.gameObject.AddComponent<CanvasGroup>();
        g.interactable = show;
        g.blocksRaycasts = show;
        DOTween.Kill(g);
        float alpha = show ? 1f : 0f;
        if (animate) DOTween.To(() => g.alpha, a => g.alpha = a, alpha, arrowFadeTime).SetTarget(g).SetLink(arrow.gameObject);
        else g.alpha = alpha;
    }

    void UpdateDots()
    {
        for (int i = 0; i < dots.Count; i++)
        {
            bool on = i == selected;
            dots[i].color = on ? dotOn : dotOff;
            dots[i].rectTransform.sizeDelta = on ? dotSizeOn : dotSizeOff;
        }
    }

    // ---------- 入力(Input Actions) ----------

    void ReadInput(out int move, out bool ok, out bool back)
    {
        move = 0;
        ok = WasPressed(submitAction);
        back = WasPressed(cancelAction);

        //Navigate の左右を「1枚ずつ」の入力に変える。押しっぱなしなら一定間隔でくり返す
        float x = navigateAction != null && navigateAction.action != null ? navigateAction.action.ReadValue<Vector2>().x : 0f;
        int dir = x > 0.5f ? 1 : (x < -0.5f ? -1 : 0);
        if (dir == 0)
        {
            heldDir = 0;
        }
        else if (dir != heldDir)
        {
            move = dir;
            heldDir = dir;
            repeatTimer = repeatDelay;
        }
        else
        {
            repeatTimer -= Time.unscaledDeltaTime;
            if (repeatTimer <= 0f)
            {
                move = dir;
                repeatTimer = repeatInterval;
            }
        }
    }

    static bool WasPressed(InputActionReference r)
    {
        return r != null && r.action != null && r.action.WasPressedThisFrame();
    }
}
