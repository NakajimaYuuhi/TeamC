using UnityEngine;

public class NPC_Destroyer : MonoBehaviour
{
    // コライダー
    [SerializeField] private Collider _collider;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // 当たったNPCタグのオブジェクトを破壊する
    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Trigger Entered: " + other.gameObject.name);

        // PatentTargetスクリプトを持つオブジェクトの子オブジェクトを取得
        ParentTarget parentTarget = other.GetComponent<ParentTarget>();
        if (parentTarget != null)
        {
            GameObject parentObject = parentTarget.GetParentObject();
            if (parentObject != null)
            {
                //Debug.Log("NPC Destroyed: " + parentObject.name);
                Destroy(parentObject);
            }
        }
    }
}
