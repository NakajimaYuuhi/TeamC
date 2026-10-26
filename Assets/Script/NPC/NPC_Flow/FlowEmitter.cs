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

/// <summary>
/// 動いた方向に流れを作る。プレイヤーにもNPCにも付けられる。
///  - 既にある流れに沿って動いていると、書き込みが増幅される
///  - 流れに逆らって動いていると、書き込みが弱まる(道を打ち消さないように)
/// </summary>
public class FlowEmitter : MonoBehaviour
{
    [SerializeField] FlowField field;
    [SerializeField] float radius = 2f;
    [SerializeField] float gain = 3f;

    [Header("増幅")]
    [Tooltip("流れに完全に沿っているとき、書き込みが (1 + amplify) 倍になる")]
    [SerializeField, Min(0f)] float amplify = 2f;

    [Header("逆流")]
    [Tooltip("逆流にどれだけ敏感に書き込みを止めるか。3なら、最大の1/3程度の逆流で書き込み0")]
    [SerializeField, Min(0f)] float counterSensitivity = 3f;

    [Header("Debug")]
    [SerializeField] bool showDebug = false;

    Vector3 prevPos;
    float lastAlign;
    float lastScale = 1f;

    void Start()
    {
        if (field == null) field = FindAnyObjectByType<FlowField>();
        prevPos = transform.position;
    }

    void Update()
    {
        Vector3 delta = transform.position - prevPos;
        prevPos = transform.position;

        Vector2 move = new Vector2(delta.x, delta.z);   // 1フレームの移動量
        if (move.sqrMagnitude < 1e-6f || field == null) return;

        // 今いる場所の流れと、自分の動きがどれだけ揃っているか(-1~1)
        Vector2 here = field.Sample(transform.position);
        float align = Vector2.Dot(here, move.normalized) / Mathf.Max(field.MaxStrength, 0.0001f);

        float scale;
        if (align >= 0f)
        {
            // 沿っている → 増幅
            scale = 1f + amplify * align;
        }
        else
        {
            // 逆らっている → 弱める(強い逆流ほど0に近づく)
            scale = 1f - Mathf.Clamp01(-align * counterSensitivity);
        }

        lastAlign = align;
        lastScale = scale;

        if (scale <= 0f) return;

        // 移動量ベースなのでフレームレートが変わっても総量はほぼ同じ
        field.AddFlow(transform.position, move * (gain * scale), radius);
    }

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;

        // 緑 = 増幅中、赤 = 逆流で抑制中。円の大きさ = 書き込みの強さ
        Gizmos.color = lastAlign >= 0f ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f,
                              0.2f + 0.15f * lastScale);
    }
}
