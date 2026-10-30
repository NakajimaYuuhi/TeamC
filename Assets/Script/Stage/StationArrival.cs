using UnityEngine;

//電車が走って駅に着くように見せる。電車は動かさず、ホームと線路のほうを動かす
//走っているあいだはホームは遠くにあり、線路が流れる。ブレーキで減速して、ドアが開くときにぴったり止まる
public class StationArrival : MonoBehaviour
{
    [Tooltip("ドアの開く時間を読む")]
    [SerializeField] TrainDoors doors;
    [Tooltip("ホームの部品。当たり判定ごと動かす")]
    [SerializeField] Transform station;
    [Tooltip("線路の部品。走っているあいだ流す")]
    [SerializeField] Transform tracks;
    [Tooltip("電車の進む向き")]
    [SerializeField] Vector3 travel = Vector3.right;
    [Tooltip("走る速さ。メートル毎秒")]
    [SerializeField] float runSpeed = 15f;
    [Tooltip("ブレーキをかけてから止まるまでの秒数")]
    [SerializeField] float brakeTime = 6f;
    [Tooltip("線路の1区画の長さ。流すときにこの長さでくり返す")]
    [SerializeField] float trackLength = 10f;

    Vector3 stationBase, tracksBase;
    float startTime;

    //ホームが止まる位置からのずれ
    public Vector3 Offset { get; private set; }
    public bool Arrived { get; private set; }
    //ブレーキをかけ始める秒数
    public float BrakeStart => (doors != null ? doors.OpenAfter : 0f) - brakeTime;

    void Awake()
    {
        if (station) stationBase = station.localPosition;
        if (tracks) tracksBase = tracks.localPosition;
        Move(0f);
    }

    void Start()
    {
        startTime = Time.time;
    }

    void FixedUpdate()
    {
        if (Arrived) return;
        Move(Time.time - startTime);
    }

    //t 秒のときの、ホームまでの残りの距離と、線路の流れた距離
    void Move(float t)
    {
        float arrive = doors != null ? doors.OpenAfter : 0f;
        float b = Mathf.Max(0.01f, brakeTime), tb = arrive - b;
        float left, run;
        if (t < tb)
        {
            left = runSpeed * (tb - t) + runSpeed * b * 0.5f;
            run = runSpeed * t;
        }
        else
        {
            float r = Mathf.Clamp(arrive - t, 0f, b);
            left = runSpeed * r * r / (2f * b);
            run = runSpeed * Mathf.Max(0f, tb) + runSpeed * b * 0.5f - left;
        }
        Vector3 dir = travel.normalized;
        Offset = dir * left;
        if (station) station.localPosition = stationBase + Offset;
        if (tracks) tracks.localPosition = tracksBase - dir * Mathf.Repeat(run, trackLength);
        if (t >= arrive) Arrived = true;
    }
}
