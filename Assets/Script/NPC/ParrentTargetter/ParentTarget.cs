using UnityEngine;

public class ParentTarget : MonoBehaviour
{
    [SerializeField] private GameObject _parentObject;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public GameObject GetParentObject()
    {
        return _parentObject;
    }
}
