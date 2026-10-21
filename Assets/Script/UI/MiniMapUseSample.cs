using UnityEngine;

public class MiniMapUseSample : MonoBehaviour
{

    [SerializeField] private GameObject _target;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        MiniMapManager minimapManager = GetComponent<MiniMapManager>();

        GameObject target = _target;
        if (target == null)
        {
        minimapManager.Initialize();
        }
        else
        {
            minimapManager.Initialize(target);

        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
