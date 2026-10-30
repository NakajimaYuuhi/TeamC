using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

//¡‚¢‚éŠK‚ğ HUD ‚Éo‚·BŠK‚Ì‚ ‚éƒXƒe[ƒW‚Å‚¾‚¯•\¦‚·‚é
public class HudFloor : MonoBehaviour
{
    [Tooltip("ŠK‚ÌD‚ğo‚·Š")]
    [SerializeField] Image badge;
    [Tooltip("hud_floor_1`4 ‚ğ‡”Ô‚É")]
    [SerializeField] Sprite[] floorSprites;
    [Tooltip("ŠK‚ª•Ï‚í‚Á‚½‚Æ‚«‚ÉA‚Ó‚­‚ç‚Ş‘å‚«‚³")]
    [SerializeField] float popScale = 1.25f;
    [Tooltip("‚Ó‚­‚ç‚ñ‚Å–ß‚é‚Ü‚Å‚Ì•b”")]
    [SerializeField] float popTime = 0.35f;

    StageFloors floors;

    void Start()
    {
        floors = FindFirstObjectByType<StageFloors>();
        if (floors == null)
        {
            gameObject.SetActive(false);
            return;
        }
        floors.FloorChanged += OnFloorChanged;
        if (floors.CurrentFloor > 0) Show(floors.CurrentFloor, false);
    }

    void OnDestroy()
    {
        if (floors != null) floors.FloorChanged -= OnFloorChanged;
    }

    void OnFloorChanged(int floor)
    {
        Show(floor, true);
    }

    void Show(int floor, bool pop)
    {
        int i = floor - 1;
        if (badge == null || floorSprites == null || i < 0 || i >= floorSprites.Length || floorSprites[i] == null) return;
        badge.sprite = floorSprites[i];
        badge.SetNativeSize();
        if (!pop) return;
        var t = badge.rectTransform;
        t.DOKill();
        t.localScale = Vector3.one * popScale;
        t.DOScale(1f, popTime).SetEase(Ease.OutBack).SetLink(badge.gameObject);
    }
}
