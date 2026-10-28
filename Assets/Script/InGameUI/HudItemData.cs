using UnityEngine;

//アイテム1種類分の、画面に出すもの(アイコン・名前・説明文)
//・Project ビューで右クリック → Create → 押して参る → アイテムの見た目 で作る
//・名前と説明文は画像。決まったら画像を差し替える
[CreateAssetMenu(menuName = "押して参る/アイテムの見た目", fileName = "Item_")]
public class HudItemData : ScriptableObject
{
    [Tooltip("アイテム欄に出すアイコン(正方形の画像。墨のふちを付けておくと、どの背景でも読める)")]
    public Sprite icon;
    [Tooltip("説明の札に出すアイテム名の画像(横 366 × 縦 38 まで)")]
    public Sprite nameImage;
    [Tooltip("説明の札に出す説明文の画像(2行まで。横 366 × 縦 62 まで)")]
    public Sprite descriptionImage;
}
