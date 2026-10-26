using UnityEngine;

//=====================================================
//
//  FlowEmitter.cs
//  流れ場に流れを追加するためのコンポーネント
//  
//  フレームのポジションから移動量を計算
//  2次元のベクトル場に書き込む
//
//=====================================================
public class FlowEmitter : MonoBehaviour
{
    [SerializeField] FlowField field;
    [SerializeField] float radius = 2f;
    [SerializeField] float gain = 3f;

    Vector3 prevPos;

    void Start()
    {
        // 過去座標の初期化
        prevPos = transform.position;
    }

    void Update()
    {
        // --- 移動量の計算 ---
        Vector3 delta = transform.position - prevPos;
        
        // --- 過去座標の更新 ---
        prevPos = transform.position;

        // --- 2次元ベクトルに変換 ---

        // XZ平面上の移動量を2次元ベクトルに変換
        Vector2 move = new Vector2(delta.x, delta.z);
        // 移動量がほぼゼロなら何もしない
        if (move.sqrMagnitude < 1e-6f) return;

        // 移動量ベースなのでフレームレートが変わっても総量はほぼ同じ
        field.AddFlow(transform.position, move * gain, radius);
    }
}

