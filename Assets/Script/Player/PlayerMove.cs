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
    [Header("プレイヤーの移動速度")]
    [SerializeField] private float moveSpeed = 5.0f;//プレイヤー移動速度


    Rigidbody rb;
    private Vector2 moveInput; //移動の入力を格納する変数


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    void FixedUpdate()
    {
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
        Vector3 targetVelocity = new Vector3(moveInput.x * moveSpeed, rb.linearVelocity.y, moveInput.y * moveSpeed);
        rb.linearVelocity = targetVelocity;
    }
}
