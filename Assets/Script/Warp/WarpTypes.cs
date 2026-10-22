using System;
using UnityEngine;

public enum WarpFailReason
{
    None,
    Cooldown,
    NoDirection,
    NoGap,
    AngleMismatch,
    Blocked
}

public enum WarpPathCheckMode
{
    Linecast,
    CapsuleCast
}

public struct WarpCandidate
{
    public Vector3 position;
    public float distance;
    public float gapWidth;
    public float angle;
    public GameObject npcA;
    public GameObject npcB;
}

[Serializable]
public struct WarpSearchResult
{
    public bool success;
    public WarpFailReason reason;
    public WarpCandidate best;
    public float closestAngle;
}

[Serializable]
public class WarpGapSettings
{
    [Header("固定探索範囲（XZ平面、ワールド座標）")]
    public Vector3 areaStart = new Vector3(-5f, 0f, -5f);
    public Vector3 areaEnd = new Vector3(5f, 0f, 5f);

    [Tooltip("指定した場合はTransformのワールド座標を優先する")]
    public Transform areaStartTransform;
    public Transform areaEndTransform;

    [Min(0.1f)] public float gridCellSize = 2f;

    [Header("プレイヤー周辺の検索条件")]
    [Min(0.01f)] public float searchRadius = 6f;
    [Min(0f)] public float minDistance = 0.8f;
    [Min(0.01f)] public float maxDistance = 5f;

    [Header("NPC収集用の高さ（地形高さの計算には使用しない）")]
    [Min(0.1f)] public float npcQueryHeight = 4f;
    public float npcQueryCenterYOffset = 1f;

    [Header("隙間")]
    [Min(0f)] public float margin = 0.1f;
    [Min(0.01f)] public float maxGapWidth = 2.5f;
    [Min(0.01f)] public float sampleSpacing = 0.35f;
    [Min(3)] public int maxSamplesPerGap = 9;
    [Min(0f)] public float mergeDistance = 0.15f;

    [Header("入力方向・距離評価")]
    [Range(0.1f, 180f)] public float angleTolerance = 20f;
    [Min(0f)] public float angleWeight = 1f;
    [Min(0f)] public float distanceWeight = 2f; // 短距離を優先する
    [Min(0f)] public float stickyRadius = 0.6f;
    [Min(0f)] public float switchThreshold = 0.25f;
    [Min(0f)] public float confirmRadius = 0.5f;

    [Header("Physics")]
    public LayerMask npcMask;
    public LayerMask obstacleMask; // 地面とプレイヤー自身のLayerを含めない
    public WarpPathCheckMode pathCheckMode = WarpPathCheckMode.CapsuleCast;
    [Min(64)] public int maxOverlapColliders = 1024;

    [Header("Gizmosデバッグ表示")]
    public bool drawDebugArea = true;
    public bool drawDebugGrid = true;
    public float debugLineYOffset = 0.05f;

    public Vector3 GetStartPosition()
    {
        return areaStartTransform != null ? areaStartTransform.position : areaStart;
    }

    public Vector3 GetEndPosition()
    {
        return areaEndTransform != null ? areaEndTransform.position : areaEnd;
    }

    // 始点・終点の指定順にかかわらず、正規化した矩形を返す。
    public Rect GetAreaRectXZ()
    {
        Vector3 a = GetStartPosition();
        Vector3 b = GetEndPosition();
        return Rect.MinMaxRect(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.z, b.z),
            Mathf.Max(a.x, b.x),
            Mathf.Max(a.z, b.z));
    }

    public bool ContainsXZ(Vector3 p)
    {
        Rect r = GetAreaRectXZ();
        return p.x >= r.xMin && p.x <= r.xMax &&
               p.z >= r.yMin && p.z <= r.yMax;
    }
}
