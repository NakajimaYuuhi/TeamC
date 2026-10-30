using System;
using System.Collections.Generic;
using UnityEngine;

//c‚Éd‚È‚Á‚½ŠK‚ğŠÇ—‚·‚éBƒvƒŒƒCƒ„[‚Ì‚¢‚éŠK‚æ‚èã‚ÌŠK‚ÍÁ‚µ‚ÄAã‚©‚çŒ©‚¦‚é‚æ‚¤‚É‚·‚é
//‚Ù‚©‚ÌŠK‚Ì NPC ‚Í~‚ß‚Ä‚¨‚­Bl‚Ì—¬‚ê‚Ìê‚Í‘S•”‚ÌŠK‚Å1–‡‚È‚Ì‚ÅAã‰º‚ÌŠK‚ª“®‚©‚È‚¢‚æ‚¤‚É‚·‚é‚½‚ß
//‚Ù‚©‚ÌŠK‚É—‚¿‚Ä‚¢‚éƒAƒCƒeƒ€‚à‰B‚·
public class StageFloors : MonoBehaviour
{
    [Tooltip("ŠK‚Ì•”•iB1ŠK‚©‚ç‡‚É•À‚×‚é")]
    [SerializeField] GameObject[] floors;
    [Tooltip("1ŠK‚Ì°‚Ì‚‚³")]
    [SerializeField] float baseHeight = 0f;
    [Tooltip("1‚Â‚ÌŠK‚Ì‚‚³")]
    [SerializeField] float floorHeight = 4f;
    [Tooltip("NPC ‚ğ’T‚µ’¼‚·ŠÔŠuB•b")]
    [SerializeField] float scanInterval = 1f;

    Transform player;
    readonly HashSet<GameObject> npcs = new HashSet<GameObject>();
    readonly HashSet<GameObject> items = new HashSet<GameObject>();
    float scanTimer;

    //¡‚ÌŠKB1ŠK‚ª 1
    public int CurrentFloor { get; private set; } = -1;
    public int FloorCount => floors != null ? floors.Length : 0;
    public event Action<int> FloorChanged;

    void Update()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }

        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = scanInterval;
            foreach (var npc in GameObject.FindGameObjectsWithTag("NPC")) npcs.Add(npc);
            foreach (var item in FindObjectsByType<ItemFloat>(FindObjectsSortMode.None)) items.Add(item.gameObject);
        }

        int floor = FloorAt(player.position.y);
        if (floor != CurrentFloor)
        {
            CurrentFloor = floor;
            for (int i = 0; i < FloorCount; i++) if (floors[i]) floors[i].SetActive(i + 1 <= floor);
            FloorChanged?.Invoke(floor);
        }

        //¡‚ÌŠK‚Ì NPC ‚¾‚¯“®‚©‚·
        npcs.RemoveWhere(n => n == null);
        foreach (var npc in npcs)
        {
            bool here = FloorAt(npc.transform.position.y) == floor;
            if (npc.activeSelf != here) npc.SetActive(here);
        }
        items.RemoveWhere(n => n == null);
        foreach (var item in items)
        {
            bool here = FloorAt(item.transform.position.y) == floor;
            if (item.activeSelf != here) item.SetActive(here);
        }
    }

    //‚‚³‚©‚çŠK‚ğ‹‚ß‚éB1ŠK‚ª 1
    public int FloorAt(float y)
    {
        int f = Mathf.FloorToInt((y - baseHeight + 1f) / floorHeight) + 1;
        return Mathf.Clamp(f, 1, Mathf.Max(1, FloorCount));
    }
}
