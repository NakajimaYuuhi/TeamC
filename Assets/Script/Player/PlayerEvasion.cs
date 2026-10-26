/////////////////////////////////////////
/// PlayerEvasion.cs
/// 2026/10/23 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/23 佐藤蒼也：ステップ回避の判定と、成功・失敗イベントを実装
/// 
/////////////////////////////////////////

using UnityEngine;
using UnityEngine.Events;

//PlayerTmp（PlayerMoveと同じオブジェクト）に付ける
//ステップ中にNPCのすれすれを通ったら回避成功としてイベントを呼ぶ
//
//判定のルール
//  ステップ中に、EvasionAreaにNPCがいた（または入ってきた）
//  かつ、ステップ中に体（EvasionArea以外のコライダー）がNPCに当たらなかった → 回避成功
//  ステップ中に体がNPCに当たった → 回避失敗
//  周りにNPCがいないところでのステップ → 何も起きない
[RequireComponent(typeof(PlayerMove))]
public class PlayerEvasion : MonoBehaviour
{
    [Header("回避判定エリア（空なら子から自動で探す）")]
    [SerializeField] private EvasionSensor evasionSensor;

    [Header("回避成功時に呼ばれる関数（Inspectorやコードから登録）")]
    public UnityEvent onEvasionSucceeded = new UnityEvent();

    [Header("回避失敗時（ステップ中に体がNPCに当たった）に呼ばれる関数")]
    public UnityEvent onEvasionFailed = new UnityEvent();

    [Header("回り込み回避")]
    [SerializeField, Tooltip("ステップ中にNPCを回り込んでよけるか")]
    private bool enableDodge = true;
    [SerializeField, Tooltip("回り込みの強さ（1で完全によける、0でよけない）"), Range(0f, 1f)]
    private float dodgeStrength = 1f;

    private PlayerMove playerMove;
    private bool isStepping; //ステップ中か
    private bool nearMiss;   //ステップ中にNPCのすれすれを通ったか
    private bool bodyHit;    //ステップ中に体がNPCに当たったか

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();

        if (evasionSensor == null)
        {
            evasionSensor = GetComponentInChildren<EvasionSensor>();
        }

        if (evasionSensor == null)
        {
            Debug.LogError("EvasionSensorが見つからない。EvasionAreaに付けてください");
        }
    }

    //有効になったときにイベントへ登録する
    void OnEnable()
    {
        playerMove.StepStarted += OnStepStarted;
        playerMove.StepEnded += OnStepEnded;

        if (evasionSensor != null)
        {
            evasionSensor.NPCEntered += OnNPCEnteredArea;
        }
    }

    //無効になったときに登録を解除する
    void OnDisable()
    {
        playerMove.StepStarted -= OnStepStarted;
        playerMove.StepEnded -= OnStepEnded;

        if (evasionSensor != null)
        {
            evasionSensor.NPCEntered -= OnNPCEnteredArea;
        }
    }

    //ステップが始まったとき（PlayerMoveから呼ばれる）
    private void OnStepStarted()
    {
        isStepping = true;
        bodyHit = false;

        //ステップを始めた時点ですでにすぐそばにNPCがいれば、すれすれ扱いにする
        nearMiss = evasionSensor != null && evasionSensor.HasNPCNearby;
    }

    //EvasionAreaにNPCが入ってきたとき（EvasionSensorから呼ばれる）
    private void OnNPCEnteredArea(GameObject npc)
    {
        if (isStepping)
        {
            nearMiss = true;
        }
    }

    //体がぶつかったとき
    //（EvasionAreaはTriggerなのでここには来ない。来るのは体のコライダーだけ）
    void OnCollisionEnter(Collision collision)
    {
        CheckBodyHit(collision);
    }

    //触れ続けている間（ステップ開始時にすでに触れていた場合も失敗にするため）
    void OnCollisionStay(Collision collision)
    {
        CheckBodyHit(collision);
    }

    private void CheckBodyHit(Collision collision)
    {
        if (!isStepping) return;

        if (EvasionSensor.FindNPCRoot(collision.transform) != null)
        {
            bodyHit = true;
        }
    }

    //ステップが終わったとき（PlayerMoveから呼ばれる）に成功・失敗を判定する
    private void OnStepEnded()
    {
        isStepping = false;

        if (bodyHit)
        {
            Debug.Log("回避失敗");
            onEvasionFailed.Invoke();
        }
        else if (nearMiss)
        {
            Debug.Log("回避成功");
            onEvasionSucceeded.Invoke();
        }
    }

    //ステップ中の速度を、近くのNPCを回り込むように曲げて返す（PlayerMoveから毎回呼ばれる）
    public Vector3 GetDodgeVelocity(Vector3 position, Vector3 velocity)
    {
        if (!enableDodge || evasionSensor == null) return velocity;

        //エリア内に一番近いNPCがいなければ、そのまま進む
        if (!evasionSensor.TryGetNearestNPC(position, out Vector3 npcPosition)) return velocity;

        //プレイヤーからNPCへの水平な向き
        Vector3 toNPC = npcPosition - position;
        toNPC.y = 0f;
        if (toNPC.sqrMagnitude < 0.0001f) return velocity;
        Vector3 n = toNPC.normalized;

        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speed = flatVelocity.magnitude;

        //速度のうち、NPCに向かっている成分の大きさ（内積）
        float toward = Vector3.Dot(flatVelocity, n);

        //NPCから離れる向きに進んでいるなら、よける必要はない
        if (toward <= 0f) return velocity;

        //NPCに向かう成分を消して、横を通る成分だけを残す
        Vector3 tangent = flatVelocity - n * toward;

        //NPCに真正面から向かっていて横の成分がないときは、右側によける
        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.Cross(Vector3.up, n);
        }

        //横を通る向きに、元の速さで進む
        Vector3 dodgeVelocity = tangent.normalized * speed;

        //強さに応じて、元の速度と回り込みの速度を混ぜる
        Vector3 result = Vector3.Lerp(flatVelocity, dodgeVelocity, dodgeStrength);

        return new Vector3(result.x, velocity.y, result.z);
    }
}