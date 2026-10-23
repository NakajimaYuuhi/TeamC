using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerOcclusionHighlightTarget : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField, InspectorName("対象モデル")]
    [Tooltip("ハイライト表示するプレイヤーモデルの親Transformです。")]
    private Transform visualRoot;

    [SerializeField, InspectorName("判定カメラ")]
    [Tooltip("プレイヤーが隠れているかを判定するカメラです。")]
    private Camera targetCamera;

    [SerializeField, InspectorName("ハイライトマテリアル")]
    [Tooltip("隠れた部分を描画するマテリアルです。")]
    private Material highlightMaterial;

    [Header("遮蔽設定")]
    [SerializeField, InspectorName("遮蔽物レイヤー")]
    [Tooltip("プレイヤーを隠したときにハイライトを有効にするGameObjectレイヤーです。")]
    private LayerMask occluderLayers;

    private readonly List<GameObject> highlightObjects = new List<GameObject>();
    private Renderer[] targetRenderers;

    private void Awake()
    {
        if (visualRoot == null || highlightMaterial == null)
        {
            enabled = false;
            return;
        }

        targetRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var targetRenderer in targetRenderers)
        {
            CreateHighlightObject(targetRenderer);
        }

        SetHighlightVisible(false);
    }

    private void LateUpdate()
    {
        if (targetCamera == null || targetRenderers.Length == 0)
        {
            SetHighlightVisible(false);
            return;
        }

        var targetPosition = GetTargetPosition();
        var isOccluded = Physics.Linecast(
            targetCamera.transform.position,
            targetPosition,
            occluderLayers,
            QueryTriggerInteraction.Ignore);

        SetHighlightVisible(isOccluded);
    }

    private void OnDisable()
    {
        SetHighlightVisible(false);
    }

    private void OnDestroy()
    {
        foreach (var highlightObject in highlightObjects)
        {
            if (highlightObject != null)
            {
                Destroy(highlightObject);
            }
        }
    }

    private Vector3 GetTargetPosition()
    {
        var hasBounds = false;
        var targetBounds = new Bounds();

        foreach (var targetRenderer in targetRenderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                targetBounds = targetRenderer.bounds;
                hasBounds = true;
                continue;
            }

            targetBounds.Encapsulate(targetRenderer.bounds);
        }

        return hasBounds ? targetBounds.center : visualRoot.position;
    }

    private void CreateHighlightObject(Renderer targetRenderer)
    {
        if (targetRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
        {
            CreateSkinnedMeshHighlight(skinnedMeshRenderer);
            return;
        }

        if (targetRenderer is MeshRenderer meshRenderer
            && meshRenderer.TryGetComponent(out MeshFilter meshFilter)
            && meshFilter.sharedMesh != null)
        {
            CreateMeshHighlight(meshRenderer, meshFilter.sharedMesh);
        }
    }

    private void CreateMeshHighlight(MeshRenderer sourceRenderer, Mesh mesh)
    {
        var highlightObject = CreateHighlightGameObject(sourceRenderer);
        var meshFilter = highlightObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        var highlightRenderer = highlightObject.AddComponent<MeshRenderer>();
        ApplyRendererSettings(highlightRenderer, mesh.subMeshCount);
    }

    private void CreateSkinnedMeshHighlight(SkinnedMeshRenderer sourceRenderer)
    {
        if (sourceRenderer.sharedMesh == null)
        {
            return;
        }

        var highlightObject = CreateHighlightGameObject(sourceRenderer);
        var highlightRenderer = highlightObject.AddComponent<SkinnedMeshRenderer>();
        highlightRenderer.sharedMesh = sourceRenderer.sharedMesh;
        highlightRenderer.bones = sourceRenderer.bones;
        highlightRenderer.rootBone = sourceRenderer.rootBone;
        highlightRenderer.localBounds = sourceRenderer.localBounds;
        highlightRenderer.quality = sourceRenderer.quality;
        highlightRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        ApplyRendererSettings(highlightRenderer, sourceRenderer.sharedMesh.subMeshCount);
    }

    private GameObject CreateHighlightGameObject(Renderer sourceRenderer)
    {
        var highlightObject = new GameObject($"{sourceRenderer.gameObject.name}_OcclusionHighlight");
        highlightObject.hideFlags = HideFlags.HideAndDontSave;
        highlightObject.layer = sourceRenderer.gameObject.layer;
        highlightObject.transform.SetParent(sourceRenderer.transform, false);
        highlightObjects.Add(highlightObject);
        return highlightObject;
    }

    private void ApplyRendererSettings(Renderer highlightRenderer, int materialCount)
    {
        var materials = new Material[materialCount];
        for (var index = 0; index < materials.Length; index++)
        {
            materials[index] = highlightMaterial;
        }

        highlightRenderer.sharedMaterials = materials;
        highlightRenderer.shadowCastingMode = ShadowCastingMode.Off;
        highlightRenderer.receiveShadows = false;
        highlightRenderer.lightProbeUsage = LightProbeUsage.Off;
        highlightRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        highlightRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void SetHighlightVisible(bool isVisible)
    {
        foreach (var highlightObject in highlightObjects)
        {
            if (highlightObject != null && highlightObject.activeSelf != isVisible)
            {
                highlightObject.SetActive(isVisible);
            }
        }
    }
}
