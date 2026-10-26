using UnityEngine;

public class ItemSkill : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag != "NPC") return;

        
    }
}
