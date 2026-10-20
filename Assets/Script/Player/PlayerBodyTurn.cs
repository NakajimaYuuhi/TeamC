/////////////////////////////////////////
/// PlayerBodyTurn.cs
/// 2026/10/20 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/20 佐藤蒼也：プレイヤーの体の回転処理実装
/// 
/////////////////////////////////////////

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBodyTurn : MonoBehaviour
{
    [Header("回転移動速度")]
    [SerializeField] private float leanSpeed = 2.0f;

    [Header("基本回転設定")]
    private readonly Vector3 defaultRotation = new Vector3(0f, 90f, 0f);

    Rigidbody rb;
    private bool isShiftPressed;    //そのフレームにShiftキーが押されているか
    private Vector2 leanInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
      
    }

    
    void Update()
    {
        CheckInput(); //Shift + WASDの入力を受け取る,回転
    }

   　void FixedUpdate()
    {
        LeanMovement();
    }

    //入力をまとめる関数
    //Shiftキーが押されている場合はWASDの入力を受け取る,回転のための関数
    private void CheckInput()
    {
        //                キーボード接続確認   &&      左Shiftキーが押されているか
        isShiftPressed = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        if (isShiftPressed)
        {
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.wKey.isPressed) y = 1f;    //Wキーが押されている→yが1
            if (Keyboard.current.sKey.isPressed) y = -1f;   //Sキーが押されている→yが-1
            if (Keyboard.current.aKey.isPressed) x = -1f;   //Aキーが押されている→xが-1
            if (Keyboard.current.dKey.isPressed) x = 1f;    //Dキーが押されている→xが1    

            leanInput = new Vector2(x, y);
        }
        else
        {
            leanInput = Vector2.zero;
        }
    }

    //回転移動処理
    private void LeanMovement()
    {
        if (isShiftPressed && leanInput != Vector2.zero)
        {
            //回転を切り替え
            UpdateRotationOnLean(leanInput);

            //移動
            Vector3 targetVelocity = new Vector3(leanInput.x * leanSpeed, rb.linearVelocity.y, leanInput.y * leanSpeed);
            rb.linearVelocity = targetVelocity;
            return;
        }

        //押していない・動いていないとき
        transform.eulerAngles = defaultRotation;
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    //回転の切り替え
    private void UpdateRotationOnLean(Vector2 input)
    {
        float targetYAngle = 90.0f;

        if (input.y > 0) targetYAngle = 0.0f;       //W
        else if (input.y < 0) targetYAngle = 180.0f; //S

        transform.eulerAngles = new Vector3(0f, targetYAngle, 0f);
    }
}
