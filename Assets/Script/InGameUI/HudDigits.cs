using UnityEngine;
using UnityEngine.UI;

//数字を画像で出す。並べた Image に、右から順に1文字ずつ入れる
//・使わない欄は消える
//・「.」は dot の画像になる
public class HudDigits : MonoBehaviour
{
    [Tooltip("数字の画像 0～9 を順番に")]
    [SerializeField] Sprite[] digits = new Sprite[10];
    [Tooltip("小数点の画像")]
    [SerializeField] Sprite dot;
    [Tooltip("数字を入れる Image。左から順に")]
    [SerializeField] Image[] slots;

    string current;

    public void SetText(string text)
    {
        if (text == current) return;
        current = text;
        int n = slots.Length;
        for (int i = 0; i < n; i++)
        {
            //右端からの位置で、入れる文字を決める
            int ci = text.Length - (n - i);
            var slot = slots[i];
            if (ci < 0)
            {
                slot.enabled = false;
                continue;
            }
            char c = text[ci];
            Sprite s = c == '.' ? dot : (c >= '0' && c <= '9' ? digits[c - '0'] : null);
            slot.enabled = s != null;
            slot.sprite = s;
        }
    }
}
