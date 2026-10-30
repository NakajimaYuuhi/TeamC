using System.Collections.Generic;
using UnityEngine;

//満員電車の人を出して動かす。電車のステージのプレハブに付ける
//走っているあいだは車内に立つ人と、ホームでドアを待つ人。ときどき揺れて、着く直前にブレーキで前へ流される
//ドアが開くと、降りる人はホームへ出て、待っていた人は車内へなだれ込む
public class TrainCrowd : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("出す人のプレハブ")]
    [SerializeField] GameObject npcPrefab;
    [Tooltip("時間でドアを開ける部品")]
    [SerializeField] TrainDoors doors;
    [Tooltip("駅に着くように見せる部品。ホームで待つ人はホームに乗って一緒に来る")]
    [SerializeField] StationArrival station;

    [Header("場所。Unity の座標")]
    [Tooltip("車内の通路。中心")]
    [SerializeField] Vector3[] aisleCenters;
    [Tooltip("車内の通路。大きさ")]
    [SerializeField] Vector3[] aisleSizes;
    [Tooltip("ホーム側のドアの中心。敷居の上")]
    [SerializeField] Vector3[] doorPoints;
    [Tooltip("降りる人がいるドアの番号。0 から")]
    [SerializeField] int[] alightDoors = { 6, 7 };
    [Tooltip("ホームの奥の、降りた人が行く範囲。中心")]
    [SerializeField] Vector3 platformCenter;
    [Tooltip("ホームの奥の、降りた人が行く範囲。大きさ")]
    [SerializeField] Vector3 platformSize = new Vector3(40, 0, 3);
    [Tooltip("車内からホームへ向かう向き")]
    [SerializeField] Vector3 outward = Vector3.forward;

    [Header("人数。難易度")]
    [Tooltip("車両ごとに、通路に立つ人。2～3人の固まりで置き、あいだに通り道を残す")]
    [SerializeField] int[] standersByCar = { 12, 16 };
    [Tooltip("ドアごとに、車内のドアの前に固まる人")]
    [SerializeField] int[] clusterByDoor = { 0, 3, 3, 2, 2, 2, 4, 4 };
    [Tooltip("ドアごとに、ホームで待っていて乗り込んでくる人。降りる人のいるドアにはいない")]
    [SerializeField] int[] boardingByDoor = { 2, 5, 6, 3, 2, 2, 0, 0 };
    [Tooltip("ホームで、乗らずに立っている人")]
    [SerializeField] int platformWaiters = 6;
    [Tooltip("乗らずに立っている人の場所。中心")]
    [SerializeField] Vector3 waitCenter;
    [Tooltip("乗らずに立っている人の場所。大きさ")]
    [SerializeField] Vector3 waitSize = new Vector3(10, 0, 2);
    [Tooltip("立っている人が元の場所へ戻る強さ。大きいほど押しても戻ってくる")]
    [SerializeField, Range(0f, 1f)] float holdPull = 0.5f;
    [Tooltip("降りる人とホームへ乗る人の速さ")]
    [SerializeField] float rushSpeed = 2.6f;

    [Header("揺れ")]
    [Tooltip("揺れの間隔。秒")]
    [SerializeField] Vector2 swayInterval = new Vector2(6f, 10f);
    [Tooltip("ふだんの揺れの強さ")]
    [SerializeField] float swayForce = 6f;
    [Tooltip("ドアが開く何秒前にブレーキがかかるか。駅の部品があればそちらに合わせる")]
    [SerializeField] float brakeLead = 2.5f;
    [Tooltip("ブレーキの強さ")]
    [SerializeField] float brakeForce = 12f;
    [Tooltip("電車の進む向き")]
    [SerializeField] Vector3 travel = Vector3.right;

    readonly List<CrowdAgent> all = new List<CrowdAgent>();
    readonly List<CrowdAgent> alighting = new List<CrowdAgent>();
    readonly List<(CrowdAgent agent, int door)> boarding = new List<(CrowdAgent, int)>();
    readonly List<(CrowdAgent agent, Vector3 at)> riding = new List<(CrowdAgent, Vector3)>();
    float startTime, nextSway;
    bool braked, opened;
    Rigidbody player;
    NpcPhysicsLodManager lod;

    void Start()
    {
        startTime = Time.time;
        nextSway = Random.Range(swayInterval.x, swayInterval.y);
        lod = FindFirstObjectByType<NpcPhysicsLodManager>();
        var alight = new HashSet<int>(alightDoors);

        //通路に立つ人。2～3人の固まりを、すき間をあけて並べる
        Vector3 alongCar = Vector3.Cross(Vector3.up, outward).normalized;
        for (int c = 0; c < aisleCenters.Length; c++)
        {
            int left = Count(standersByCar, c);
            while (left > 0)
            {
                int n = Mathf.Min(left, Random.Range(2, 4));
                Vector3 g = RandomIn(aisleCenters[c], aisleSizes[c]);
                for (int i = 0; i < n; i++) Spawn(g + alongCar * Random.Range(-0.6f, 0.6f) + outward * Random.Range(-0.5f, 0.5f), RandomSide());
                left -= n;
            }
        }

        //ドアの前に固まる人。降りる人のいるドアでは、降りる人になる
        Vector3 along = Vector3.Cross(Vector3.up, outward).normalized;
        for (int d = 0; d < doorPoints.Length; d++)
            for (int i = 0; i < Count(clusterByDoor, d); i++)
            {
                Vector3 p = doorPoints[d] - outward * Random.Range(0.4f, 1.1f) + along * Random.Range(-0.9f, 0.9f);
                var a = Spawn(p, outward);
                if (alight.Contains(d)) alighting.Add(a);
            }

        //ホームで、乗らずに立っている人。ゴールのドアの前をふさぐ
        for (int i = 0; i < platformWaiters; i++)
        {
            Vector3 p = RandomIn(waitCenter, waitSize);
            var w = Spawn(p, -outward, station == null);
            if (station != null) { w.SetRiding(true); w.Warp(p + station.Offset); riding.Add((w, p)); }
        }

        //ホームでドアを待つ人
        for (int d = 0; d < doorPoints.Length; d++)
        {
            if (alight.Contains(d)) continue;
            for (int i = 0; i < Count(boardingByDoor, d); i++)
            {
                Vector3 p = doorPoints[d] + outward * Random.Range(0.8f, 2.6f) + along * Random.Range(-1.3f, 1.3f);
                var b = Spawn(p, -outward, station == null);
                boarding.Add((b, d));
                if (station != null) { b.SetRiding(true); b.Warp(p + station.Offset); riding.Add((b, p)); }
            }
        }
    }

    static int Count(int[] list, int i) => list != null && i < list.Length ? Mathf.Max(0, list[i]) : 0;

    CrowdAgent Spawn(Vector3 pos, Vector3 face, bool register = true)
    {
        var npc = Instantiate(npcPrefab, pos + Vector3.up * 0.2f, Quaternion.LookRotation(face));
        var a = CrowdAgent.Attach(npc);
        a.Hold(pos, face, holdPull);
        if (register && lod != null && a.Controller != null) lod.Register(a.Controller);
        all.Add(a);
        return a;
    }

    void FixedUpdate()
    {
        Ride();
        if (opened) return;
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<Rigidbody>();
        }
        float t = Time.time - startTime;
        float openAt = doors != null ? doors.OpenAfter : float.MaxValue;

        //ブレーキ。進む向きへ、みんなまとめて流される
        float brakeAt = station != null ? station.BrakeStart : openAt - brakeLead;
        if (!braked && t >= brakeAt)
        {
            braked = true;
            Push(travel.normalized * brakeForce);
        }
        //ふだんの揺れ。前か後ろへ
        else if (!braked && t >= nextSway)
        {
            nextSway = t + Random.Range(swayInterval.x, swayInterval.y);
            Push(travel.normalized * (Random.value < 0.5f ? -swayForce : swayForce));
        }

        if (doors != null && doors.IsOpen) OnOpen();
    }

    //ホームで待つ人を、ホームと一緒に動かす。駅に着いたら降ろす
    void Ride()
    {
        if (station == null || riding.Count == 0) return;
        foreach (var (a, at) in riding)
        {
            if (a == null || a.Body == null) continue;
            if (!station.Arrived) { a.Body.MovePosition(at + station.Offset + Vector3.up * 0.05f); continue; }
            a.Warp(at + Vector3.up * 0.05f);
            a.SetRiding(false);
            if (lod != null && a.Controller != null) lod.Register(a.Controller);
        }
        if (station.Arrived) riding.Clear();
    }

    void Push(Vector3 impulse)
    {
        foreach (var a in all)
            if (a != null && a.Body != null && !a.Body.isKinematic) a.Body.AddForce(impulse, ForceMode.VelocityChange);
        if (player != null && !player.isKinematic) player.AddForce(impulse * 0.6f, ForceMode.VelocityChange);
    }

    //降りる人はホームの奥へ、待っていた人は車内へ
    void OnOpen()
    {
        opened = true;
        foreach (var a in alighting)
        {
            if (a == null) continue;
            Vector3 end = RandomIn(platformCenter, platformSize);
            a.RouteEnded += x => x.Hold(x.transform.position, -outward, 0.2f);
            a.Route(new[] { a.transform.position + outward * 2.5f, end }, rushSpeed);
        }
        foreach (var (a, d) in boarding)
        {
            if (a == null) continue;
            Vector3 inside = doorPoints[d] - outward * Random.Range(0.8f, 1.6f) + Vector3.Cross(Vector3.up, outward).normalized * Random.Range(-1.5f, 1.5f);
            a.RouteEnded += x => x.Hold(x.transform.position, RandomSide(), holdPull);
            a.Route(new[] { doorPoints[d], inside }, rushSpeed);
        }
    }

    Vector3 RandomSide() => Random.value < 0.5f ? outward : -outward;

    static Vector3 RandomIn(Vector3 c, Vector3 s) =>
        c + new Vector3(Random.Range(-0.5f, 0.5f) * s.x, 0f, Random.Range(-0.5f, 0.5f) * s.z);
}
