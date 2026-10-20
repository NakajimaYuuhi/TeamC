using System;
using System.Collections.Generic;
using UnityEngine;

public enum NpcSpawnAreaTag
{
    WalkArea,
    StandArea,
    GroupArea
}

[Serializable]
public class NpcSpawnArea
{
    [SerializeField, InspectorName("中心位置")]
    [Tooltip("Boxの中心のワールド座標です。")]
    private Vector3 center;

    [SerializeField, InspectorName("サイズ")]
    [Tooltip("ワールドXYZ軸方向のBoxの全幅です。各成分に0以上を指定してください。")]
    private Vector3 size = Vector3.one;

    [SerializeField, InspectorName("エリアタグ")]
    [Tooltip("このBoxの用途です。GameObjectのTagとは独立した分類です。")]
    private NpcSpawnAreaTag tag;

    public Vector3 Center => center;
    public Vector3 Size => size;
    public NpcSpawnAreaTag Tag => tag;
}

public class NpcSpawnAreaData : ScriptableObject
{
    [SerializeField, InspectorName("NPC配置範囲一覧")]
    [Tooltip("ステージで使用するBoxの一覧です。")]
    private List<NpcSpawnArea> areas = new List<NpcSpawnArea>();

    public IReadOnlyList<NpcSpawnArea> Areas => areas;

    public NpcSpawnArea GetArea(int index)
    {
        return areas[index];
    }
}
