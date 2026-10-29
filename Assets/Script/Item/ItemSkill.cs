using UnityEditor.SearchService;
using UnityEngine;

public class ItemSkill : MonoBehaviour
{
    public enum SkillType
    {
        ToPurpose,
        Attract,
        Avoid,
        TypeCount,
    };

    [SerializeField]
    private SkillType _skillType = SkillType.ToPurpose;
    public void SetType(SkillType type)
    {
        _skillType = type;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("NPC")) return;

        switch (_skillType)
        {
            case SkillType.ToPurpose:
                ExecuteToPurpose(other.gameObject);
                break;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        switch (_skillType)
        {
            case SkillType.Attract:
                ExecuteAttract(other.gameObject);
                break;
            case SkillType.Avoid:
                ExecuteAvoid(other.gameObject);
                break;
        }
    }

    /// <summary>
    /// 敵がプレイヤーと同じ目標地点に行くようにする
    /// </summary>
    private void ExecuteToPurpose(GameObject obj)
    {
        var npcCtrl = obj.GetComponentInParent<NPC_Controller>();
        if(npcCtrl != null)
        {
            npcCtrl.SetNode(npcCtrl.NodeGenerator.GetNode(0));
        }
    }

    /// <summary>
    /// 敵を引き寄せる
    /// </summary>
    private void ExecuteAttract(GameObject obj)
    {
        var rb = obj.GetComponentInParent<Rigidbody>();
        if(rb != null)
        {
            Vector3 direction = transform.position - obj.transform.position;

            rb.AddForce(direction * 10.0f,ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// 敵がアイテムを避けて進む
    /// </summary>
    private void ExecuteAvoid(GameObject obj)
    {
        var rb = obj.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            Vector3 direction = obj.transform.position - transform.position;

            direction.y = 0.0f;
            rb.AddForce(direction * 10.0f,ForceMode.Acceleration);
        }
    }
}
