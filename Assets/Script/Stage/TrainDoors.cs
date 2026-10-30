using DG.Tweening;
using UnityEngine;

//時間になったら、ホーム側のドアを全部開ける。開くまではドアの当たり判定でホームへ出られない
public class TrainDoors : MonoBehaviour
{
    [Tooltip("ステージが始まってから開くまでの秒数")]
    [SerializeField] float openAfter = 25f;
    [Tooltip("開き切るまでの秒数")]
    [SerializeField] float slideTime = 0.6f;
    [Tooltip("扉が横に動く距離")]
    [SerializeField] float slideDistance = 0.65f;
    [Tooltip("扉。1つのドアの2枚を続けて並べる")]
    [SerializeField] Transform[] leaves;
    [Tooltip("ドアの所をふさぐ当たり判定。開くと消す")]
    [SerializeField] Collider[] blockers;

    float startTime;
    bool opened;

    public bool IsOpen => opened;
    //開くまでの秒数
    public float OpenAfter => openAfter;

    void Start()
    {
        startTime = Time.time;
    }

    void Update()
    {
        if (!opened && Time.time - startTime >= openAfter) Open();
    }

    //全部のドアを開ける
    public void Open()
    {
        if (opened) return;
        opened = true;
        foreach (var c in blockers) if (c) c.enabled = false;
        //2枚ずつ、相手と反対の向きへ動かす
        for (int i = 0; i + 1 < leaves.Length; i += 2)
        {
            Transform a = leaves[i], b = leaves[i + 1];
            if (a == null || b == null) continue;
            Vector3 dir = (a.localPosition - b.localPosition).normalized * slideDistance;
            a.DOLocalMove(a.localPosition + dir, slideTime).SetEase(Ease.OutCubic).SetLink(a.gameObject);
            b.DOLocalMove(b.localPosition - dir, slideTime).SetEase(Ease.OutCubic).SetLink(b.gameObject);
        }
    }
}
