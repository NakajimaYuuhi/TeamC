// 10/27 佐藤作成

using UnityEngine;
using UnityEngine.UI;

//ステージクリア数表示用スクリプト
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class ClearCountImage : MonoBehaviour
{
    [Tooltip("クリア数")]
    [Range(0, 5)][SerializeField] int cleared = 0;

    [Tooltip("クリア数画像")]
    [SerializeField] Sprite[] sprites = new Sprite[6];

    public int Cleared
    {
        get => cleared;
        set { cleared = Mathf.Clamp(value, 0, sprites.Length - 1); Refresh(); }
    }

    void OnEnable() => Refresh();

    void OnValidate() => Refresh();

    void Refresh()
    {
        if (sprites == null || cleared < 0 || cleared >= sprites.Length || sprites[cleared] == null) return;
        var image = GetComponent<Image>();
        if (image != null) image.sprite = sprites[cleared];
    }
}
