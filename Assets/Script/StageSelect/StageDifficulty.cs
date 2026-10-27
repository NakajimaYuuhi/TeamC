// 10/27 佐藤作成

using UnityEngine;
using UnityEngine.UI;

//カードの難易度表示。Inspectorで難易度を決めると、バーの画像が切りかわる
[ExecuteAlways]
public class StageDifficulty : MonoBehaviour
{
    [Tooltip("難易度")]
    [Range(1, 5)][SerializeField] int difficulty = 3;

    [Tooltip("左から順にバーの Image")]
    [SerializeField] Image[] bars = new Image[5];
    [Tooltip("ついているバー")]
    [SerializeField] Sprite barOn;
    [Tooltip("消えているバー")]
    [SerializeField] Sprite barOff;

    public int Difficulty
    {
        get => difficulty;
        set { difficulty = Mathf.Clamp(value, 1, bars.Length); Refresh(); }
    }

    void OnEnable() => Refresh();

    void OnValidate() => Refresh();

    void Refresh()
    {
        if (bars == null) return;
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null) continue;
            var sprite = i < difficulty ? barOn : barOff;
            if (sprite != null) bars[i].sprite = sprite;
        }
    }
}
