using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//PlayerItemController の動きを、新しい HUD(GameHud)に伝える
//・PlayerItemController と同じオブジェクトに付ける。PlayerItemController は書きかえない
//・アイテムモードに入る/出る → HUD のアイテムモード
//・拾う/投げる → HUD のアイテム欄と説明の札
//・アイテムを持っていないのに左クリック → 大きい枠を揺らす
[RequireComponent(typeof(PlayerItemController))]
public class PlayerItemHud : MonoBehaviour
{
    [Tooltip("PlayerItemController の「アイテムモード開始・投擲」と同じアクション名")]
    [SerializeField] string useItemActionName = "UseItem";

    PlayerItemController controller;
    GameHud hud;
    InputAction useItemAction;
    readonly List<GameObject> shown = new List<GameObject>();   //HUD に出しているアイテム(順番どおり)

    void Awake()
    {
        controller = GetComponent<PlayerItemController>();
    }

    void OnEnable()
    {
        controller.ModeChanged += OnModeChanged;
        controller.InventoryChanged += OnInventoryChanged;
    }

    void OnDisable()
    {
        controller.ModeChanged -= OnModeChanged;
        controller.InventoryChanged -= OnInventoryChanged;
    }

    void Start()
    {
        var input = GetComponentInParent<PlayerInput>();
        if (input != null) useItemAction = input.actions.FindAction(useItemActionName);
    }

    //HUD は GameFlowManager がプレイヤーを作ったあとに作るので、使うときに探す
    GameHud Hud
    {
        get
        {
            if (hud == null)
            {
                var flow = FindFirstObjectByType<GameFlowManager>();
                if (flow != null) hud = flow.Hud;
            }
            return hud;
        }
    }

    void Update()
    {
        //持っていないのに左クリックしたら、大きい枠を揺らす
        if (useItemAction != null && useItemAction.WasPressedThisFrame()
            && controller.CurrentMode == PlayerItemController.Mode.Normal && controller.CurrentItem == null)
        {
            if (Hud != null) Hud.ShakeEmpty();
        }
    }

    void OnModeChanged(PlayerItemController.Mode mode)
    {
        if (Hud != null) Hud.SetItemMode(mode == PlayerItemController.Mode.Aiming);
    }

    //持ち物が変わったら HUD に合わせる。1つ増えた/先頭が減った ときは弾む動きつきで
    void OnInventoryChanged()
    {
        if (Hud == null) return;
        var now = controller.Items;

        //見た目が無いアイテムがあると HUD と数がずれるので、そのときは全部入れ直す
        bool allHaveData = true;
        foreach (var go in now) if (DataOf(go) == null) allHaveData = false;

        if (allHaveData && now.Count == shown.Count + 1 && SameOrder(now, 0, shown, 0, shown.Count))
        {
            Hud.AddItem(DataOf(now[now.Count - 1]));
        }
        else if (allHaveData && now.Count == shown.Count - 1 && SameOrder(now, 0, shown, 1, now.Count))
        {
            Hud.TakeItem();
        }
        else if (allHaveData && now.Count == shown.Count && now.Count > 1 && SameItems(now, shown))
        {
            //同じアイテムで並びだけ変わった(ホイールで切り替えた)
            var list = new List<HudItemData>();
            foreach (var go in now) list.Add(DataOf(go));
            Hud.SwitchItems(list);
        }
        else
        {
            var list = new List<HudItemData>();
            foreach (var go in now) list.Add(DataOf(go));
            Hud.SetItems(list);
        }

        shown.Clear();
        shown.AddRange(now);
    }

    static bool SameOrder(IReadOnlyList<GameObject> a, int aStart, List<GameObject> b, int bStart, int count)
    {
        for (int i = 0; i < count; i++)
            if (a[aStart + i] != b[bStart + i]) return false;
        return true;
    }

    //同じアイテムがそろっていて、並びだけが違うか
    static bool SameItems(IReadOnlyList<GameObject> a, List<GameObject> b)
    {
        if (SameOrder(a, 0, b, 0, b.Count)) return false;
        foreach (var go in a) if (!b.Contains(go)) return false;
        return true;
    }

    static HudItemData DataOf(GameObject item)
    {
        var info = item ? item.GetComponent<ItemHudInfo>() : null;
        if (info == null || info.data == null)
        {
            Debug.LogWarning($"[PlayerItemHud] {(item ? item.name : "?")} に ItemHudInfo(HUD の見た目)が付いていません", item);
            return null;
        }
        return info.data;
    }
}
