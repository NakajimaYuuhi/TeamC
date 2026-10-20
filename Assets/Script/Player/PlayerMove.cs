/////////////////////////////////////////
/// PlayerMove.cs
/// 2026/10/20 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/20 佐藤蒼也：プレイヤー移動処理実装
/// 
/////////////////////////////////////////




using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("プレイヤーのデータ")]
    [SerializeField] private PlayerData playerData;

    [Header("向きの設定")]
    [SerializeField] private float turnSpeed = 10f; //進行方向へ向きを変える速さ

    Rigidbody rb;
    private Vector2 moveInput; //移動の入力を格納する変数
    private float moveSpeed; //移動速度を格納する変数
    private Vector3 playerSize; //プレイヤーのサイズを格納する変数
    private CameraController cameraController; //移動方向の基準にするカメラ


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        moveSpeed = playerData.MoveSpeed; //プレイヤーの移動速度を取得
        playerSize = playerData.PlayerSize; //プレイヤーのサイズを取得

        //親オブジェクトのサイズを設定
        if (transform.parent != null)
        {
            transform.parent.localScale = playerSize;
        }
        else
        {
            transform.localScale = playerSize;
        }

        //MainCameraタグのついたカメラからCameraControllerを取得
        if (Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }

        if (cameraController == null)
        {
            Debug.LogError("CameraControllerのついたMainCameraがない");
        }
    }

    void Update()
    {

    }

    void FixedUpdate()
    {
        //Shift中はPlayerBodyTurnに任せる
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) return;

        Move();
    }

    //移動の入力を受け取る関数 WASD
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();//入力を取得 
    }

    //移動処理
    private void Move()
    {
        if (cameraController == null) return;

        moveSpeed = playerData.MoveSpeed;

        //カメラの回転角度から水平な前方向・右方向を取得（親の回転の影響を受けない）
        Vector3 forward = cameraController.FlatForward;
        Vector3 right = cameraController.FlatRight;

        //W/Sでカメラの奥・手前、A/Dで画面の左右に移動
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        Vector3 horizontalVelocity = moveDirection * moveSpeed;

        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

        //入力があるときは進行方向へ滑らかに向きを変える
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }
    }
}