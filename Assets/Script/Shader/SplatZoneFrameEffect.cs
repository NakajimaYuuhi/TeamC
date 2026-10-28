using System.Collections.Generic;
using UnityEngine;

public class SplatZoneFrameEffect : MonoBehaviour
{
    [Header("Material")]
    [SerializeField]
    private Material frameMaterial;

    [Header("Frame Shape")]
    [SerializeField, Min(1)]
    private int frameCount = 4;

    [SerializeField, Min(0.01f)]
    private float frameWidth = 4.0f;

    [SerializeField, Min(0.01f)]
    private float frameDepth = 4.0f;

    [SerializeField, Min(0.001f)]
    private float frameThickness = 0.12f;

    [SerializeField]
    private Color frameColor = new Color(0.3f, 1f, 0.05f, 1f);

    [Header("Animation")]
    [SerializeField]
    private float startHeight = 0.05f;

    [SerializeField, Min(0.01f)]
    private float riseHeight = 2.0f;

    [SerializeField, Min(0.01f)]
    private float cycleDuration = 2.0f;

    [SerializeField]
    private bool loop = true;

    [SerializeField]
    private AnimationCurve heightCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [SerializeField]
    private AnimationCurve alphaCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.35f, 1f),
        new Keyframe(0.75f, 0.4f),
        new Keyframe(1f, 0f)
    );

    [Header("Ground Grid")]
    [SerializeField]
    private bool generateGrid = true;

    [SerializeField, Min(1)]
    private int gridCount = 12;

    [SerializeField, Min(0f)]
    private float gridGap = 0.04f;

    [SerializeField]
    private float gridHeight = 0.01f;

    [SerializeField]
    private Color gridColor = new Color(0.05f, 0.5f, 0.05f, 0.7f);

    private readonly List<FrameData> frames = new();

    private Mesh frameMesh;
    private Mesh gridMesh;

    private GameObject gridObject;

    private MaterialPropertyBlock propertyBlock;

    private float elapsedTime;

    private static readonly int BaseColorID =
        Shader.PropertyToID("_BaseColor");

    private static readonly int AlphaID =
        Shader.PropertyToID("_Alpha");

    private class FrameData
    {
        public Transform transform;
        public MeshRenderer renderer;
        public float phase;
    }

    private void Awake()
    {
        if (frameMaterial == null)
        {
            Debug.LogError(
                "[SplatZoneFrameEffect] Materialが設定されていません。",
                this
            );

            enabled = false;
            return;
        }

        propertyBlock = new MaterialPropertyBlock();

        Generate();
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        UpdateFrames();
    }

    // 枠と地面のメッシュを生成
    private void Generate()
    {
        frameMesh = CreateFrameMesh(
            frameWidth,
            frameDepth,
            frameThickness
        );

        GenerateFrames();

        if (generateGrid)
        {
            GenerateGrid();
        }

        UpdateFrames();
    }

    // 四角い枠を生成
    private Mesh CreateFrameMesh(
        float width,
        float depth,
        float thickness)
    {
        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;

        float innerHalfWidth =
            Mathf.Max(0f, halfWidth - thickness);

        float innerHalfDepth =
            Mathf.Max(0f, halfDepth - thickness);

        Vector3[] vertices =
        {
            // 外周
            new(-halfWidth, 0f, -halfDepth),
            new(-halfWidth, 0f,  halfDepth),
            new( halfWidth, 0f,  halfDepth),
            new( halfWidth, 0f, -halfDepth),

            // 内周
            new(-innerHalfWidth, 0f, -innerHalfDepth),
            new(-innerHalfWidth, 0f,  innerHalfDepth),
            new( innerHalfWidth, 0f,  innerHalfDepth),
            new( innerHalfWidth, 0f, -innerHalfDepth)
        };

        int[] triangles =
        {
            0, 1, 5,
            0, 5, 4,

            1, 2, 6,
            1, 6, 5,

            2, 3, 7,
            2, 7, 6,

            3, 0, 4,
            3, 4, 7
        };

        Mesh mesh = new Mesh
        {
            name = "SplatZoneFrameMesh",
            vertices = vertices,
            triangles = triangles
        };

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    // 上昇する枠を生成
    private void GenerateFrames()
    {
        frames.Clear();

        for (int i = 0; i < frameCount; i++)
        {
            GameObject obj = new GameObject(
                $"Frame_{i}"
            );

            obj.transform.SetParent(
                transform,
                false
            );

            MeshFilter filter =
                obj.AddComponent<MeshFilter>();

            MeshRenderer renderer =
                obj.AddComponent<MeshRenderer>();

            filter.sharedMesh = frameMesh;

            renderer.sharedMaterial = frameMaterial;

            FrameData data = new FrameData
            {
                transform = obj.transform,
                renderer = renderer,
                phase = (float)i / frameCount
            };

            frames.Add(data);
        }
    }

    // 地面の格子を生成
    private void GenerateGrid()
    {
        gridMesh = CreateGridMesh();

        gridObject = new GameObject(
            "GroundGrid"
        );

        gridObject.transform.SetParent(
            transform,
            false
        );

        gridObject.transform.localPosition =
            new Vector3(0f, gridHeight, 0f);

        MeshFilter filter =
            gridObject.AddComponent<MeshFilter>();

        MeshRenderer renderer =
            gridObject.AddComponent<MeshRenderer>();

        filter.sharedMesh = gridMesh;

        renderer.sharedMaterial = frameMaterial;

        MaterialPropertyBlock block =
            new MaterialPropertyBlock();

        block.SetColor(
            BaseColorID,
            gridColor
        );

        block.SetFloat(
            AlphaID,
            gridColor.a
        );

        renderer.SetPropertyBlock(block);
    }

    // 格子状のメッシュを生成
    private Mesh CreateGridMesh()
    {
        List<Vector3> vertices = new();
        List<int> triangles = new();

        float cellWidth = frameWidth / gridCount;
        float cellDepth = frameDepth / gridCount;

        float halfWidth = frameWidth * 0.5f;
        float halfDepth = frameDepth * 0.5f;

        for (int y = 0; y < gridCount; y++)
        {
            for (int x = 0; x < gridCount; x++)
            {
                float minX =
                    -halfWidth + x * cellWidth +
                    gridGap * 0.5f;

                float maxX =
                    -halfWidth + (x + 1) * cellWidth -
                    gridGap * 0.5f;

                float minZ =
                    -halfDepth + y * cellDepth +
                    gridGap * 0.5f;

                float maxZ =
                    -halfDepth + (y + 1) * cellDepth -
                    gridGap * 0.5f;

                int index = vertices.Count;

                vertices.Add(
                    new Vector3(minX, 0f, minZ)
                );

                vertices.Add(
                    new Vector3(minX, 0f, maxZ)
                );

                vertices.Add(
                    new Vector3(maxX, 0f, maxZ)
                );

                vertices.Add(
                    new Vector3(maxX, 0f, minZ)
                );

                triangles.Add(index + 0);
                triangles.Add(index + 1);
                triangles.Add(index + 2);

                triangles.Add(index + 0);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }
        }

        Mesh mesh = new Mesh
        {
            name = "SplatZoneGridMesh",
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };

        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        return mesh;
    }

    // 枠の上昇・透明度を更新
    private void UpdateFrames()
    {
        if (propertyBlock == null)
            return;

        float normalizedTime =
            elapsedTime / cycleDuration;

        for (int i = 0; i < frames.Count; i++)
        {
            FrameData frame = frames[i];

            float progress =
                normalizedTime + frame.phase;

            if (loop)
            {
                progress = Mathf.Repeat(
                    progress,
                    1f
                );
            }
            else
            {
                progress = Mathf.Clamp01(
                    progress
                );
            }

            float heightT =
                heightCurve.Evaluate(progress);

            float height =
                startHeight +
                heightT * riseHeight;

            frame.transform.localPosition =
                new Vector3(0f, height, 0f);

            float alpha =
                alphaCurve.Evaluate(progress);

            alpha = Mathf.Clamp01(alpha);

            propertyBlock.Clear();

            propertyBlock.SetColor(
                BaseColorID,
                frameColor
            );

            propertyBlock.SetFloat(
                AlphaID,
                alpha
            );

            frame.renderer.SetPropertyBlock(
                propertyBlock
            );
        }
    }

    private void OnDestroy()
    {
        if (frameMesh != null)
            Destroy(frameMesh);

        if (gridMesh != null)
            Destroy(gridMesh);
    }
}