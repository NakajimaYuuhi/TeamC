using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

//階段に入ったプレイヤーを、画面を暗くしてから次の階の出口へ移す。階段室のトリガーに付ける
[RequireComponent(typeof(Collider))]
public class StairWarp : MonoBehaviour
{
    [Tooltip("移った先。次の階の階段の前")]
    [SerializeField] Transform exit;
    [Tooltip("暗くなるまでの秒数")]
    [SerializeField] float fadeOutTime = 0.25f;
    [Tooltip("暗いままの秒数")]
    [SerializeField] float holdTime = 0.1f;
    [Tooltip("明るくなるまでの秒数")]
    [SerializeField] float fadeInTime = 0.3f;

    static CanvasGroup fader;
    static bool busy;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnDestroy()
    {
        //シーンをまたいで動けなくならないように戻す
        busy = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (busy || exit == null) return;
        var body = other.attachedRigidbody;
        if (body == null || !body.CompareTag("Player")) return;

        busy = true;
        var f = Fader();
        DOTween.Sequence()
            .Append(DOTween.To(() => f.alpha, a => f.alpha = a, 1f, fadeOutTime))
            .AppendCallback(() => Move(body))
            .AppendInterval(holdTime)
            .Append(DOTween.To(() => f.alpha, a => f.alpha = a, 0f, fadeInTime))
            .OnComplete(() => busy = false)
            .OnKill(() => busy = false)
            .SetLink(gameObject);
    }

    void Move(Rigidbody body)
    {
        if (body == null) return;
        body.linearVelocity = Vector3.zero;
        body.position = exit.position;
        body.transform.position = exit.position;
    }

    //画面全体を覆う黒い板。最初に使うときに作る
    static CanvasGroup Fader()
    {
        if (fader != null) return fader;
        var go = new GameObject("StairFader");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        fader = go.AddComponent<CanvasGroup>();
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
        var img = new GameObject("Black").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.color = Color.black;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return fader;
    }
}
