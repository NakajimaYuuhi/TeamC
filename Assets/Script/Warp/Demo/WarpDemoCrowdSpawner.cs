/////////////////////////////////////////
/// WarpDemoCrowdSpawner.cs
/// ワープデモ用：通路の両端からNPCを乱雑に出し入れする
///
/// ・通路（Z方向に長い箱）の両端からランダムな位置で出現
/// ・途中のランダムな点を経由して反対側へ → スクランブル交差点のように入り乱れる
/// ・オブジェクトプールで使い回す
/////////////////////////////////////////

using System.Collections.Generic;
using UnityEngine;

public class WarpDemoCrowdSpawner : MonoBehaviour
{
    [Header("通路（中心とサイズ x=幅, y=長さ(Z方向)）")]
    [SerializeField] private Vector3 areaCenter = Vector3.zero;
    [SerializeField] private Vector2 areaSize = new Vector2(6f, 30f);

    [Header("出現")]
    [SerializeField, Min(1)] private int maxAgents = 40;
    [SerializeField, Tooltip("出現間隔（秒）の最小・最大")]
    private Vector2 spawnIntervalRange = new Vector2(0.1f, 0.45f);
    [SerializeField, Tooltip("開始時に通路を埋めておく割合")]
    [Range(0f, 1f)] private float initialFillRate = 0.8f;
    [SerializeField, Tooltip("この時間を過ぎたら強制的に消す（詰まり対策）")]
    private float maxLifetime = 60f;

    [Header("NPC")]
    [SerializeField] private Vector2 speedRange = new Vector2(1.2f, 2.4f);
    [SerializeField] private float agentRadius = 0.3f;
    [SerializeField] private float agentHeight = 1.7f;
    [SerializeField] private float agentMass = 10f;
    [SerializeField] private Color[] palette =
    {
        new Color(0.85f, 0.42f, 0.38f),
        new Color(0.36f, 0.55f, 0.82f),
        new Color(0.42f, 0.70f, 0.50f),
        new Color(0.88f, 0.72f, 0.36f),
        new Color(0.62f, 0.48f, 0.78f),
        new Color(0.50f, 0.50f, 0.55f),
    };

    public float MaxLifetime => maxLifetime;
    public int ActiveCount => active.Count;

    private readonly Queue<WarpDemoCrowdAgent> pool = new Queue<WarpDemoCrowdAgent>();
    private readonly HashSet<WarpDemoCrowdAgent> active = new HashSet<WarpDemoCrowdAgent>();
    private Material[] materials;
    private int npcLayer;
    private int npcMask;
    private float spawnTimer;

    private void Awake()
    {
        npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer < 0)
        {
            Debug.LogError("WarpDemoCrowdSpawner: NPCレイヤーがない", this);
            npcLayer = 0;
        }
        npcMask = 1 << npcLayer;
        CreateMaterials();
    }

    private void Start()
    {
        int fill = Mathf.RoundToInt(maxAgents * initialFillRate);
        for (int attempt = 0; attempt < fill * 4 && active.Count < fill; attempt++)
        {
            TrySpawn(true);
        }
    }

    private void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        spawnTimer = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        if (active.Count < maxAgents) TrySpawn(false);
    }

    //inside=true のときは通路の途中に出す（開始時の埋め用）
    private bool TrySpawn(bool inside)
    {
        float halfW = Mathf.Max(0f, areaSize.x * 0.5f - agentRadius - 0.15f);
        float halfL = areaSize.y * 0.5f;
        int dir = Random.value < 0.5f ? 1 : -1; //+1: -Z側 → +Z側

        float startZ = inside ? Random.Range(-halfL, halfL) : -dir * halfL;
        float endZ = dir * halfL;

        Vector3 start = areaCenter + new Vector3(Random.Range(-halfW, halfW), agentHeight * 0.5f, startZ);
        Vector3 mid = areaCenter + new Vector3(Random.Range(-halfW, halfW), 0f, Mathf.Lerp(startZ, endZ, Random.Range(0.3f, 0.7f)));
        Vector3 end = areaCenter + new Vector3(Random.Range(-halfW, halfW), 0f, endZ);

        //出現位置に既に他のNPCがいないか確認（床などを拾わないよう npcMask のみ対象）
        float bottomY = agentRadius + 0.05f;
        float topY = Mathf.Max(bottomY, agentHeight - agentRadius);
        Vector3 p0 = new Vector3(start.x, start.y - agentHeight * 0.5f + bottomY, start.z);
        Vector3 p1 = new Vector3(start.x, start.y - agentHeight * 0.5f + topY, start.z);

        if (Physics.CheckCapsule(p0, p1, agentRadius * 0.9f, npcMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        WarpDemoCrowdAgent agent = pool.Count > 0 ? pool.Dequeue() : CreateAgent();
        agent.transform.SetPositionAndRotation(start, Quaternion.identity);
        agent.gameObject.SetActive(true);
        agent.Begin(this, mid, end, Random.Range(speedRange.x, speedRange.y), npcMask);
        active.Add(agent);
        return true;
    }

    public void Despawn(WarpDemoCrowdAgent agent)
    {
        if (!active.Remove(agent)) return;
        agent.gameObject.SetActive(false);
        pool.Enqueue(agent);
    }

    private WarpDemoCrowdAgent CreateAgent()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "CrowdAgent";
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(agentRadius * 2f, agentHeight * 0.5f, agentRadius * 2f);
        go.tag = "NPC";
        go.layer = npcLayer;

        var body = go.GetComponent<Renderer>();
        if (materials != null && materials.Length > 0)
        {
            body.sharedMaterial = materials[Random.Range(0, materials.Length)];
        }

        //向きが分かるように頭の前に小さな目印
        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Nose";
        var noseCol = nose.GetComponent<Collider>();
        if (noseCol != null) Destroy(noseCol);
        nose.transform.SetParent(go.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.6f, 0.45f);
        nose.transform.localScale = new Vector3(0.35f, 0.12f, 0.2f);
        nose.layer = npcLayer;
        var noseRenderer = nose.GetComponent<Renderer>();
        if (body != null) noseRenderer.sharedMaterial = body.sharedMaterial;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = agentMass;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        go.SetActive(false);
        return go.AddComponent<WarpDemoCrowdAgent>();
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (palette == null || palette.Length == 0)
        {
            materials = new Material[0];
            return;
        }

        materials = new Material[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            var mat = new Material(shader) { name = "CrowdAgent_" + i };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", palette[i]);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", palette[i]);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            materials[i] = mat;
        }
    }

#if UNITY_EDITOR
    //デモシーン生成用
    public void EditorSetup(Vector3 center, Vector2 size, int max)
    {
        areaCenter = center;
        areaSize = size;
        maxAgents = max;
    }
#endif

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(areaCenter + Vector3.up * 0.05f, new Vector3(areaSize.x, 0.1f, areaSize.y));
    }
}
