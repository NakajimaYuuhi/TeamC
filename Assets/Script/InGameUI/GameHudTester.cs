using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//GameHud の見た目を確かめるための仮の操作
//・Inspector の数字を動かすと、そのまま画面に出る
//・左クリックでアイテムモード → 左クリックで投げる / 右クリックでやめる
//・「拾う」のアクションで、テスト用のアイテムを1つ拾う
public class GameHudTester : MonoBehaviour
{
    [SerializeField] GameHud hud;

    [Header("表示する値")]
    [SerializeField] float time = 60f;
    [Tooltip("オンなら残り時間が減っていく(0 になったら 60 に戻る)")]
    [SerializeField] bool countDown = true;
    [SerializeField, Range(0, 1000)] int distance = 1000;
    [Tooltip("ついてきている人の人数")]
    [SerializeField, Range(0, 60)] int followers = 7;

    [Header("テスト用のアイテム(拾うたびに順番に使う)")]
    [SerializeField] List<HudItemData> testItems = new List<HudItemData>();
    [Tooltip("最初から持っているアイテム(3つまで)")]
    [SerializeField] List<HudItemData> startItems = new List<HudItemData>();

    [Header("入力(Input Actions)")]
    [Tooltip("左クリック(アイテムモード / 投げる)")]
    [SerializeField] InputActionReference itemAction;
    [Tooltip("右クリック(アイテムモードをやめる)")]
    [SerializeField] InputActionReference cancelAction;
    [Tooltip("テスト用に1つ拾う")]
    [SerializeField] InputActionReference pickAction;

    int nextTest;

    void OnEnable()
    {
        foreach (var r in new[] { itemAction, cancelAction, pickAction })
            if (r != null && r.action != null) r.action.Enable();
    }

    void Start()
    {
        if (hud) hud.SetItems(startItems);
    }

    void Update()
    {
        if (!hud) return;
        if (countDown)
        {
            time -= Time.deltaTime;
            if (time < 0f) time = 60f;
        }
        hud.SetTime(time);
        hud.SetDistance(distance);
        hud.SetFollowers(followers);

        if (Pressed(pickAction) && testItems.Count > 0)
        {
            hud.AddItem(testItems[nextTest % testItems.Count]);
            nextTest++;
        }
        if (Pressed(itemAction))
        {
            if (!hud.ItemMode)
            {
                if (hud.ItemCount > 0) hud.SetItemMode(true);
                else hud.ShakeEmpty();
            }
            else
            {
                var thrown = hud.TakeItem();
                Debug.Log($"[HUD テスト] 投げた: {(thrown ? thrown.name : "なし")}");
                hud.SetItemMode(false);
            }
        }
        if (Pressed(cancelAction) && hud.ItemMode) hud.SetItemMode(false);
    }

    static bool Pressed(InputActionReference r) => r != null && r.action != null && r.action.WasPressedThisFrame();
}
