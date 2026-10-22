using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WarpGapFinder
{
    private struct NpcInfo
    {
        public GameObject root;
        public Vector3 pos;
        public float radius;
        public float minX, maxX, minZ, maxZ;
    }

    private readonly WarpGapSettings s;
    private readonly float playerRadius;
    private readonly Vector3 capsuleOffset0;
    private readonly Vector3 capsuleOffset1;

    private Collider[] overlapBuffer = new Collider[64];
    private readonly Dictionary<GameObject, int> npcIndex = new Dictionary<GameObject, int>();
    private readonly List<NpcInfo> npcs = new List<NpcInfo>();
    private readonly Dictionary<long, List<int>> spatialGrid = new Dictionary<long, List<int>>();
    private readonly Stack<List<int>> bucketPool = new Stack<List<int>>();
    private readonly HashSet<ulong> processedPairs = new HashSet<ulong>();
    private readonly List<WarpCandidate> candidates = new List<WarpCandidate>();

    private bool hadGeometricCandidate;
    private bool warnedBufferLimit;

    public IReadOnlyList<WarpCandidate> Candidates => candidates;
    public int LastNpcCount { get; private set; }
    public int LastPairCount { get; private set; }
    public int LastSampleCount { get; private set; }
    public float PlayerRadius => playerRadius;
    public Vector3 CapsuleOffset0 => capsuleOffset0;
    public Vector3 CapsuleOffset1 => capsuleOffset1;

    // Offsetはプレイヤーの基準点から見た、ワールド方向のカプセル両端オフセット。
    public WarpGapFinder(WarpGapSettings settings, float playerRadius, Vector3 offset0, Vector3 offset1)
    {
        s = settings ?? throw new ArgumentNullException(nameof(settings));
        if (playerRadius <= 0f) throw new ArgumentOutOfRangeException(nameof(playerRadius));
        this.playerRadius = playerRadius;
        capsuleOffset0 = offset0;
        capsuleOffset1 = offset1;
        ValidateSettings();
    }

    public void RebuildCandidates(Vector3 origin)
    {
        ValidateSettings();
        candidates.Clear();
        hadGeometricCandidate = false;
        LastNpcCount = 0;
        LastPairCount = 0;
        LastSampleCount = 0;

        Rect area = s.GetAreaRectXZ();
        // ワープ可能範囲と、固定探索領域の共通部分だけを見る。
        Rect targetArea = Rect.MinMaxRect(
            Mathf.Max(area.xMin, origin.x - s.maxDistance),
            Mathf.Max(area.yMin, origin.z - s.maxDistance),
            Mathf.Min(area.xMax, origin.x + s.maxDistance),
            Mathf.Min(area.yMax, origin.z + s.maxDistance));

        if (targetArea.width <= 0f || targetArea.height <= 0f) return;

        CollectNpcs(origin, targetArea);
        LastNpcCount = npcs.Count;
        if (npcs.Count < 2) return;

        // ペアの探索領域は境界外に少し拡張する。
        // 有効なワープ先が境界内にあっても、ペアの重なり領域が外にある場合があるため。
        Rect gridWorkingArea = Rect.MinMaxRect(
            area.xMin - s.maxGapWidth, area.yMin - s.maxGapWidth,
            area.xMax + s.maxGapWidth, area.yMax + s.maxGapWidth);
        BuildSpatialHash(gridWorkingArea);
        FindGapCandidates(origin);
    }

    private void ValidateSettings()
    {
        Rect r = s.GetAreaRectXZ();
        if (r.width <= 0f || r.height <= 0f) throw new ArgumentException("探索範囲のX/Z幅が0です。");
        if (s.gridCellSize <= 0f) throw new ArgumentException("gridCellSizeは正数にしてください。");
        if (s.searchRadius <= 0f) throw new ArgumentException("searchRadiusは正数にしてください。");
        if (s.maxDistance <= 0f || s.minDistance < 0f || s.minDistance > s.maxDistance)
            throw new ArgumentException("ワープ距離の設定が不正です。");
        if (s.margin < 0f || s.maxGapWidth < 2f * (playerRadius + s.margin))
            throw new ArgumentException("maxGapWidthが必要な隙間幅より小さいです。");
        if (s.sampleSpacing <= 0f || s.maxSamplesPerGap < 3 || s.mergeDistance < 0f)
            throw new ArgumentException("候補サンプリングの設定が不正です。");
        if (s.angleTolerance <= 0f || s.angleTolerance > 180f || s.angleWeight < 0f || s.distanceWeight < 0f)
            throw new ArgumentException("角度または評価の設定が不正です。");
        if (s.stickyRadius < 0f || s.switchThreshold < 0f || s.confirmRadius < 0f)
            throw new ArgumentException("候補安定化の設定が不正です。");
        if (s.npcQueryHeight <= 0f || s.maxOverlapColliders < 64)
            throw new ArgumentException("NPC収集の設定が不正です。");
    }

    private void CollectNpcs(Vector3 origin, Rect targetArea)
    {
        npcs.Clear();
        npcIndex.Clear();

        // 境界付近のNPCが隙間を作る可能性があるため、NPC収集だけは少し広くする。
        // プレイヤーの実際のワープ位置は、後段で固定探索範囲内に厳密に制限する。
        float padding = s.maxGapWidth;
        float minX = targetArea.xMin - padding;
        float maxX = targetArea.xMax + padding;
        float minZ = targetArea.yMin - padding;
        float maxZ = targetArea.yMax + padding;

        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            origin.y + s.npcQueryCenterYOffset,
            (minZ + maxZ) * 0.5f);
        Vector3 half = new Vector3(
            (maxX - minX) * 0.5f,
            s.npcQueryHeight * 0.5f,
            (maxZ - minZ) * 0.5f);

        int count;
        while (true)
        {
            count = Physics.OverlapBoxNonAlloc(
                center, half, overlapBuffer, Quaternion.identity,
                s.npcMask, QueryTriggerInteraction.Ignore);
            if (count < overlapBuffer.Length) break;

            if (overlapBuffer.Length >= s.maxOverlapColliders)
            {
                if (!warnedBufferLimit)
                {
                    Debug.LogWarning("WarpGapFinder: NPCの検索用Colliderバッファが上限に達しました。探索結果が不完全な可能性があります。");
                    warnedBufferLimit = true;
                }
                break;
            }
            int newSize = Mathf.Min(overlapBuffer.Length * 2, s.maxOverlapColliders);
            Array.Resize(ref overlapBuffer, newSize);
        }

        float searchSqr = s.searchRadius * s.searchRadius;
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null) continue;
            Bounds bounds = col.bounds;

            // 固定範囲のほかに、従来の検索半径条件も維持する。
            // Boundsによる近似チェックであり、コライダーとの厳密な距離ではない。
            if (bounds.SqrDistance(origin) > searchSqr) continue;

            GameObject root = FindNPCRoot(col.transform);
            if (root == null) continue;

            if (npcIndex.TryGetValue(root, out int index))
            {
                NpcInfo info = npcs[index];
                info.minX = Mathf.Min(info.minX, bounds.min.x);
                info.maxX = Mathf.Max(info.maxX, bounds.max.x);
                info.minZ = Mathf.Min(info.minZ, bounds.min.z);
                info.maxZ = Mathf.Max(info.maxZ, bounds.max.z);
                npcs[index] = info;
            }
            else
            {
                npcIndex.Add(root, npcs.Count);
                npcs.Add(new NpcInfo
                {
                    root = root,
                    minX = bounds.min.x,
                    maxX = bounds.max.x,
                    minZ = bounds.min.z,
                    maxZ = bounds.max.z
                });
            }
        }

        for (int i = 0; i < npcs.Count; i++)
        {
            NpcInfo npc = npcs[i];
            float halfX = (npc.maxX - npc.minX) * 0.5f;
            float halfZ = (npc.maxZ - npc.minZ) * 0.5f;
            npc.pos = new Vector3(
                (npc.minX + npc.maxX) * 0.5f,
                origin.y,
                (npc.minZ + npc.maxZ) * 0.5f);
            // 単純な円近似。最終的な接触判定には実際のColliderを使用する。
            npc.radius = Mathf.Max(halfX, halfZ);
            npcs[i] = npc;
        }
    }

    private void BuildSpatialHash(Rect area)
    {
        foreach (List<int> bucket in spatialGrid.Values)
        {
            bucket.Clear();
            bucketPool.Push(bucket);
        }
        spatialGrid.Clear();

        float cell = s.gridCellSize;
        float expansion = s.maxGapWidth * 0.5f;
        int cellMinX = Mathf.FloorToInt(area.xMin / cell);
        int cellMaxX = Mathf.FloorToInt(area.xMax / cell);
        int cellMinZ = Mathf.FloorToInt(area.yMin / cell);
        int cellMaxZ = Mathf.FloorToInt(area.yMax / cell);

        for (int i = 0; i < npcs.Count; i++)
        {
            NpcInfo npc = npcs[i];
            float r = npc.radius + expansion;
            if (npc.pos.x + r < area.xMin || npc.pos.x - r > area.xMax ||
                npc.pos.z + r < area.yMin || npc.pos.z - r > area.yMax)
                continue;

            int x0 = Mathf.Max(cellMinX, Mathf.FloorToInt((npc.pos.x - r) / cell));
            int x1 = Mathf.Min(cellMaxX, Mathf.FloorToInt((npc.pos.x + r) / cell));
            int z0 = Mathf.Max(cellMinZ, Mathf.FloorToInt((npc.pos.z - r) / cell));
            int z1 = Mathf.Min(cellMaxZ, Mathf.FloorToInt((npc.pos.z + r) / cell));

            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    long key = ((long)x << 32) | (uint)z;
                    if (!spatialGrid.TryGetValue(key, out List<int> bucket))
                    {
                        bucket = bucketPool.Count > 0 ? bucketPool.Pop() : new List<int>(8);
                        spatialGrid.Add(key, bucket);
                    }
                    bucket.Add(i);
                }
            }
        }
    }

    private void FindGapCandidates(Vector3 origin)
    {
        processedPairs.Clear();
        foreach (List<int> bucket in spatialGrid.Values)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                for (int j = i + 1; j < bucket.Count; j++)
                {
                    int a = bucket[i];
                    int b = bucket[j];
                    if (a > b) { int t = a; a = b; b = t; }
                    ulong key = ((ulong)(uint)a << 32) | (uint)b;
                    if (!processedPairs.Add(key)) continue;
                    LastPairCount++;
                    ProcessNpcPair(npcs[a], npcs[b], origin);
                }
            }
        }
    }

    private void ProcessNpcPair(NpcInfo a, NpcInfo b, Vector3 origin)
    {
        Vector3 ab = b.pos - a.pos;
        ab.y = 0f;
        float sqrDistance = ab.sqrMagnitude;
        float maxPairDistance = a.radius + b.radius + s.maxGapWidth;
        float minPairDistance = a.radius + b.radius + 2f * (playerRadius + s.margin);
        if (sqrDistance > maxPairDistance * maxPairDistance ||
            sqrDistance < minPairDistance * minPairDistance || sqrDistance < 0.00000001f)
            return;

        float distance = Mathf.Sqrt(sqrDistance);
        float gapWidth = distance - a.radius - b.radius;
        Vector3 direction = ab / distance;
        float requiredRadius = playerRadius + s.margin;
        float minOffset = a.radius + requiredRadius;
        float maxOffset = distance - b.radius - requiredRadius;
        float length = maxOffset - minOffset;
        if (length < 0f) return;

        int sampleCount = CalculateSampleCount(length);
        int centerIndex = sampleCount / 2;
        for (int i = 0; i < sampleCount; i++)
        {
            // 中央 -> 左 -> 右 -> ... の順で試す。
            int index = i == 0 ? centerIndex :
                ((i & 1) == 1 ? centerIndex - (i + 1) / 2 : centerIndex + i / 2);
            float t = sampleCount == 1 ? 0.5f : index / (float)(sampleCount - 1);
            Vector3 p = a.pos + direction * Mathf.Lerp(minOffset, maxOffset, t);
            p.y = origin.y;
            TryAddCandidate(p, origin, gapWidth, a.root, b.root);
        }
    }

    private int CalculateSampleCount(float length)
    {
        if (length <= 0.0001f) return 1;
        int count = Mathf.Clamp(Mathf.CeilToInt(length / s.sampleSpacing) + 1, 3, s.maxSamplesPerGap);
        return (count & 1) == 0 ? count - 1 : count;
    }

    private void TryAddCandidate(Vector3 p, Vector3 origin, float gapWidth, GameObject a, GameObject b)
    {
        LastSampleCount++;
        // ここが固定探索領域の厳密な制限。
        if (!s.ContainsXZ(p)) return;

        Vector3 to = p - origin;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < s.minDistance || distance > s.maxDistance) return;
        hadGeometricCandidate = true;

        int duplicateIndex = FindNearbyCandidate(p);
        if (duplicateIndex >= 0 && candidates[duplicateIndex].distance <= distance) return;
        if (!IsSpaceFree(p) || !IsReachable(origin, p)) return;

        WarpCandidate c = new WarpCandidate
        {
            position = p,
            distance = distance,
            gapWidth = gapWidth,
            npcA = a,
            npcB = b
        };
        if (duplicateIndex >= 0) candidates[duplicateIndex] = c;
        else candidates.Add(c);
    }

    private int FindNearbyCandidate(Vector3 p)
    {
        float sq = s.mergeDistance * s.mergeDistance;
        int result = -1;
        float minSqr = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3 delta = candidates[i].position - p;
            delta.y = 0f;
            float d = delta.sqrMagnitude;
            if (d < sq && d < minSqr) { result = i; minSqr = d; }
        }
        return result;
    }

    public WarpSearchResult Evaluate(Vector3 origin, Vector3 aim, Vector3? prevPos = null)
    {
        WarpSearchResult result = new WarpSearchResult { closestAngle = float.MaxValue };
        aim.y = 0f;
        if (aim.sqrMagnitude < 0.01f) { result.reason = WarpFailReason.NoDirection; return result; }
        if (candidates.Count == 0)
        {
            result.reason = hadGeometricCandidate ? WarpFailReason.Blocked : WarpFailReason.NoGap;
            return result;
        }

        aim.Normalize();
        float bestScore = float.MaxValue;
        float previousScore = float.MaxValue;
        float previousSqr = float.MaxValue;
        int bestIndex = -1;
        int previousIndex = -1;
        float stickySqr = s.stickyRadius * s.stickyRadius;

        for (int i = 0; i < candidates.Count; i++)
        {
            WarpCandidate c = candidates[i];
            Vector3 to = c.position - origin;
            to.y = 0f;
            c.distance = to.magnitude;
            if (c.distance <= 0.0001f || c.distance < s.minDistance || c.distance > s.maxDistance)
                continue;
            c.angle = Vector3.Angle(aim, to);
            candidates[i] = c;
            result.closestAngle = Mathf.Min(result.closestAngle, c.angle);
            if (c.angle > s.angleTolerance) continue;

            // スコアが小さいほど優先。短距離優先仕様は維持する。
            float score = c.angle / s.angleTolerance * s.angleWeight +
                          c.distance / s.maxDistance * s.distanceWeight;
            if (score < bestScore) { bestScore = score; bestIndex = i; }

            if (!prevPos.HasValue) continue;
            Vector3 delta = c.position - prevPos.Value;
            delta.y = 0f;
            float sq = delta.sqrMagnitude;
            if (sq <= stickySqr && sq < previousSqr)
            {
                previousSqr = sq;
                previousScore = score;
                previousIndex = i;
            }
        }

        if (bestIndex < 0)
        {
            result.reason = result.closestAngle == float.MaxValue
                ? WarpFailReason.NoGap : WarpFailReason.AngleMismatch;
            return result;
        }
        if (previousIndex >= 0 && previousScore <= bestScore + s.switchThreshold)
            bestIndex = previousIndex;

        result.success = true;
        result.reason = WarpFailReason.None;
        result.best = candidates[bestIndex];
        return result;
    }

    public bool IsSpaceFree(Vector3 p)
    {
        int mask = s.npcMask.value | s.obstacleMask.value;
        return !Physics.CheckCapsule(
            p + capsuleOffset0, p + capsuleOffset1,
            playerRadius + s.margin, mask, QueryTriggerInteraction.Ignore);
    }

    private bool IsReachable(Vector3 origin, Vector3 p)
    {
        Vector3 to = p - origin;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance <= 0.0001f) return true;

        if (s.pathCheckMode == WarpPathCheckMode.Linecast)
        {
            Vector3 offset = (capsuleOffset0 + capsuleOffset1) * 0.5f;
            return !Physics.Linecast(origin + offset, p + offset,
                s.obstacleMask, QueryTriggerInteraction.Ignore);
        }

        return !Physics.CapsuleCast(
            origin + capsuleOffset0, origin + capsuleOffset1,
            playerRadius + s.margin, to / distance, distance,
            s.obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // 候補を作ったNPCペアが、再探索後も近い位置で隙間を形成していることを確認。
    public bool TryConfirmCandidate(Vector3 origin, WarpCandidate before, out WarpCandidate confirmed)
    {
        confirmed = default;
        RebuildCandidates(origin);
        float allowedSqr = s.confirmRadius * s.confirmRadius;
        float bestSqr = float.MaxValue;
        int bestIndex = -1;
        for (int i = 0; i < candidates.Count; i++)
        {
            WarpCandidate c = candidates[i];
            bool samePair = (c.npcA == before.npcA && c.npcB == before.npcB) ||
                            (c.npcA == before.npcB && c.npcB == before.npcA);
            if (!samePair) continue;
            Vector3 d = c.position - before.position;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq <= allowedSqr && sq < bestSqr) { bestSqr = sq; bestIndex = i; }
        }
        if (bestIndex < 0) return false;
        WarpCandidate selected = candidates[bestIndex];
        if (!IsSpaceFree(selected.position) || !IsReachable(origin, selected.position)) return false;
        confirmed = selected;
        return true;
    }

    private static GameObject FindNPCRoot(Transform t)
    {
        while (t != null)
        {
            if (t.CompareTag("NPC")) return t.gameObject;
            t = t.parent;
        }
        return null;
    }
}
