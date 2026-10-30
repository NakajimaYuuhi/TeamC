using System;
using System.Collections.Generic;
using UnityEngine;

//購買のステージの人を出して動かす。校舎のプレハブに付ける
//階ごとに、廊下を歩く人、走る人、立ち話の人と、2つの人の流れを作る
//ダッシュの流れはプレイヤーと同じ向きに階段へ走り、下の階へ下りて、1階の購買まで走り続ける。逆向きの流れは階段から来てプレイヤーとすれ違う
//流れの人は道の最後に着くと、出てくる所へ戻ってまた進む。1階のダッシュの人は購買の前に人だかりを作る
public class SchoolCrowd : MonoBehaviour
{
    [Serializable]
    public class Path
    {
        [Tooltip("たどる点")]
        public Vector3[] points;
    }

    [Serializable]
    public class Stream
    {
        [Tooltip("人数")]
        public int count = 8;
        [Tooltip("出てくる所")]
        public Vector3[] spawns;
        [Tooltip("たどる道。出てきた所からいちばん近い道を使う")]
        public Path[] paths;
        [Tooltip("速さ")]
        public Vector2 speed = new Vector2(3f, 3.8f);
        [Tooltip("道の最後で、購買の前に並ぶ")]
        public bool toShop;
        [Tooltip("0 より大きいと、この秒数ごとにまとめて出てくる。波のあいだにすき間ができる")]
        public float waveInterval;
        [Tooltip("波の中で出てくる時間のばらつき。秒")]
        public float waveSpread = 0.6f;
        [Tooltip("オンなら、道の最後で1つ下の階へ下りて、その階の同じ流れを続ける。上の階には教室から新しい人が出てくる")]
        public bool toNextFloor;
        [Tooltip("下りてきた人を合わせて、この階のこの流れにいてよい人数。人数の何倍か")]
        public float capRate = 2f;
    }

    [Serializable]
    public class Floor
    {
        [Tooltip("階の名前。確認用")]
        public string name;
        [Tooltip("廊下の両はし。歩く人と走る人がこのあいだを行き来する。2つで1本")]
        public Vector3[] corridors;
        [Tooltip("廊下の幅の半分。人がばらける幅")]
        public float laneHalfWidth = 1.1f;
        [Tooltip("立ち話の場所")]
        public Vector3[] standSpots;
        [Tooltip("歩く人")]
        public int walkers = 8;
        [Tooltip("走る人")]
        public int runners = 3;
        [Tooltip("立ち話の組の人数")]
        public int standGroup = 3;
        [Tooltip("プレイヤーと同じ向きに、階段や購買へダッシュする流れ")]
        public Stream rush;
        [Tooltip("プレイヤーと逆向きに進む流れ")]
        public Stream counter;
    }

    [Header("参照")]
    [Tooltip("出す人のプレハブ")]
    [SerializeField] GameObject npcPrefab;

    [Header("階")]
    [SerializeField] Floor[] floors;

    [Header("購買の前")]
    [Tooltip("人だかりの中心")]
    [SerializeField] Vector3 shopCenter;
    [Tooltip("人だかりの大きさ")]
    [SerializeField] Vector3 shopSize = new Vector3(6, 0, 2.2f);
    [Tooltip("人だかりの最大人数。これを超えると買えた人から帰る")]
    [SerializeField] int shopMax = 18;
    [Tooltip("買えた人が帰っていく所")]
    [SerializeField] Vector3 shopExit;

    [Header("速さ")]
    [SerializeField] Vector2 walkSpeed = new Vector2(1.0f, 1.8f);
    [SerializeField] Vector2 runSpeed = new Vector2(3.4f, 4.0f);
    [Tooltip("立ち話の人が元の場所へ戻る強さ")]
    [SerializeField, Range(0f, 1f)] float standPull = 0.7f;
    [Tooltip("流れの人が次に進み出すまでの秒数")]
    [SerializeField] Vector2 respawnDelay = new Vector2(0.5f, 3f);

    NpcPhysicsLodManager lod;
    readonly Queue<CrowdAgent> shopQueue = new Queue<CrowdAgent>();
    readonly List<(CrowdAgent agent, Stream stream, float at)> waiting = new List<(CrowdAgent, Stream, float)>();
    readonly Dictionary<CrowdAgent, Stream> streamOf = new Dictionary<CrowdAgent, Stream>();
    readonly HashSet<CrowdAgent> leaving = new HashSet<CrowdAgent>();
    readonly Dictionary<Stream, int> floorOf = new Dictionary<Stream, int>();

    void Start()
    {
        lod = FindFirstObjectByType<NpcPhysicsLodManager>();
        for (int i = 0; i < floors.Length; i++)
        {
            if (floors[i].rush != null) floorOf[floors[i].rush] = i;
            SpawnFloor(floors[i]);
        }
    }

    static float Rand(Vector2 r) => UnityEngine.Random.Range(r.x, r.y);

    void SpawnFloor(Floor f)
    {
        int lines = f.corridors != null ? f.corridors.Length / 2 : 0;
        //歩く人と走る人。どちらも遠くでは止めてかまわない
        for (int i = 0; i < f.walkers + f.runners && lines > 0; i++)
        {
            bool run = i >= f.walkers;
            int l = UnityEngine.Random.Range(0, lines);
            Vector3 a = f.corridors[l * 2], b = f.corridors[l * 2 + 1];
            Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * UnityEngine.Random.Range(-f.laneHalfWidth, f.laneHalfWidth);
            Vector3 p = Vector3.Lerp(a, b, UnityEngine.Random.value) + side;
            var agent = Spawn(p, b - a, true);
            float s = run ? Rand(runSpeed) : Rand(walkSpeed);
            if (UnityEngine.Random.value < 0.5f) agent.Patrol(a + side, b + side, s);
            else agent.Patrol(b + side, a + side, s);
        }
        //立ち話。輪になって内側を向く
        if (f.standSpots != null)
            foreach (var spot in f.standSpots)
                for (int i = 0; i < f.standGroup; i++)
                {
                    float ang = (i + UnityEngine.Random.value * 0.4f) * Mathf.PI * 2f / f.standGroup;
                    Vector3 p = spot + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.55f;
                    Spawn(p, spot - p, true).Hold(p, spot - p, standPull);
                }
        SpawnStream(f.rush);
        SpawnStream(f.counter);
    }

    //流れの人。流れが止まらないように、遠くでも止めない
    void SpawnStream(Stream s)
    {
        if (s == null || s.spawns == null || s.spawns.Length == 0) return;
        for (int i = 0; i < s.count; i++)
        {
            var agent = NewStreamAgent(s, s.spawns[UnityEngine.Random.Range(0, s.spawns.Length)]);
            StartRoute(agent, s, true);
        }
    }

    CrowdAgent NewStreamAgent(Stream s, Vector3 pos)
    {
        var agent = Spawn(pos, Vector3.forward, false);
        streamOf[agent] = s;
        agent.RouteEnded += OnRouteEnded;
        return agent;
    }

    CrowdAgent Spawn(Vector3 pos, Vector3 face, bool register)
    {
        face.y = 0f;
        if (face.sqrMagnitude < 1e-4f) face = Vector3.forward;
        var npc = Instantiate(npcPrefab, pos + Vector3.up * 0.2f, Quaternion.LookRotation(face));
        var a = CrowdAgent.Attach(npc);
        a.Hold(pos, face, standPull);
        if (register && lod != null && a.Controller != null) lod.Register(a.Controller);
        return a;
    }

    //出てきた所から、いちばん近い道を進む。初めは道の途中から始めて、流れを最初からできた形にする
    void StartRoute(CrowdAgent a, Stream s, bool midway)
    {
        if (s.paths == null || s.paths.Length == 0) return;
        Vector3 from = a.transform.position;
        Path best = null;
        float bestD = float.MaxValue;
        foreach (var p in s.paths)
        {
            if (p.points == null || p.points.Length == 0) continue;
            float d = (p.points[0] - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        if (best == null) return;
        int start = midway ? UnityEngine.Random.Range(0, best.points.Length) : 0;
        if (midway && start > 0) a.Warp(Vector3.Lerp(best.points[start - 1], best.points[start], UnityEngine.Random.value) + Vector3.up * 0.2f);
        var rest = new Vector3[best.points.Length - start];
        Array.Copy(best.points, start, rest, 0, rest.Length);
        for (int i = 0; i < rest.Length; i++) rest[i] += new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f, UnityEngine.Random.Range(-0.6f, 0.6f));
        a.Route(rest, Rand(s.speed));
    }

    void OnRouteEnded(CrowdAgent a)
    {
        if (!streamOf.TryGetValue(a, out var s)) return;
        if (s.toShop && !leaving.Remove(a))
        {
            //購買の前に並ぶ。多すぎたら、前の人から帰る
            Vector3 spot = shopCenter + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f) * shopSize.x, 0f, UnityEngine.Random.Range(-0.5f, 0.5f) * shopSize.z);
            a.Hold(spot, shopCenter - spot, standPull);
            shopQueue.Enqueue(a);
            while (shopQueue.Count > shopMax)
            {
                var done = shopQueue.Dequeue();
                if (done == null) continue;
                leaving.Add(done);
                done.Route(new[] { shopExit }, Rand(walkSpeed) + 0.5f);
            }
            return;
        }
        //階段に着いたら下の階へ下りて、下の階の流れを続ける。この階には教室から新しい人を出す
        if (s.toNextFloor && floorOf.TryGetValue(s, out int fi) && fi + 1 < floors.Length && floors[fi + 1].rush != null)
        {
            var next = floors[fi + 1].rush;
            if (CountIn(next) < next.count * next.capRate)
            {
                Vector3 arrive = Nearest(next.spawns, a.transform.position) + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0f, UnityEngine.Random.Range(-0.7f, 0.7f));
                a.Warp(arrive + Vector3.up * 0.2f);
                streamOf[a] = next;
                StartRoute(a, next, false);
                var fresh = NewStreamAgent(s, s.spawns[UnityEngine.Random.Range(0, s.spawns.Length)]);
                waiting.Add((fresh, s, NextStart(s)));
                return;
            }
        }
        //道の最後や出口に着いたら、出てくる所へ移して少し待たせ、また進ませる
        Vector3 p = s.spawns[UnityEngine.Random.Range(0, s.spawns.Length)];
        a.Warp(p + Vector3.up * 0.2f);
        a.Hold(p, Vector3.forward, standPull);
        waiting.Add((a, s, NextStart(s)));
    }

    int CountIn(Stream s)
    {
        int n = 0;
        foreach (var kv in streamOf) if (kv.Value == s && kv.Key != null) n++;
        return n;
    }

    static Vector3 Nearest(Vector3[] list, Vector3 from)
    {
        Vector3 best = list[0];
        float bestD = float.MaxValue;
        foreach (var p in list)
        {
            float d = new Vector2(p.x - from.x, p.z - from.z).sqrMagnitude;
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    //次に進み出す時刻。波のある流れは、次の波の時刻にそろえる
    float NextStart(Stream s)
    {
        if (s.waveInterval <= 0f) return Time.time + Rand(respawnDelay);
        float wave = (Mathf.Floor(Time.time / s.waveInterval) + 1f) * s.waveInterval;
        return wave + UnityEngine.Random.Range(0f, s.waveSpread);
    }

    void Update()
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            var (a, s, at) = waiting[i];
            if (a == null) { waiting.RemoveAt(i); continue; }
            if (Time.time < at || !a.gameObject.activeInHierarchy) continue;
            waiting.RemoveAt(i);
            StartRoute(a, s, false);
        }
    }
}
