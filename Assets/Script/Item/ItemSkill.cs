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

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag != "NPC") return;

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
}
