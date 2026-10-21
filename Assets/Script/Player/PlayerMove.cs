/////////////////////////////////////////
/// PlayerMove.cs
/// 2026/10/20 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/20 佐藤蒼也：プレイヤー移動処理実装
/// 2026/10/20 佐藤蒼也：Shift中の横歩き処理実装
/// 2026/10/21 佐藤蒼也：PlayerBodyTurnの処理を統合
/// 2026/10/21 佐藤蒼也：進行方向への回転と左向きへの回転の速度を分離
/// 2026/10/21 佐藤蒼也：横歩き中は常に進行方向の左を向くよう変更
/// 
/////////////////////////////////////////




using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("プレイヤーのデータ")]
    [SerializeField] private PlayerData playerData;



    [Header("横歩きの設定,通常時に対する速度の割合")]
    [SerializeField, Range(0.1f, 1f)] private float sideStepSpeedRate = 0.5f; //通常時に対する速度の割合

    Rigidbody rb;
    private Vector2 moveInput; //移動の入力を格納する変数
    private float moveSpeed; //移動速度を格納する変数
    private float turnSpeed; //進行方向へ体を向ける回転速度
    private float faceLeftTurnSpeed = 5f; //Shift中に体の正面を進行方向の左に向ける回転速度
    private Vector3 playerSize; //プレイヤーのサイズを格納する変数
    private CameraController cameraController; //移動方向の基準にするカメラ
    private float modelYawOffset = 90f; //モデルの正面のずれ補正

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        moveSpeed = playerData.MoveSpeed; //プレイヤーの移動速度を取得
        playerSize = playerData.PlayerSize; //プレイヤーのサイズを取得
        turnSpeed = playerData.TurnSpeed; //進行方向への回転速度を取得

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
        //Shiftが押されているか
        bool isSideStep = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Move(isSideStep);//プレイヤー移動処理
    }

    //移動の入力を受け取る関数 WASD
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();//入力を取得 
    }

    //移動処理
    private void Move(bool isSideStep)
    {
        if (cameraController == null) return;

        moveSpeed = playerData.MoveSpeed;

        //横歩き中は速度を落とす
        if (isSideStep)
        {
            moveSpeed *= sideStepSpeedRate;
        }

        //カメラの回転角度から水平な前方向・右方向を取得
        Vector3 forward = cameraController.FlatForward;
        Vector3 right = cameraController.FlatRight;

        //W/Sでカメラの奥・手前、A/Dで画面の左右に移動
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        Vector3 horizontalVelocity = moveDirection * moveSpeed;

        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

        //入力がないときは向きを変えない
        if (moveDirection.sqrMagnitude <= 0.01f) return;

        if (isSideStep)
        {
            //横歩き：押したキーの方向に進み、体の正面は進行方向の左に向ける 上と進行方向の外積で左側がもとまる
            Vector3 leftOfMoveDirection = Vector3.Cross(moveDirection, Vector3.up);
            RotateToward(leftOfMoveDirection, faceLeftTurnSpeed);
        }
        else
        {
            //通常移動：進行方向へ体を向ける
            RotateToward(moveDirection, turnSpeed);
        }
    }

    //体の正面を指定した方向へ、指定した速さで滑らかに向ける
    private void RotateToward(Vector3 direction, float speed)
    {
        //向けたい方向にモデルの正面のずれ分を足した向きを目標にする
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, speed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
    }
}