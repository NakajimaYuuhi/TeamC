/////////////////////////////////////////
/// PlayerWarp.cs
/// NPCの隙間へワープする処理（PlayerMoveとは独立したコンポーネント）
///
/// ・方向は移動入力（OnMove）と同じものを使う
/// ・ボタンを押した瞬間に判定 → 成功ならワープ、失敗なら失敗イベント
/// ・ワープ候補位置に半透明のプレイヤー（WarpGhost）を表示する
/// ・固定範囲やグリッドのGizmos表示は WarpGridDebugDrawer を通して描画
/////////////////////////////////////////

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public enum WarpGamepadButton
{
    ButtonWest,   // □ / X
    ButtonNorth,  // △ / Y
    ButtonSouth,  // × / A
    ButtonEast,   // ○ / B
    LeftShoulder, // L1 / LB
    RightShoulder // R1 / RB
}

[RequireComponent(typeof(Rigidbody))]
public class PlayerWarp : MonoBehaviour
{
    [Header("隙間探しの設定")]
    [SerializeField] private WarpGapSettings settings = new WarpGapSettings();

    [Header("入力（ワープボタン）")]
    [SerializeField] private Key warpKey = Key.E;
    [SerializeField] private WarpGamepadButton warpGamepadButton = WarpGamepadButton.ButtonWest;

    [Header("ワープ")]
    [SerializeField, Min(0f), Tooltip("成功後のクールダウン（秒）")]
    private float cooldown = 0.8f;
    [SerializeField, Min(0f), Tooltip("失敗後のクールダウン（秒）連打防止")]
    private float failCooldown = 0.3f;
    [SerializeField, Tooltip("判定に使う体のコライダー（空なら自動取得）")]
    private Collider bodyCollider;
    [SerializeField, Min(0f), Tooltip("判定カプセルを床から浮かせる量")]
    private float groundClearance = 0.05f;

    [Header("半透明プレイヤー")]
    [SerializeField] private bool showGhost = true;
    [SerializeField] private Material ghostMaterial;
    [SerializeField, Min(0f), Tooltip("候補を作り直す間隔（秒）")]
    private float previewInterval = 0.05f;
    [SerializeField, Min(0f)] private float ghostFollowSpeed = 25f;

    [Header("イベント（Inspector用）")]
    [SerializeField] private UnityEvent<WarpSearchResult> onWarpSucceeded = new UnityEvent<WarpSearchResult>();
    [SerializeField] private UnityEvent<WarpSearchResult> onWarpFailed = new UnityEvent<WarpSearchResult>();

    //コードから購読する用
    public event Action<WarpSearchResult> WarpSucceeded;
    public event Action<WarpSearchResult> WarpFailed;

    public WarpSearchResult LastResult { get; private set; }
    public WarpSearchResult PreviewResult { get; private set; }
    public float CooldownRemaining => cooldownTimer;
    public WarpGapSettings Settings => settings;
    public IReadOnlyList<WarpCandidate> Candidates => finder != null ? finder.Candidates : null;
    public Key WarpKey => warpKey;
    public WarpGamepadButton WarpGamepadButton => warpGamepadButton;

    private Rigidbody rb;
    private CameraController cameraController;
    private WarpGapFinder finder;
    private WarpGhost ghost;

    private Vector2 moveInput;
    private bool warpRequested;
    private float cooldownTimer;
    private float previewTimer;
    private bool hasGhostTarget;
    private Vector3 ghostTarget;

    private float playerRadius = 0.3f;
    private Vector3 capsuleOffset0;
    private Vector3 capsuleOffset1;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        //レイヤー未設定なら自動設定
        if (settings.npcMask == 0) settings.npcMask = LayerMask.GetMask("NPC");
        if (settings.obstacleMask == 0) settings.obstacleMask = LayerMask.GetMask("Default");

        if (bodyCollider == null) bodyCollider = FindBodyCollider();
        if (bodyCollider == null) Debug.LogError("PlayerWarp: 体のコライダーが見つかりません", this);

        CalculatePlayerShape();
        finder = new WarpGapFinder(settings, playerRadius, capsuleOffset0, capsuleOffset1);
        LastResult = new WarpSearchResult { reason = WarpFailReason.None };
    }

    private void Start()
    {
        FindCamera();
        if (showGhost) ghost = WarpGhost.Create(transform, ghostMaterial);
    }

    private void OnDisable()
    {
        warpRequested = false;
        HideGhost();
    }

    private void OnDestroy()
    {
        if (ghost != null) Destroy(ghost.gameObject);
    }

    private void Update()
    {
        if (cooldownTimer > 0f) cooldownTimer = Mathf.Max(0f, cooldownTimer - Time.deltaTime);

        if (WasWarpPressed()) warpRequested = true;

        UpdatePreview();
    }

    private void FixedUpdate()
    {
        if (!warpRequested) return;
        warpRequested = false;
        TryWarp();
    }

    //PlayerInput（Send Messages）から届く移動入力。PlayerMoveにも同じものが届く
    private void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    //================ ワープ ================
    private void TryWarp()
    {
        Vector3 origin = rb.position;
        Vector3 aim = GetAim();
        WarpSearchResult result = new WarpSearchResult { closestAngle = float.MaxValue };

        if (cooldownTimer > 0f)
        {
            result.reason = WarpFailReason.Cooldown;
        }
        else if (aim.sqrMagnitude < 0.01f)
        {
            result.reason = WarpFailReason.NoDirection;
        }
        else
        {
            //直前のプレビュー候補があり、NPCペアが維持されていれば確定チェックを試みる
            if (PreviewResult.success && finder.TryConfirmCandidate(origin, PreviewResult.best, out WarpCandidate confirmed))
            {
                result.success = true;
                result.reason = WarpFailReason.None;
                result.best = confirmed;
            }
            else
            {
                //なければ最新状態で再探索して評価
                finder.RebuildCandidates(origin);
                result = finder.Evaluate(origin, aim, hasGhostTarget ? ghostTarget : (Vector3?)null);
            }

            //念のため実行直前の空間チェック
            if (result.success && !finder.IsSpaceFree(result.best.position))
            {
                result.success = false;
                result.reason = WarpFailReason.Blocked;
            }
        }

        LastResult = result;

        if (result.success)
        {
            Teleport(result.best.position);
            cooldownTimer = cooldown;
            HideGhost();
            WarpSucceeded?.Invoke(result);
            onWarpSucceeded.Invoke(result);
        }
        else
        {
            if (result.reason != WarpFailReason.Cooldown) cooldownTimer = Mathf.Max(cooldownTimer, failCooldown);
            WarpFailed?.Invoke(result);
            onWarpFailed.Invoke(result);
        }
    }

    private void Teleport(Vector3 target)
    {
        Vector3 p = new Vector3(target.x, rb.position.y, target.z);
        rb.position = p;
        transform.position = p;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    //外部（ペナルティなど）からワープを封じる
    public void AddCooldown(float seconds)
    {
        cooldownTimer = Mathf.Max(cooldownTimer, seconds);
    }

    //================ 半透明プレイヤー ================
    private void UpdatePreview()
    {
        if (ghost == null || finder == null) return;

        previewTimer -= Time.deltaTime;
        if (previewTimer <= 0f)
        {
            previewTimer = previewInterval;
            finder.RebuildCandidates(rb.position);
        }

        WarpSearchResult preview = finder.Evaluate(rb.position, GetAim(), hasGhostTarget ? ghostTarget : (Vector3?)null);
        PreviewResult = preview;

        bool show = preview.success && cooldownTimer <= 0f;
        if (show)
        {
            //候補が大きく変わったときはなめらかに追わず瞬時に移す
            bool snap = !hasGhostTarget || (preview.best.position - ghostTarget).sqrMagnitude > settings.stickyRadius * settings.stickyRadius * 4f;
            ghostTarget = preview.best.position;
            Vector3 pos = new Vector3(ghostTarget.x, transform.position.y, ghostTarget.z);
            ghost.Show(pos, transform.rotation, transform.lossyScale, snap, ghostFollowSpeed);
            hasGhostTarget = true;
        }
        else
        {
            HideGhost();
        }
    }

    private void HideGhost()
    {
        if (ghost != null) ghost.Hide();
        hasGhostTarget = false;
    }

    //================ 形状計算・補助 ================
    private void CalculatePlayerShape()
    {
        if (bodyCollider == null)
        {
            playerRadius = 0.3f;
            capsuleOffset0 = Vector3.up * 0.3f;
            capsuleOffset1 = Vector3.up * 1.5f;
            return;
        }

        Bounds b = bodyCollider.bounds;
        Vector3 ls = bodyCollider.transform.lossyScale;

        if (bodyCollider is BoxCollider box)
        {
            Vector3 size = Vector3.Scale(box.size, ls);
            playerRadius = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)) * 0.5f;
        }
        else if (bodyCollider is CapsuleCollider cap)
        {
            playerRadius = cap.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.z));
        }
        else
        {
            playerRadius = Mathf.Max(b.extents.x, b.extents.z);
        }

        float centerY = b.center.y - transform.position.y;
        float halfH = b.extents.y;
        float bottom = centerY - halfH + playerRadius + groundClearance;
        float top = centerY + halfH - playerRadius;
        if (top < bottom) top = bottom;

        capsuleOffset0 = Vector3.up * bottom;
        capsuleOffset1 = Vector3.up * top;
    }

    //PlayerMove.Move と同じくカメラ基準で入力方向を求める
    private Vector3 GetAim()
    {
        if (cameraController == null) FindCamera();

        Vector3 forward = cameraController != null ? cameraController.FlatForward : Vector3.forward;
        Vector3 right = cameraController != null ? cameraController.FlatRight : Vector3.right;
        Vector3 aim = forward * moveInput.y + right * moveInput.x;
        aim.y = 0f;
        return aim;
    }

    private void FindCamera()
    {
        if (Camera.main != null) cameraController = Camera.main.GetComponent<CameraController>();
    }

    private bool WasWarpPressed()
    {
        bool pressed = false;

        Keyboard kb = Keyboard.current;
        if (kb != null && warpKey != Key.None) pressed |= kb[warpKey].wasPressedThisFrame;

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            switch (warpGamepadButton)
            {
                case WarpGamepadButton.ButtonWest:
                    pressed |= gp.buttonWest.wasPressedThisFrame;
                    break;
                case WarpGamepadButton.ButtonNorth:
                    pressed |= gp.buttonNorth.wasPressedThisFrame;
                    break;
                case WarpGamepadButton.ButtonSouth:
                    pressed |= gp.buttonSouth.wasPressedThisFrame;
                    break;
                case WarpGamepadButton.ButtonEast:
                    pressed |= gp.buttonEast.wasPressedThisFrame;
                    break;
                case WarpGamepadButton.LeftShoulder:
                    pressed |= gp.leftShoulder.wasPressedThisFrame;
                    break;
                case WarpGamepadButton.RightShoulder:
                    pressed |= gp.rightShoulder.wasPressedThisFrame;
                    break;
            }
        }

        return pressed;
    }

    private Collider FindBodyCollider()
    {
        foreach (Collider c in GetComponents<Collider>())
        {
            if (c.enabled && !c.isTrigger) return c;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        //ユーザー作成のグリッド描画ヘルパーを呼び出し
        WarpGridDebugDrawer.Draw(settings);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || finder == null) return;

        Vector3 origin = transform.position;

        //入力方向と許容角度
        Vector3 aim = GetAim();
        if (aim.sqrMagnitude > 0.01f)
        {
            aim.Normalize();
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin, origin + aim * settings.maxDistance);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, settings.angleTolerance, 0f) * aim * settings.maxDistance);
            Gizmos.DrawLine(origin, origin + Quaternion.Euler(0f, -settings.angleTolerance, 0f) * aim * settings.maxDistance);
        }

        //候補（緑:角度内 / 灰:角度外）
        foreach (WarpCandidate c in finder.Candidates)
        {
            bool ok = c.angle >= 0f && c.angle <= settings.angleTolerance;
            Gizmos.color = ok ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(c.position, finder.PlayerRadius);
        }
    }
}
