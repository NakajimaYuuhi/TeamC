/////////////////////////////////////////
/// PlayerData.cs
/// 2026/10/20 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/20 佐藤蒼也：プレイヤーのデータをScriptableObject化
/// 
/// PlayerMove.csで使用するプレイヤーの移動速度、回転速度、サイズ、声の大きさ、押す力、迷惑度を格納しています
/// 
/////////////////////////////////////////


using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerData : ScriptableObject
{
    [SerializeField] public float MoveSpeed = 5.0f;                             //プレイヤー移動速度
    [SerializeField] public float TurnSpeed = 2.0f;                             //プレイヤー回転速度
    [SerializeField] public Vector3 PlayerSize = new Vector3(1.0f,1.0f,1.0f);   //プレイヤーのサイズ
    [SerializeField] public float VoiceVolume = 1.0f;                           //プレイヤーの声の大きさ
    [SerializeField, Range(10.0f, 50.0f)] public float PlayerPushPower = 30.0f;    //プレイヤーの押す力
    [SerializeField] public float PlayerNuisance = 0.0f;                        //プレイヤーの迷惑度
}