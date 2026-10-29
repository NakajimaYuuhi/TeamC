using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

//リザルトのボタン1つ
//選択中は黄色の画像にして脈打たせる
//マウスを乗せると選択、クリックで決定。EventSystem と InputSystemUIInputModule が必要
[RequireComponent(typeof(Image))]
public class ResultButton : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField, Tooltip("ふだんの画像")]
    Sprite normal;
    [SerializeField, Tooltip("選択中の画像")]
    Sprite selected;
    [SerializeField, Tooltip("脈打つときの大きさ")]
    float pulseScale = 1.05f;
    [SerializeField, Tooltip("脈打つ1往復の秒数")]
    float pulseTime = 0.9f;

    public IResultButtonOwner Owner { get; set; }

    Image image;
    Tween pulse;

    Image Img => image != null ? image : (image = GetComponent<Image>());

    public void SetSelected(bool on)
    {
        Img.sprite = on ? selected : normal;
        pulse?.Kill();
        transform.localScale = Vector3.one;
        if (!on) return;
        pulse = transform.DOScale(pulseScale, pulseTime * 0.5f).SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo).SetDelay(0.3f).SetUpdate(true).SetLink(gameObject);
    }

    /// <summary>押したときにへこませる</summary>
    public void PlayPress()
    {
        pulse?.Kill();
        transform.localScale = Vector3.one;
        transform.DOPunchScale(Vector3.one * -0.1f, 0.2f, 1, 0f).SetUpdate(true).SetLink(gameObject);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (Owner != null) Owner.OnHover(this);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (Owner != null) Owner.OnClick(this);
    }
}

//ボタンを受け持つ画面
public interface IResultButtonOwner
{
    void OnHover(ResultButton b);
    void OnClick(ResultButton b);
}
