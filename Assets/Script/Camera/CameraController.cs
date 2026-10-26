/////////////////////////////////////////
/// CameraController.cs
/// 2026/10/20 作者：佐藤蒼也
/// ログ
/// 2026/10/20 佐藤蒼也：カメラ追従・拡縮処理実装
/// 2026/10/20 佐藤蒼也：マウスでの視点回転処理実装
/// 2026/10/22 佐藤蒼也：1人称視点実装
/// 
/////////////////////////////////////////

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;


public class CameraController : MonoBehaviour
{
    [Header("1人称モード:チェックを入れると1人称モードになる")]
    [SerializeField, Tooltip("チェックを入れると1人称モード")] private bool FPSMode = false;

    [Header("カメラの位置調整")]
    [SerializeField, Tooltip("プレイヤーからの距離")] 
    private Vector3 offset = new Vector3(0f, 5f, -10f); //プレイヤーからの距離
    [SerializeField, Tooltip("追従の滑らかさ")] 
    private float followSpeed = 10f; //追従の滑らかさ

    [Header("カメラのズーム設定")]
    [SerializeField, Tooltip("カメラの拡縮速度")] 
    private float zoomSpeed = 2.0f;  //カメラの拡縮速度
    [SerializeField, Tooltip("最も近づける距離")] 
    private float minZoom = 3f;      //最も近づける距離
    [SerializeField, Tooltip("最も遠ざけられる距離")] 
    private float maxZoom = 15f;     //最も遠ざけられる距離

    [Header("カメラの回転設定")]
    [SerializeField, Tooltip("マウス感度")] 
    private float mouseSensitivity = 0.15f; //マウス感度
    [SerializeField, Tooltip("見下ろし角度の下限")] 
    private float minPitch = -10f;          //見下ろし角度の下限
    [SerializeField, Tooltip("見下ろし角度の上限")] 
    private float maxPitch = 80f;           //見下ろし角度の上限
    [SerializeField, Tooltip("右クリック中のみ回転するか")] 
    private bool rotateOnlyWhileRightClick = true; //右クリック中のみ回転するか

    [Header("1人称モードの設定")]
    [SerializeField, Tooltip("プレイヤーの位置から目の位置までのずれ")] 
    private Vector3 fpsEyeOffset = new Vector3(0f, 1.5f, 0f); //プレイヤーの位置から目の位置までのずれ
    [SerializeField, Tooltip("見上げる限界")] 
    private float fpsMinPitch = -80f; //見上げる限界
    [SerializeField, Tooltip("見下ろす限界")] 
    private float fpsMaxPitch = 80f;  //見下ろす限界


    private float currentZoomDistance; //現在のズーム距離
    private Transform target; //追従先
    private Vector3 smoothedTargetPosition; //追従させた注視点
    private float yaw;   //左右の回転角度
    private float pitch; //上下の回転角度

    private Renderer[] playerRenderers; //1人称のときに見た目を消すプレイヤーの描画
    private bool appliedFPSMode;        //最後に反映したモード
    private PlayerInput playerInput;    //PlayerInput取得用
    private InputAction cameraModeAction; //カメラモード切り替えのアクション

    //プレイヤーの移動方向の基準にする水平な前方向・右方向
    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

    public float CurrentZoomDistance => currentZoomDistance;
    public void SetZoomDistance(float dis)
    {
        currentZoomDistance = dis;
    }

    void Start()
    {
        //Playerタグのついたオブジェクトを探してカメラの追従対象に設定
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            smoothedTargetPosition = target.position;

            //プレイヤーを取得しておく
            playerRenderers = target.GetComponentsInChildren<Renderer>();

            //プレイヤーについているインプットシステムを取得
            playerInput = playerObj.GetComponentInParent<PlayerInput>();
            
            //カメラモード切り替えのアクションに、押されたときの処理を登録する
            if (playerInput != null)
            {
                cameraModeAction = playerInput.actions.FindAction("CameraModeChange");
                if (cameraModeAction != null)
                {
                    cameraModeAction.performed += OnCameraModeChange;
                }
                else
                {
                    Debug.LogError("CameraModeChangeアクションがない");
                }
            }
        }
        else
        {
            Debug.LogError("Playerタグのついたオブジェクトがない");
        }

        //カメラとプレイヤーの初期状態の距離を取得
        currentZoomDistance = Mathf.Abs(offset.z);

        //offsetの向きから初期の回転角度を求める
        Vector3 dir = offset.normalized;
        yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
        pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;

        //開始時のモードを反映
        ApplyMode();
    }


    void LateUpdate()
    {
        if (target == null) return;

        
        if (FPSMode != appliedFPSMode)
        {
            ApplyMode();　//Inspectorでモードが切り替えられたら反映する
        }

        HandleRotation();//カメラ回転処理

        if (FPSMode)
        {
            UpdateFirstPerson();
            //1人称モードのときはズーム機能いれない
        }
        else
        {
            HandleZoom();//カメラ拡縮処理
            UpdateThirdPerson();
        }
    }

    void Update()
    {

    }

    //3人称カメラ
    private void UpdateThirdPerson()
    {
        //注視点に追従させる
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, target.position, t);

        //回転角度からカメラの方向を計算し、ズーム距離だけ離した位置に置く
        Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        transform.position = smoothedTargetPosition + direction * currentZoomDistance;
        transform.LookAt(smoothedTargetPosition + Vector3.up * 1.5f);
    }

    //1人称カメラ
    private void UpdateFirstPerson()
    {
        //目の位置にカメラを置く
        transform.position = target.position + fpsEyeOffset;

        //マウスで決めた角度をそのままカメラの向きにする
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    //モードの切り替えを反映する
    private void ApplyMode()
    {
        appliedFPSMode = FPSMode;//true:1人称 false:3人称

        
        if (playerRenderers != null)
        {
            //1人称のときは自分の体が視界をふさがないように見た目を消す
            foreach (Renderer r in playerRenderers)
            {
                r.shadowCastingMode = FPSMode ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                //ShadowOnly:本体を消して影だけ描画 On:本体も描画
            }
        }

        if (FPSMode)
        {
            //1人称は常にマウスで視点を動かすので、カーソルを固定して隠す
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;

            //見下ろし角度を1人称の範囲に収める
            pitch = Mathf.Clamp(pitch, fpsMinPitch, fpsMaxPitch);
        }
        else
        {
            //3人称に戻したときに、カメラが遠くから飛んでこないよう注視点をプレイヤーに合わせる
            smoothedTargetPosition = target.position;

            //見下ろし角度を3人称の範囲に収める
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    //拡縮処理 マウスホイール
    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scrollValue = Mouse.current.scroll.y.ReadValue();

        if (scrollValue != 0f)
        {
            currentZoomDistance -= scrollValue * zoomSpeed * 0.01f;
            currentZoomDistance = Mathf.Clamp(currentZoomDistance, minZoom, maxZoom);
        }
    }

    //視点回転処理 マウス移動
    private void HandleRotation()
    {
        if (Mouse.current == null) return;

        //3人称で、右クリック中のみ回転する設定なら、押していないときはカーソルを戻して終了
        if (!FPSMode && rotateOnlyWhileRightClick && !Mouse.current.rightButton.isPressed)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            return;
        }

        //回転中はカーソルを固定して非表示にする
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;   //左右
        pitch -= mouseDelta.y * mouseSensitivity; //上下

        //モードに合わせて上下の角度を制限する
        if (FPSMode)
        {
            pitch = Mathf.Clamp(pitch, fpsMinPitch, fpsMaxPitch);
        }
        else
        {
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }
        //カメラモードチェンジ Vキー
    void OnCameraModeChange(InputAction.CallbackContext context)
    {
        FPSMode = !FPSMode;
    }

    //カメラが消えるときに登録を解除する
    void OnDestroy()
    {
        if (cameraModeAction != null)
        {
            cameraModeAction.performed -= OnCameraModeChange;
        }
    }
}