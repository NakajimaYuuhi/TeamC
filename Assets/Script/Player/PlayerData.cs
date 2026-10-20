using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Scriptable Objects/PlayerData")]
public class PlayerData : ScriptableObject
{
    [SerializeField] public float MoveSpeed = 5.0f;  //プレイヤー移動速度
    [SerializeField] public float LeanSpeed = 2.0f;  //プレイヤー回転速度
    [SerializeField] public Vector3 PlayerSize = new Vector3(1.0f,1.0f,1.0f); //プレイヤーのサイズ
    [SerializeField] public float VoiceVolume = 1.0f;//プレイヤーの声の大きさ
    [SerializeField, Range(0.1f, 0.5f)] public float PlayerPushPower = 0.3f; //プレイヤーの押す力
    [SerializeField] public float PlayerNuisance = 0.5f; //プレイヤーの迷惑度
}