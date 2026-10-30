using UnityEngine;

public class DestinationData : ScriptableObject
{
    [Header("目的地設定")]
    [SerializeField, InspectorName("目的地名")]
    [Tooltip("目的地を識別するための名前です。")]
    private string destinationName;

    [SerializeField, InspectorName("目的地座標")]
    [Tooltip("目的地のワールド座標です。")]
    private Vector3 destinationPosition;

    [SerializeField, InspectorName("達成範囲中心オフセット X")]
    [Tooltip("目的地から達成範囲の中心をX方向へずらす距離です。")]
    private float achievementCenterOffsetX;

    [SerializeField, InspectorName("達成範囲中心オフセット Z")]
    [Tooltip("目的地から達成範囲の中心をZ方向へずらす距離です。")]
    private float achievementCenterOffsetZ;

    [SerializeField, Min(0f), InspectorName("目標時間（秒）")]
    [Tooltip("目的地へ到達するまでの目標時間です。")]
    private float targetTime;

    [SerializeField, Min(0f), InspectorName("達成範囲 X")]
    [Tooltip("XZ平面上で目的地へ到達したとみなす長方形のX方向の全幅です。")]
    private float achievementSizeX;

    [SerializeField, Min(0f), InspectorName("達成範囲 Z")]
    [Tooltip("XZ平面上で目的地へ到達したとみなす長方形のZ方向の全幅です。")]
    private float achievementSizeZ;

    [SerializeField, Min(0f), InspectorName("達成範囲 Y")]
    [Tooltip("高さの全幅です。0 なら高さは見ません。階が重なったステージで使います。")]
    private float achievementSizeY;

    public string DestinationName => destinationName;
    public Vector3 DestinationPosition => destinationPosition;
    public float AchievementCenterOffsetX => achievementCenterOffsetX;
    public float AchievementCenterOffsetZ => achievementCenterOffsetZ;
    public Vector3 AchievementCenterPosition => destinationPosition
        + new Vector3(achievementCenterOffsetX, 0f, achievementCenterOffsetZ);
    public float TargetTime => targetTime;
    public float AchievementSizeX => achievementSizeX;
    public float AchievementSizeZ => achievementSizeZ;
    public float AchievementSizeY => achievementSizeY;

    public bool IsWithinAchievementRange(Vector3 position)
    {
        var offset = position - AchievementCenterPosition;
        return Mathf.Abs(offset.x) <= achievementSizeX * 0.5f
            && Mathf.Abs(offset.z) <= achievementSizeZ * 0.5f
            && (achievementSizeY <= 0f || Mathf.Abs(offset.y) <= achievementSizeY * 0.5f);
    }
}
