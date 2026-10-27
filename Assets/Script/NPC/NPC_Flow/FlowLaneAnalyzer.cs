using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 分析層。FlowField を一定間隔で読んで、同じ向きでつながったセルを「レーン」にまとめる。
/// 前回の解析結果とセルの重なりで対応付けて、IDを引き継ぐ。結合・分裂・誕生・消滅をイベントで通知する。
/// ルール:FlowField には絶対に書き込まない(読むだけ)。
/// </summary>
public class FlowLaneAnalyzer : MonoBehaviour
{
    // ------------------------------------------------------------
    // レーン1本分の情報
    // ------------------------------------------------------------
    public class Lane
    {
        public int index;            // lanes リスト内の位置(laneId配列に入っている値)。解析ごとに変わる
        public int id;               // 安定ID。同じレーンなら解析をまたいでも同じ
        public int cellCount;        // 大きさ(セル数)
        public float totalStrength;  // 流れの強さの合計
        public Vector2 avgDir;       // 平均の向き(正規化済み)
        public Vector3 center;       // 重心(ワールド座標)
        public float bornTime;       // 生まれた時刻(IDを引き継いだら元の時刻のまま)
        public Color debugColor;

        public float Age => Time.time - bornTime;
    }

    [SerializeField] FlowField field;

    [Header("解析")]
    [Tooltip("何秒ごとに解析するか")]
    [SerializeField, Min(0.05f)] float interval = 0.5f;
    [Tooltip("maxStrength に対してこの割合以上の強さのセルだけをレーン候補にする")]
    [SerializeField, Range(0f, 1f)] float minStrengthRatio = 0.15f;
    [Tooltip("隣のセルと向きの差がこの角度以内ならつなげる")]
    [SerializeField, Range(0f, 90f)] float maxAngle = 30f;
    [Tooltip("オフ:上下左右の4近傍 / オン:斜めも含む8近傍")]
    [SerializeField] bool use8Neighbors = false;
    [Tooltip("これより小さいレーンはノイズとして捨てる")]
    [SerializeField, Min(1)] int minCells = 10;

    [Header("追跡")]
    [Tooltip("前回と今回でこのセル数以上重なっていたら「同じ流れ由来」とみなす")]
    [SerializeField, Min(1)] int minOverlapCells = 3;

    [Header("Debug")]
    [SerializeField] bool updateDebugTexture = true;
    [SerializeField] bool drawLaneGizmos = true;
    [SerializeField] bool logEvents = false;

    // 結果(今回と前回をダブルバッファで持つ)
    int[] laneId;        // セルごとのレーン index。-1 = どのレーンにも属さない
    int[] prevLaneId;
    List<Lane> lanes = new List<Lane>();
    List<Lane> prevLanes = new List<Lane>();
    int nextId = 0;      // 次に配る安定ID

    // 作業用
    int[] queue;
    Texture2D laneTex;
    float timer;
    readonly Dictionary<long, int> overlap = new Dictionary<long, int>();
    readonly List<Pair> pairs = new List<Pair>();

    struct Pair { public int prev, cur, count; }

    const int NONE = -1;
    const int NOISE = -2;   // 解析中だけ使う「小さすぎて捨てた」印

    static readonly Vector2Int[] N4 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };
    static readonly Vector2Int[] N8 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(-1, 1),
        new Vector2Int(1, -1), new Vector2Int(-1, -1),
    };

    // ------------------------------------------------------------
    // 外から使うもの
    // ------------------------------------------------------------
    public IReadOnlyList<Lane> Lanes => lanes;

    /// <summary>解析が終わるたびに呼ばれる(下のイベントより後)</summary>
    public event System.Action OnAnalyzed;
    /// <summary>新しいレーンが生まれた</summary>
    public event System.Action<Lane> OnLaneBorn;
    /// <summary>レーンが消えた(引数は消えたレーンのID)</summary>
    public event System.Action<int> OnLaneVanished;
    /// <summary>複数のレーンが1本になった(結合後のレーン, 合流した元のID一覧)</summary>
    public event System.Action<Lane, IReadOnlyList<int>> OnLaneMerged;
    /// <summary>1本のレーンが複数に分かれた(元のID, 分かれた後のレーン一覧)</summary>
    public event System.Action<int, IReadOnlyList<Lane>> OnLaneSplit;

    /// <summary>ワールド座標がどのレーンにいるか。いなければ null</summary>
    public Lane GetLaneAt(Vector3 worldPos)
    {
        if (laneId == null) return null;
        Vector2 g = field.WorldToGrid(worldPos);
        int x = Mathf.FloorToInt(g.x);
        int y = Mathf.FloorToInt(g.y);
        if (!field.InBounds(x, y)) return null;

        int idx = laneId[x + y * field.Width];
        return idx >= 0 ? lanes[idx] : null;
    }

    /// <summary>安定IDからレーンを探す。今はもう無ければ null</summary>
    public Lane FindLane(int id)
    {
        foreach (var l in lanes) if (l.id == id) return l;
        return null;
    }

    // ------------------------------------------------------------
    void Start()
    {
        if (field == null) field = FindAnyObjectByType<FlowField>();

        int n = field.Width * field.Height;
        laneId = new int[n];
        prevLaneId = new int[n];
        queue = new int[n];
        for (int i = 0; i < n; i++) laneId[i] = NONE;

        laneTex = new Texture2D(field.Width, field.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // レーンの境目をくっきり見せる
            wrapMode = TextureWrapMode.Clamp,
            name = "FlowLaneTex"
        };
        field.SetLaneTexture(laneTex);

        Analyze();
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;
            Analyze();
        }
    }

    void OnDestroy()
    {
        if (laneTex != null) Destroy(laneTex);
    }

    // ------------------------------------------------------------
    // 解析の流れ
    // ------------------------------------------------------------
    public void Analyze()
    {
        // 今回の結果を「前回」に回して、空いた方に今回の結果を書く
        (prevLaneId, laneId) = (laneId, prevLaneId);
        (prevLanes, lanes) = (lanes, prevLanes);
        lanes.Clear();

        Label();        // 1. 塗りつぶしでレーンを作る(この時点では id 未決定)
        Track();        // 2. 前回と対応付けて id を決める + イベント

        if (updateDebugTexture) UploadDebugTexture();
        OnAnalyzed?.Invoke();
    }

    // ------------------------------------------------------------
    // 1. 連結成分ラベリング(BFS の塗りつぶし)
    // ------------------------------------------------------------
    void Label()
    {
        int w = field.Width;
        int n = laneId.Length;
        float cs = field.CellSize;
        Vector3 origin = field.OriginPosition;

        float minStrength = field.MaxStrength * minStrengthRatio;
        float minSq = minStrength * minStrength;
        float cosMax = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
        Vector2Int[] neighbors = use8Neighbors ? N8 : N4;

        for (int i = 0; i < n; i++) laneId[i] = NONE;

        for (int start = 0; start < n; start++)
        {
            if (laneId[start] != NONE) continue;                  // 処理済み
            if (field.GetCell(start).sqrMagnitude < minSq) continue; // 弱いセルは起点にしない

            int idx = lanes.Count;   // 採用されたら lanes のインデックスになる番号
            int head = 0, tail = 0;
            queue[tail++] = start;
            laneId[start] = idx;

            Vector2 sumFlow = Vector2.zero;
            Vector2 sumPos = Vector2.zero;
            float sumStrength = 0f;

            while (head < tail)
            {
                int c = queue[head++];
                int cx = c % w;
                int cy = c / w;
                Vector2 fc = field.GetCell(c);
                Vector2 dc = fc.normalized;

                sumFlow += fc;
                sumPos += new Vector2(cx + 0.5f, cy + 0.5f);
                sumStrength += fc.magnitude;

                foreach (var d in neighbors)
                {
                    int nx = cx + d.x;
                    int ny = cy + d.y;
                    if (!field.InBounds(nx, ny)) continue;

                    int ni = nx + ny * w;
                    if (laneId[ni] != NONE) continue;

                    Vector2 fn = field.GetCell(ni);
                    if (fn.sqrMagnitude < minSq) continue;                  // 弱すぎる
                    if (Vector2.Dot(dc, fn.normalized) < cosMax) continue;  // 向きが違う

                    laneId[ni] = idx;
                    queue[tail++] = ni;
                }
            }

            if (tail < minCells)
            {
                for (int k = 0; k < tail; k++) laneId[queue[k]] = NOISE;
                continue;
            }

            Vector2 avgPos = sumPos / tail;
            lanes.Add(new Lane
            {
                index = idx,
                id = NONE,   // Track() で決める
                cellCount = tail,
                totalStrength = sumStrength,
                avgDir = sumFlow.normalized,
                center = origin + new Vector3(avgPos.x * cs, 0f, avgPos.y * cs),
            });
        }

        for (int i = 0; i < n; i++)
            if (laneId[i] == NOISE) laneId[i] = NONE;
    }

    // ------------------------------------------------------------
    // 2. 追跡:前回のレーンとセルの重なりで対応付ける
    // ------------------------------------------------------------
    void Track()
    {
        // ---- 重なりを数える:(前回index, 今回index) → 重なったセル数 ----
        overlap.Clear();
        for (int i = 0; i < laneId.Length; i++)
        {
            int p = prevLaneId[i];
            int c = laneId[i];
            if (p < 0 || c < 0) continue;

            long key = ((long)p << 32) | (uint)c;
            overlap.TryGetValue(key, out int v);
            overlap[key] = v + 1;
        }

        // 小さい重なりは偶然とみなして捨てる
        pairs.Clear();
        foreach (var kv in overlap)
        {
            if (kv.Value < minOverlapCells) continue;
            pairs.Add(new Pair
            {
                prev = (int)(kv.Key >> 32),
                cur = (int)(kv.Key & 0xffffffffL),
                count = kv.Value
            });
        }

        // ---- ID引き継ぎ:重なりが大きい組から順に、早い者勝ち ----
        pairs.Sort((a, b) => b.count.CompareTo(a.count));
        var prevUsed = new bool[prevLanes.Count];

        foreach (var pr in pairs)
        {
            Lane cur = lanes[pr.cur];
            if (cur.id != NONE || prevUsed[pr.prev]) continue;

            Lane prev = prevLanes[pr.prev];
            cur.id = prev.id;
            cur.bornTime = prev.bornTime;
            prevUsed[pr.prev] = true;
        }

        // 引き継げなかったレーンには新しいID
        foreach (var lane in lanes)
        {
            if (lane.id == NONE)
            {
                lane.id = nextId++;
                lane.bornTime = Time.time;
            }
            // 色はIDで決める → IDが同じなら色も変わらない
            lane.debugColor = Color.HSVToRGB(Mathf.Repeat(lane.id * 0.618034f, 1f), 0.75f, 1f);
        }

        // ---- 何本と何本がつながっているかを集計 ----
        var sourcesOfCur = new List<int>[lanes.Count];   // 今回のレーン ← 前回のどのレーンから来たか
        var targetsOfPrev = new List<Lane>[prevLanes.Count]; // 前回のレーン → 今回のどのレーンへ行ったか
        foreach (var pr in pairs)
        {
            (sourcesOfCur[pr.cur] ??= new List<int>()).Add(prevLanes[pr.prev].id);
            (targetsOfPrev[pr.prev] ??= new List<Lane>()).Add(lanes[pr.cur]);
        }

        // ---- イベント ----
        for (int c = 0; c < lanes.Count; c++)
        {
            var src = sourcesOfCur[c];
            if (src == null)
            {
                // 前回と全く重ならない → 新しく生まれた
                Log($"Born   #{lanes[c].id} ({lanes[c].cellCount} cells)");
                OnLaneBorn?.Invoke(lanes[c]);
            }
            else if (src.Count >= 2)
            {
                // 前回の2本以上が、今回の1本に重なる → 結合
                Log($"Merged #{string.Join(" + #", src)} → #{lanes[c].id}");
                OnLaneMerged?.Invoke(lanes[c], src);
            }
        }

        for (int p = 0; p < prevLanes.Count; p++)
        {
            var dst = targetsOfPrev[p];
            if (dst == null)
            {
                // 今回のどれとも重ならない → 消えた
                Log($"Vanish #{prevLanes[p].id}");
                OnLaneVanished?.Invoke(prevLanes[p].id);
            }
            else if (dst.Count >= 2)
            {
                // 前回の1本が、今回の2本以上に重なる → 分裂
                Log($"Split  #{prevLanes[p].id} → {dst.Count} lanes");
                OnLaneSplit?.Invoke(prevLanes[p].id, dst);
            }
        }
    }

    void Log(string msg)
    {
        if (logEvents) Debug.Log($"[FlowLane] {msg}", this);
    }

    void UploadDebugTexture()
    {
        var px = laneTex.GetPixelData<Color32>(0);
        Color32 empty = new Color32(0, 0, 0, 0);
        for (int i = 0; i < laneId.Length; i++)
        {
            int idx = laneId[i];
            px[i] = idx >= 0 ? (Color32)lanes[idx].debugColor : empty;
        }
        laneTex.Apply(false);
    }

    // ------------------------------------------------------------
    // 各レーンの平均の向き・大きさ・年齢を表示
    // ------------------------------------------------------------
    void OnDrawGizmos()
    {
        if (!drawLaneGizmos || !Application.isPlaying) return;

        foreach (var lane in lanes)
        {
            Vector3 p = lane.center + Vector3.up * 0.3f;
            Vector3 dir = new Vector3(lane.avgDir.x, 0f, lane.avgDir.y);
            float len = 0.5f + Mathf.Sqrt(lane.cellCount) * field.CellSize * 0.5f;

            Gizmos.color = lane.debugColor;
            Gizmos.DrawLine(p, p + dir * len);
            Gizmos.DrawSphere(p, 0.15f);
#if UNITY_EDITOR
            Handles.Label(p + Vector3.up * 0.3f, $"#{lane.id}  {lane.cellCount}cells  {lane.Age:F1}s");
#endif
        }
    }
}
