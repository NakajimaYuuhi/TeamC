/////////////////////////////////////////
/// CameraController.cs
/// 2026/10/20 作者：佐藤蒼也
/// ログ
/// 2026/10/20 佐藤蒼也：カメラ追従・拡縮処理実装
/// 2026/10/20 佐藤蒼也：マウスでの視点回転処理実装
/// 
/////////////////////////////////////////

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;


public class CameraController : MonoBehaviour
{
    [Header("カメラの位置調整")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -10f); //プレイヤーからの距離
    [SerializeField] private float followSpeed = 10f; //追従の滑らかさ

    [Header("カメラの拡縮設定")]
    [SerializeField] private float zoomSpeed = 2.0f;  //カメラの拡縮速度
    [SerializeField] private float minZoom = 3f;      //最も近づける距離
    [SerializeField] private float maxZoom = 15f;     //最も遠ざけられる距離

    [Header("カメラの回転設定")]
    [SerializeField] private float mouseSensitivity = 0.15f; //マウス感度
    [SerializeField] private float minPitch = -10f;          //見下ろし角度の下限
    [SerializeField] private float maxPitch = 80f;           //見下ろし角度の上限
    [SerializeField] private bool rotateOnlyWhileRightClick = true; //右クリック中のみ回転するか


    private float currentZoomDistance; //現在のズーム距離
    private Transform target; //追従先
    private Vector3 smoothedTargetPosition; //追従させた注視点
    private float yaw;   //左右の回転角度
    private float pitch; //上下の回転角度

    //プレイヤーの移動方向の基準にする水平な前方向・右方向（親の回転の影響を受けない）
    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    public Vector3 FlatRight => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

    void Start()
    {
        //Playerタグのついたオブジェクトを探してカメラの追従対象に設定
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            smoothedTargetPosition = target.position;
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
    }


    void LateUpdate()
    {
        if (target == null) return;

        HandleZoom();
        HandleRotation();

        //注視点に追従させる
        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, target.position, t);

        //回転角度からカメラの方向を計算し、ズーム距離だけ離した位置に置く
        Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        transform.position = smoothedTargetPosition + direction * currentZoomDistance;
        transform.LookAt(smoothedTargetPosition + Vector3.up * 1.5f);
    }

    void Update()
    {

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

        //右クリック中のみ回転する設定なら、押していないときはカーソルを戻して終了
        if (rotateOnlyWhileRightClick && !Mouse.current.rightButton.isPressed)
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
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }
}