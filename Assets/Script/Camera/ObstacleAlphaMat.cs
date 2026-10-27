using UnityEngine;

public class ObstacleAlphaMat : MonoBehaviour
{
    private MeshRenderer[] _renderers;
    private Material[][] _originalMaterials; // 元のマテリアルに戻す際に必要
    private bool _isTransparent;
    
   
    private void Awake()
    {
        _renderers = GetComponentsInChildren<MeshRenderer>(true);

        _originalMaterials = new Material[_renderers.Length][];

        for(int i = 0;i < _originalMaterials.Length;++i)
        {
            _originalMaterials[i] = _renderers[i].sharedMaterials;
        }
    }

    public void SetTransparent(bool transparent, Material mat)
    {
        if (_isTransparent == transparent) return;

        if (transparent && mat == null)
            return;

        for (int i = 0; i < _renderers.Length; ++i)
        {
            if (_renderers[i] == null) continue;

            if(transparent)
            {
                Material[] materials = new Material[_originalMaterials[i].Length];

                for(int j = 0;j < materials.Length;++j)
                {
                    materials[j] = mat;
                }

                _renderers[i].sharedMaterials = materials;
            }
            else
            {
                _renderers[i].sharedMaterials = _originalMaterials[i];
            }
        }

        _isTransparent = transparent;
    }

    private void OnDestroy()
    {
        if (_renderers == null || _originalMaterials == null)
            return;

        for(int i = 0;i < _renderers.Length;++i)
        {
            if (_renderers[i] != null)
                _renderers[i].sharedMaterials = _originalMaterials[i];
        }
    }
}
