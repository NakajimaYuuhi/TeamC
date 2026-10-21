using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcPhysicsLodManager : MonoBehaviour
{
    [Header("距離の基準")]
    [SerializeField, InspectorName("追跡対象")]
    [Tooltip("実際に移動するプレイヤーのTransformです。ゲームフローからも設定できます。")]
    private Transform target;

    [Header("物理LODの距離（XZ平面）")]
    [SerializeField, Min(0f), InspectorName("通常動作へ戻す距離")]
    [Tooltip("この距離以内へ近づいたNPCの物理・行動を再開します。")]
    private float activationDistance = 15f;

    [SerializeField, Min(0f), InspectorName("停止する距離")]
    [Tooltip("この距離以上で停止します。通常動作へ戻す距離より大きくしてください。中間では状態を維持します。")]
    private float suspensionDistance = 20f;

    [Header("更新の分散")]
    [SerializeField, Min(0.01f), InspectorName("全NPCを確認する間隔（秒）")]
    [Tooltip("登録されたNPCを1巡する間隔の目安です。1フレームの確認数上限に達した場合は長くなります。")]
    private float checkInterval = 0.2f;

    [SerializeField, Min(1), InspectorName("1フレームの最大確認数")]
    [Tooltip("1フレームで距離判定と停止・再開を行うNPC数の上限です。")]
    private int maxChecksPerFrame = 256;

    [SerializeField, Min(0f), InspectorName("登録直後の通常動作時間（秒）")]
    [Tooltip("生成直後の落下・接地のため、登録後はこの時間が経つまで停止しません。")]
    private float initialSimulationTime = 1f;

    private struct Entry
    {
        public NPC_Controller Npc;
        public float SuspensionAllowedTime;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<NPC_Controller, int> indices = new Dictionary<NPC_Controller, int>();
    private int nextIndex;
    private float checkBudget;
    private bool hadTarget;

    private void OnEnable()
    {
        checkBudget = 0f;
        hadTarget = target != null;
        ValidateSettings();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled) ValidateSettings();
    }

    private void ValidateSettings()
    {
        if (activationDistance >= 0f && suspensionDistance > activationDistance
            && checkInterval > 0f && maxChecksPerFrame > 0 && initialSimulationTime >= 0f)
        {
            return;
        }

        Debug.LogError("NPC物理LODの設定が不正です。停止距離は復帰距離より大きくし、確認間隔・最大確認数は正の値、通常動作時間は0以上を指定してください。", this);
        enabled = false;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        checkBudget = 0f;
        hadTarget = target != null;
        if (!hadTarget) RestoreAll();
    }

    public void Register(NPC_Controller npc)
    {
        if (npc == null) throw new ArgumentNullException(nameof(npc));
        if (indices.ContainsKey(npc)) return;
        if (npc.PhysicsLodManager != null && npc.PhysicsLodManager != this)
        {
            throw new InvalidOperationException("このNPCは別の物理LOD管理スクリプトに登録されています。");
        }

        indices.Add(npc, entries.Count);
        entries.Add(new Entry { Npc = npc, SuspensionAllowedTime = Time.time + initialSimulationTime });
        npc.PhysicsLodManager = this;
    }

    public void Unregister(NPC_Controller npc)
    {
        if (ReferenceEquals(npc, null) || !indices.TryGetValue(npc, out var index)) return;
        if (npc != null)
        {
            npc.SetPhysicsLodSuspended(false);
            npc.PhysicsLodManager = null;
        }

        RemoveAt(index);
    }

    private void Update()
    {
        UpdateLod(Time.deltaTime, Time.time);
    }

    private void UpdateLod(float deltaTime, float now)
    {
        if (target == null)
        {
            if (hadTarget) RestoreAll();
            hadTarget = false;
            return;
        }

        hadTarget = true;
        if (entries.Count == 0) return;

        checkBudget = Mathf.Min(checkBudget + entries.Count * deltaTime / checkInterval, entries.Count);
        var checks = Mathf.Min(Mathf.FloorToInt(checkBudget), maxChecksPerFrame);
        checkBudget -= checks;
        var targetPosition = target.position;
        var activationSquared = activationDistance * activationDistance;
        var suspensionSquared = suspensionDistance * suspensionDistance;

        for (var count = 0; count < checks && entries.Count > 0; count++)
        {
            if (nextIndex >= entries.Count) nextIndex = 0;
            var entry = entries[nextIndex];
            var npc = entry.Npc;
            if (npc == null)
            {
                RemoveAt(nextIndex);
                continue;
            }

            nextIndex++;
            if (!npc.gameObject.activeInHierarchy || now < entry.SuspensionAllowedTime) continue;
            var position = npc.transform.position;
            var x = position.x - targetPosition.x;
            var z = position.z - targetPosition.z;
            var distanceSquared = x * x + z * z;
            if (npc.IsPhysicsLodSuspended)
            {
                if (distanceSquared <= activationSquared) npc.SetPhysicsLodSuspended(false);
            }
            else if (distanceSquared >= suspensionSquared)
            {
                npc.SetPhysicsLodSuspended(true);
            }
        }
    }

    private void RemoveAt(int index)
    {
        indices.Remove(entries[index].Npc);
        var lastIndex = entries.Count - 1;
        if (index != lastIndex)
        {
            entries[index] = entries[lastIndex];
            indices[entries[index].Npc] = index;
        }

        entries.RemoveAt(lastIndex);
        if (nextIndex >= entries.Count) nextIndex = 0;
    }

    private void RestoreAll()
    {
        foreach (var entry in entries)
        {
            if (entry.Npc != null) entry.Npc.SetPhysicsLodSuspended(false);
        }

        checkBudget = 0f;
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        foreach (var entry in entries)
        {
            if (entry.Npc != null)
            {
                entry.Npc.SetPhysicsLodSuspended(false);
                entry.Npc.PhysicsLodManager = null;
            }
        }

        entries.Clear();
        indices.Clear();
    }
}
