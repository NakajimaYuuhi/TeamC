using UnityEngine;
using UnityEngine.InputSystem;

public class tmpPlayer_forNPC : MonoBehaviour
{
    // 移動速度
    float moveSpeed = 5f;

    private Vector2 _moveInput;


    // インスペクターからイベントとして登録する関数
    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"Move Input: {_moveInput}");
        // 移動処理
        transform.Translate(new Vector3(_moveInput.x * moveSpeed, 0, _moveInput.y * moveSpeed) * Time.deltaTime);
    }
}
