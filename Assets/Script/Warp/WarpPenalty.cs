/////////////////////////////////////////
/// WarpPenalty.cs
/// ワープ失敗イベントを受け取ってペナルティを与える
///
/// ・どの失敗理由でペナルティを与えるかを Inspector で選べる
/// ・角度のずれが大きいほど迷惑度が多く上がる
/// ・少しの間動けなくする（ひるみ）
/////////////////////////////////////////

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(PlayerWarp))]
public class WarpPenalty : MonoBehaviour
{
    [Header("ペナルティを与える失敗理由")]
    [SerializeField] private bool penalizeAngleMismatch = true;
    [SerializeField] private bool penalizeNoDirection = false;
    [SerializeField] private bool penalizeNoGap = false;
    [SerializeField] private bool penalizeBlocked = false;

    [Header("迷惑度")]
    [SerializeField, Min(0f), Tooltip("失敗1回で上がる迷惑度")]
    private float nuisanceBase = 5f;
    [SerializeField, Min(0f), Tooltip("許容角度を1度超えるごとに追加される迷惑度")]
    private float nuisancePerDegree = 0.1f;

    [Header("ひるみ")]
    [SerializeField, Min(0f), Tooltip("動けなくなる時間（秒）")]
    private float stunSeconds = 0.6f;

    [Header("イベント（Inspector用）")]
    [SerializeField] private UnityEvent<WarpSearchResult> onPenalty = new UnityEvent<WarpSearchResult>();

    //コードから購読する用（結果, 上がった迷惑度）
    public event Action<WarpSearchResult, float> PenaltyApplied;

    public bool IsStunned { get; private set; }

    private PlayerWarp warp;
    private PlayerMove move;
    private Rigidbody rb;
    private Coroutine stunRoutine;

    private void Awake()
    {
        warp = GetComponent<PlayerWarp>();
        move = GetComponent<PlayerMove>();
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        warp.WarpFailed += HandleWarpFailed;
    }

    private void OnDisable()
    {
        warp.WarpFailed -= HandleWarpFailed;
        if (stunRoutine != null) EndStun();
    }

    private void HandleWarpFailed(WarpSearchResult result)
    {
        if (!ShouldPenalize(result.reason)) return;

        //迷惑度
        float add = nuisanceBase;
        if (result.reason == WarpFailReason.AngleMismatch && result.closestAngle < 360f)
        {
            add += Mathf.Max(0f, result.closestAngle - warp.Settings.angleTolerance) * nuisancePerDegree;
        }
        if (move != null) move.SetPlayerNuisance(move.GetPlayerNuisance() + add);

        //ひるみ
        if (stunSeconds > 0f)
        {
            warp.AddCooldown(stunSeconds);
            if (stunRoutine != null) StopCoroutine(stunRoutine);
            stunRoutine = StartCoroutine(StunRoutine());
        }

        PenaltyApplied?.Invoke(result, add);
        onPenalty.Invoke(result);
    }

    private bool ShouldPenalize(WarpFailReason reason)
    {
        switch (reason)
        {
            case WarpFailReason.AngleMismatch: return penalizeAngleMismatch;
            case WarpFailReason.NoDirection: return penalizeNoDirection;
            case WarpFailReason.NoGap: return penalizeNoGap;
            case WarpFailReason.Blocked: return penalizeBlocked;
            default: return false;
        }
    }

    private IEnumerator StunRoutine()
    {
        IsStunned = true;
        if (move != null) move.enabled = false;
        if (rb != null) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        yield return new WaitForSeconds(stunSeconds);

        EndStun();
    }

    private void EndStun()
    {
        if (move != null) move.enabled = true;
        IsStunned = false;
        stunRoutine = null;
    }
}
