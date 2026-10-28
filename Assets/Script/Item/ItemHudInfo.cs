using UnityEngine;

//落ちているアイテムに付けて、HUD に出す見た目を教える
//・PlayerItemController が拾うと、PlayerItemHud がこれを読んで HUD に出す
public class ItemHudInfo : MonoBehaviour
{
    [Tooltip("HUD に出すアイテムの見た目")]
    public HudItemData data;
}
