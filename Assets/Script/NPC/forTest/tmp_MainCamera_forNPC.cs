using UnityEngine;

public class tmp_MainCamera_forNPC : MonoBehaviour
{
    [SerializeField]
    GameObject _player;

    Vector3 relativePos = new Vector3(0, 5, -10);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = _player.transform.position + relativePos;
    }
}
