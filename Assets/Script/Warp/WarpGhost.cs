/////////////////////////////////////////
/// WarpGhost.cs
/// ワープ候補位置に表示する「半透明のプレイヤー」
///
/// プレイヤーの見た目（MeshRenderer / SkinnedMeshRenderer）だけを複製し、
/// コライダーやスクリプトは持たない。
/////////////////////////////////////////

using UnityEngine;
using UnityEngine.Rendering;

public class WarpGhost : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static readonly Color DefaultColor = new Color(0.35f, 0.85f, 1f, 0.4f);

    [SerializeField] private float pulseSpeed = 4f;     //明滅の速さ
    [SerializeField] private float pulseAmount = 0.15f; //明滅の強さ（アルファ）

    private Renderer[] renderers;
    private MaterialPropertyBlock mpb;
    private Color baseColor = DefaultColor;

    public bool IsVisible => gameObject.activeSelf;

    //source の見た目を複製してゴーストを作る
    public static WarpGhost Create(Transform source, Material material)
    {
        if (material == null) material = CreateRuntimeMaterial();

        var go = new GameObject(source.name + "_WarpGhost");
        var ghost = go.AddComponent<WarpGhost>();

        go.transform.SetPositionAndRotation(source.position, source.rotation);
        go.transform.localScale = source.lossyScale;

        //source のローカル空間へ変換する行列
        Matrix4x4 toLocal = Matrix4x4.TRS(source.position, source.rotation, source.lossyScale).inverse;

        foreach (Renderer r in source.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;

            Mesh mesh = null;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            if (mesh == null) continue;

            var part = new GameObject(r.name);
            part.transform.SetParent(go.transform, false);

            Matrix4x4 rel = toLocal * r.transform.localToWorldMatrix;
            part.transform.localPosition = rel.GetColumn(3);
            part.transform.localRotation = rel.rotation;
            part.transform.localScale = rel.lossyScale;

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = part.AddComponent<MeshRenderer>();
            var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        ghost.renderers = go.GetComponentsInChildren<Renderer>(true);
        ghost.mpb = new MaterialPropertyBlock();
        if (material.HasProperty(BaseColorId)) ghost.baseColor = material.GetColor(BaseColorId);
        else if (material.HasProperty(ColorId)) ghost.baseColor = material.GetColor(ColorId);

        go.SetActive(false);
        return ghost;
    }

    //指定位置に表示する（snap=false のときはなめらかに追従）
    public void Show(Vector3 position, Quaternion rotation, Vector3 scale, bool snap, float followSpeed)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            snap = true;
        }

        if (snap || followSpeed <= 0f)
        {
            transform.position = position;
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, position, t);
        }
        transform.rotation = rotation;
        transform.localScale = scale;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void Update()
    {
        if (renderers == null || mpb == null) return;

        //ふわっと明滅させる
        Color c = baseColor;
        c.a = Mathf.Clamp01(baseColor.a + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        foreach (Renderer r in renderers)
        {
            if (r != null) r.SetPropertyBlock(mpb);
        }
    }

    //---------------- マテリアル ----------------

    //マテリアルが指定されていないときの予備（URP Unlit の半透明）
    public static Material CreateRuntimeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "WarpGhost_Runtime" };
        ConfigureTransparent(mat, DefaultColor);
        return mat;
    }

    //URPのマテリアルを半透明に設定する（エディタでのアセット作成にも使う）
    public static void ConfigureTransparent(Material mat, Color color)
    {
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); //Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     //Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Back);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent;

        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
    }
}
