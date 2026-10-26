using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.GraphicsBuffer;

public class tmpPlayer_forNPC : MonoBehaviour
{
    // 移動速度
    [SerializeField] float moveSpeed = 5f;

    private Vector2 _moveInput;

    Rigidbody rb;


    // インスペクターからイベントとして登録する関数
    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"Move Input: {_moveInput}");
        // 移動処理
        //transform.Translate(new Vector3(_moveInput.x * moveSpeed, 0, _moveInput.y * moveSpeed) * Time.deltaTime);
        transform.position = new Vector3(transform.position.x + _moveInput.x * moveSpeed * Time.deltaTime, 0, transform.position.z + _moveInput.y * moveSpeed * Time.deltaTime); // 高さを固定
        //rb.linearVelocity = new Vector3(_moveInput.x * moveSpeed, 0, _moveInput.y * moveSpeed); // Rigidbodyの速度を設定


        Vector3 moveDirection = new Vector3(_moveInput.x, 0, _moveInput.y);
        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.forward = moveDirection.normalized;
        }
    }
}
