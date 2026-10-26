using UnityEngine;

// ベクトルの方向にただまっすぐ動く
public class tmp_NPC_linearMove : MonoBehaviour
{

    // 移動方向
    [SerializeField] Vector2 moveDirection = new Vector2(1, 0);

    // 移動速度
    [SerializeField] float moveSpeed = 1f;

    // Rigidbodyコンポーネントへの参照
    Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Rigidbodyコンポーネントを取得
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // 移動方向を正規化して速度を掛ける
        Vector3 velocity = new Vector3(moveDirection.x, 0, moveDirection.y).normalized * moveSpeed;

        // Rigidbodyの速度を設定
        rb.linearVelocity = velocity;
    }
}
