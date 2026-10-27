using UnityEngine;

/// <summary>
/// XZ平面上のベクトル場。transform.position をグリッドの左下(原点)として扱う。
/// ※回転は考慮していないので、このオブジェクトの回転は (0,0,0) のままにしておくこと。
/// </summary>
public class FlowField : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] int width = 128;
    [SerializeField] int height = 128;
    [SerializeField] float cellSize = 0.5f;

    [Header("Flow")]
    [Tooltip("流れが消えていく速さ(大きいほど早く消える)")]
    [SerializeField, Range(0f, 10f)] float decayPerSec = 1.5f;
    [SerializeField] float maxStrength = 5f;

    [Header("Debug View")]
    [SerializeField] Shader debugShader;          // Debug/FlowField を指定
    [SerializeField] bool showView = true;
    [SerializeField] float viewHeight = 0.02f;    // 地面とのZファイティング避け
    [SerializeField, Range(0f, 1f)] float viewAlpha = 0.7f;

    public enum ViewMode { Color = 0, Arrow = 1, Both = 2, Lane = 3 }
    [SerializeField] ViewMode viewMode = ViewMode.Color;
    [Tooltip("何セルごとに矢印を1本出すか。グリッドが細かいときに上げる")]
    [SerializeField, Min(1)] int arrowStep = 1;
    [SerializeField, Range(0.01f, 0.2f)] float arrowThickness = 0.06f;
    [Tooltip("このキーで表示モードを切り替える")]
    [SerializeField] KeyCode cycleKey = KeyCode.F1;

    public void CycleViewMode() => viewMode = (ViewMode)(((int)viewMode + 1) % 4);

    Vector2[] flow;      // 本体のデータ。index = x + y * width
    Texture2D tex;       // 可視化用(RG = 流れのベクトル)
    Material mat;
    GameObject view;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public float MaxStrength => maxStrength;
    public Texture FlowTexture => tex;
    Vector3 Origin => transform.position;

    // ------------------------------------------------------------
    void Awake()
    {
        flow = new Vector2[width * height];

        // linear = true にしておかないと値がガンマ補正されてしまう
        tex = new Texture2D(width, height, TextureFormat.RGHalf, false, true)
        {
            filterMode = FilterMode.Bilinear,   // 表示時の補間はGPUに任せる
            wrapMode = TextureWrapMode.Clamp,
            name = "FlowFieldTex"
        };

        CreateView();
    }

    void LateUpdate()
    {
        Decay(Time.deltaTime);
        Upload();

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(cycleKey)) CycleViewMode();
#endif

        if (view != null)
        {
            view.SetActive(showView);
            mat.SetFloat("_Alpha", viewAlpha);
            mat.SetFloat("_MaxStrength", maxStrength);
            mat.SetFloat("_Mode", (float)viewMode);
            mat.SetFloat("_ArrowStep", arrowStep);
            mat.SetFloat("_ArrowThickness", arrowThickness);
        }
    }

    void OnDestroy()
    {
        if (tex != null) Destroy(tex);
        if (mat != null) Destroy(mat);
    }

    // ------------------------------------------------------------
    // 座標変換
    // ------------------------------------------------------------

    /// <summary>ワールド座標 → グリッド座標(セル単位の実数。セル(0,0)の左下が0)</summary>
    public Vector2 WorldToGrid(Vector3 worldPos)
    {
        Vector3 p = worldPos - Origin;
        return new Vector2(p.x / cellSize, p.z / cellSize);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    public Vector2 GetCell(int x, int y) => flow[x + y * width];
    public Vector2 GetCell(int index) => flow[index];
    public Vector3 OriginPosition => transform.position;

    // ------------------------------------------------------------
    // 読み出し(NPCが使う)
    // ------------------------------------------------------------

    /// <summary>周りの4セルをバイリニア補間して流れを返す</summary>
    public Vector2 Sample(Vector3 worldPos)
    {
        // セルの中心を基準にしたいので 0.5 ずらす
        Vector2 g = WorldToGrid(worldPos) - new Vector2(0.5f, 0.5f);

        int x0 = Mathf.FloorToInt(g.x);
        int y0 = Mathf.FloorToInt(g.y);
        float tx = g.x - x0;
        float ty = g.y - y0;

        int xa = Mathf.Clamp(x0, 0, width - 1);
        int xb = Mathf.Clamp(x0 + 1, 0, width - 1);
        int ya = Mathf.Clamp(y0, 0, height - 1);
        int yb = Mathf.Clamp(y0 + 1, 0, height - 1);

        Vector2 bottom = Vector2.Lerp(flow[xa + ya * width], flow[xb + ya * width], tx);
        Vector2 top = Vector2.Lerp(flow[xa + yb * width], flow[xb + yb * width], tx);
        return Vector2.Lerp(bottom, top, ty);
    }

    // ------------------------------------------------------------
    // 書き込み(プレイヤーやイベントが使う)
    // ------------------------------------------------------------

    /// <summary>
    /// worldPos を中心に半径 radius の範囲へ amount を足す。中心ほど強く、端で0。
    /// 触るのは半径内のセルだけなので、範囲が小さければ軽い。
    /// </summary>
    public void AddFlow(Vector3 worldPos, Vector2 amount, float radius)
    {
        Vector2 c = WorldToGrid(worldPos);
        int r = Mathf.CeilToInt(radius / cellSize);
        int cx = Mathf.FloorToInt(c.x);
        int cy = Mathf.FloorToInt(c.y);

        for (int y = cy - r; y <= cy + r; y++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (!InBounds(x, y)) continue;

                Vector2 cellCenter = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(cellCenter, c) * cellSize;
                if (d > radius) continue;

                float w = 1f - d / radius;
                int i = x + y * width;
                flow[i] = Vector2.ClampMagnitude(flow[i] + amount * w, maxStrength);
            }
        }
    }

    /// <summary>
    /// apex(頂点)から forward 方向に、扇形(コーン)の範囲へ流れを足す。
    ///  - 頂点に近いほど強く、遠いほど弱い
    ///  - 中心線ほど強く、扇の端ほど弱い
    ///  - spread が大きいほど、流れの向きが「まっすぐ前」から「外側へ広がる向き」になる
    /// </summary>
    /// <param name="length">コーンの長さ(m)</param>
    /// <param name="halfAngleDeg">中心線から片側の角度</param>
    /// <param name="spread">0 = 全部前向き / 1 = 頂点から放射状</param>
    /// <param name="amount">中心(頂点付近・中心線上)に足す流れの量</param>
    public void AddFlowCone(Vector3 apex, Vector2 forward, float length, float halfAngleDeg,
                            float spread, float amount)
    {
        if (length <= 0f || amount <= 0f || forward.sqrMagnitude < 1e-8f) return;
        forward.Normalize();

        Vector2 a = WorldToGrid(apex);           // 頂点(グリッド座標)
        float lenCells = length / cellSize;      // 長さ(セル単位)
        float cosHalf = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);

        // 頂点を中心に半径 lenCells の四角形だけ調べる
        int minX = Mathf.Max(0, Mathf.FloorToInt(a.x - lenCells));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(a.x + lenCells));
        int minY = Mathf.Max(0, Mathf.FloorToInt(a.y - lenCells));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(a.y + lenCells));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - a;
                float dist = d.magnitude;
                if (dist > lenCells || dist < 1e-4f) continue;

                Vector2 dn = d / dist;
                float c = Vector2.Dot(dn, forward);
                if (c < cosHalf) continue;       // 扇の外

                float distT = dist / lenCells;                    // 0:頂点 ? 1:先端
                float angT = Mathf.InverseLerp(1f, cosHalf, c);   // 0:中心線 ? 1:扇の端
                float w = (1f - distT) * (1f - angT * angT);

                // 前向きと放射状(外向き)を混ぜる → 相手が左右どちらに避ければいいか分かる
                Vector2 dir = Vector2.Lerp(forward, dn, spread).normalized;

                int i = x + y * width;
                flow[i] = Vector2.ClampMagnitude(flow[i] + dir * (amount * w), maxStrength);
            }
        }
    }

    /// <summary>
    /// start から forward の「逆方向(後ろ)」に伸びる帯の範囲へ、forward 向きの流れを足す。
    ///  - start に近いほど強く、帯の後端ほど弱い
    ///  - 中心線ほど強く、帯の端ほど弱い
    /// 動いている間は毎フレーム書き直されるので、後ろ length 分の道は減衰に関係なく保たれる。
    /// </summary>
    /// <param name="length">帯の長さ(m)</param>
    /// <param name="halfWidth">帯の片側の幅(m)</param>
    /// <param name="amount">中心(start付近・中心線上)に足す流れの量</param>
    public void AddFlowBand(Vector3 start, Vector2 forward, float length, float halfWidth, float amount)
    {
        if (length <= 0f || halfWidth <= 0f || amount <= 0f || forward.sqrMagnitude < 1e-8f) return;
        forward.Normalize();

        Vector2 s = WorldToGrid(start);
        float lenCells = length / cellSize;
        float hwCells = halfWidth / cellSize;
        Vector2 back = -forward;
        Vector2 side = new Vector2(-forward.y, forward.x);   // 横方向

        // 帯の4隅から、調べる範囲(四角形)を決める
        Vector2 c0 = s + side * hwCells;
        Vector2 c1 = s - side * hwCells;
        Vector2 c2 = c0 + back * lenCells;
        Vector2 c3 = c1 + back * lenCells;
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.x, c1.x), Mathf.Min(c2.x, c3.x))));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.x, c1.x), Mathf.Max(c2.x, c3.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(Mathf.Min(c0.y, c1.y), Mathf.Min(c2.y, c3.y))));
        int maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(Mathf.Max(c0.y, c1.y), Mathf.Max(c2.y, c3.y))));

        Vector2 add = forward * amount;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - s;

                float along = Vector2.Dot(d, back);           // 後ろにどれだけ離れているか
                if (along < 0f || along > lenCells) continue;

                float across = Mathf.Abs(Vector2.Dot(d, side)); // 中心線からの距離
                if (across > hwCells) continue;

                float alongT = along / lenCells;     // 0:先頭 ? 1:後端
                float acrossT = across / hwCells;    // 0:中心 ? 1:端
                float w = (1f - alongT) * (1f - acrossT * acrossT);

                int i = x + y * width;
                flow[i] = Vector2.ClampMagnitude(flow[i] + add * w, maxStrength);
            }
        }
    }

    public void Clear()
    {
        System.Array.Clear(flow, 0, flow.Length);
    }

    /// <summary>レーン表示用のテクスチャを表示マテリアルに渡す(FlowLaneAnalyzerから呼ばれる)</summary>
    public void SetLaneTexture(Texture laneTex)
    {
        if (mat != null) mat.SetTexture("_LaneTex", laneTex);
    }

    // ------------------------------------------------------------
    // 内部処理
    // ------------------------------------------------------------

    /// <summary>全セルを指数的に減衰させる(フレームレートに依存しない)</summary>
    void Decay(float dt)
    {
        float k = Mathf.Exp(-decayPerSec * dt);
        for (int i = 0; i < flow.Length; i++)
            flow[i] *= k;
    }

    /// <summary>配列 → テクスチャ。GetPixelData でテクスチャのメモリに直接書く</summary>
    void Upload()
    {
        // RGHalf は 1ピクセル = ushort(half) × 2
        var px = tex.GetPixelData<ushort>(0);
        for (int i = 0; i < flow.Length; i++)
        {
            px[i * 2] = Mathf.FloatToHalf(flow[i].x);
            px[i * 2 + 1] = Mathf.FloatToHalf(flow[i].y);
        }
        tex.Apply(false);
    }

    void CreateView()
    {
        if (debugShader == null)
        {
            Debug.LogWarning("[FlowField] debugShader が未設定なので表示はスキップ", this);
            return;
        }

        view = GameObject.CreatePrimitive(PrimitiveType.Quad);
        view.name = "FlowFieldView";
        Destroy(view.GetComponent<Collider>());

        float sx = width * cellSize;
        float sz = height * cellSize;
        view.transform.SetParent(transform, false);
        view.transform.localPosition = new Vector3(sx * 0.5f, viewHeight, sz * 0.5f);
        view.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 寝かせて上向きに
        view.transform.localScale = new Vector3(sx, sz, 1f);

        mat = new Material(debugShader);
        mat.SetTexture("_FlowTex", tex);
        mat.SetVector("_GridSize", new Vector4(width, height, 0, 0));
        view.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // エディタ上で範囲を確認できるように
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Vector3 size = new Vector3(width * cellSize, 0f, height * cellSize);
        Gizmos.DrawWireCube(transform.position + size * 0.5f, size);
    }
}
