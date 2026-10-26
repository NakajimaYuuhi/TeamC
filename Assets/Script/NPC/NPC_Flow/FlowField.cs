using UnityEngine;

/// <summary>
/// XZ平面上のベクトル場。transform.position をグリッドの左下(原点)として扱う。
/// ※回転は考慮していないので、このオブジェクトの回転は (0,0,0) のままにしておくこと。
/// </summary>
/// 

// 使い方:
// 1. FlowField をシーンに置く。
// 2. FlowEmitter を NPC やプレイヤーに付ける。
// 3. FlowEmitter の field に FlowField を指定する。
// 4. FlowField の decayPerSec を調整して流れの消える速さを変える。

// ===== 関数一覧 =================================================================
// WorldToGrid : ワールド座標 → グリッド座標(セル単位の実数)
// InBounds : グリッド座標が範囲内かどうか
// GetCell : グリッド座標のセルの流れを返す
// Sample : 周りの4セルをバイリニア補間して流れ
// AddFlow : 指定した範囲に流れを追加
// Clear : 全セルをクリア
//
// Decay : 全セルを指数的に減衰させる
// Upload : 配列 → テクスチャ。GetPixelData でテクスチャのメモリに直接書く
//
// CreateView : デバッグ表示用の Quad を作る
// OnDrawGizmos : エディタ上で範囲を確認できるように
// ================================================================================

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

    public enum ViewMode { Color = 0, Arrow = 1, Both = 2 }
    [SerializeField] ViewMode viewMode = ViewMode.Color;
    [Tooltip("何セルごとに矢印を1本出すか。グリッドが細かいときに上げる")]
    [SerializeField, Min(1)] int arrowStep = 1;
    [SerializeField, Range(0.01f, 0.2f)] float arrowThickness = 0.06f;
    [Tooltip("このキーで表示モードを切り替える")]
    [SerializeField] KeyCode cycleKey = KeyCode.F1;

    public void CycleViewMode() => viewMode = (ViewMode)(((int)viewMode + 1) % 3);

    Vector2[] flow;      // 本体のデータ。index = x + y * width
    Texture2D tex;       // 可視化用(RG = 流れのベクトル)
    Material mat;
    GameObject view;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;
    public Texture FlowTexture => tex;
    Vector3 Origin => transform.position;

    // ------------------------------------------------------------
    void Awake()
    {
        // 配列の初期化
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

    // 移動が終わった後に実行される
    void LateUpdate()
    {
        // 流れの減衰
        Decay(Time.deltaTime);

        // テクスチャにアップロード
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

    public void Clear()
    {
        System.Array.Clear(flow, 0, flow.Length);
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
        // なので GetPixelData<ushort>(0) で ushort 配列を取得して書き込む
        var px = tex.GetPixelData<ushort>(0);
        for (int i = 0; i < flow.Length; i++)
        {
            px[i * 2] = Mathf.FloatToHalf(flow[i].x);
            px[i * 2 + 1] = Mathf.FloatToHalf(flow[i].y);
        }
        tex.Apply(false);
    }

    // 
    void CreateView()
    {
        if (debugShader == null)
        {
            Debug.LogWarning("[FlowField] debugShader が未設定なので表示はスキップ", this);
            return;
        }
        
        // デバッグ表示用の Quad を作る
        view = GameObject.CreatePrimitive(PrimitiveType.Quad);
        view.name = "FlowFieldView";
        Destroy(view.GetComponent<Collider>()); // コライダーは不要

        // Quad のサイズをグリッドに合わせる
        float sx = width * cellSize;
        float sz = height * cellSize;

        // FlowField の子にして、XZ平面上に置く
        view.transform.SetParent(transform, false);
        view.transform.localPosition = new Vector3(sx * 0.5f, viewHeight, sz * 0.5f);
        view.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 寝かせて上向きに
        view.transform.localScale = new Vector3(sx, sz, 1f);

        mat = new Material(debugShader);
        mat.SetTexture("_FlowTex", tex);
        mat.SetVector("_GridSize", new Vector4(width, height, 0, 0));
        view.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }


    // エディタ上でグリッドの範囲を表示
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Vector3 size = new Vector3(width * cellSize, 0f, height * cellSize);
        Gizmos.DrawWireCube(transform.position + size * 0.5f, size);
    }
}
