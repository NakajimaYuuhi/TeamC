using UnityEngine;

public class ItemSkill : MonoBehaviour
{
    public enum SkillType
    {
        ToPurpose,
        Attract,
        Avoid,

    };

    private SkillType _skillType;

   
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("NPC")) return;

        switch (_skillType)
        {
            case SkillType.ToPurpose:
                break;
            case SkillType.Attract:
                break;
            case SkillType.Avoid:
                break;
        }
    }

    /// <summary>
    /// 敵がプレイヤーと同じ目標地点に行くようにする
    /// </summary>
    private void ExecuteToPurpose()
    {

    }

    /// <summary>
    /// 敵がアイテムの方に近寄ってくる
    /// </summary>
    private void ExecuteAttract()
    {

    }

    /// <summary>
    /// 敵がアイテムを避けて進む
    /// </summary>
    private void ExecureAvoid()
    {

    }
}
