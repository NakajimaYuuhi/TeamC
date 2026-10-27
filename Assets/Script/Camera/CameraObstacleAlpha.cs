using UnityEngine;
using System.Collections.Generic;

public class CameraObstacleAlpha : MonoBehaviour
{
    [SerializeField]
    private Transform _player;
    [SerializeField]
    private Material _obstacleMaterial;

    private readonly HashSet<GameObject> _previousHits = new();
    private readonly HashSet<GameObject> _currentHits = new();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_player == null) 
            _player = GameObject.FindWithTag("Player").transform;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if(_player == null) return;
        
        Vector3 difference = (_player.transform.position - transform.position);
        float distance = difference.magnitude;

        if(distance <= 0.001f) return;

        Vector3 direction =  difference / distance;

        RaycastHit[] hits = Physics.RaycastAll(
            transform.position, direction, distance);
        
        Debug.DrawRay(transform.position, direction * distance, Color.red);

        _currentHits.Clear();

        foreach(RaycastHit hit in hits)
        {
            if (hit.collider.tag != "AlphaWall") continue;

            GameObject target = hit.collider.gameObject;

            if (!_currentHits.Add(target))
                continue;

            ObstacleAlphaMat alphaMat = target.GetComponent<ObstacleAlphaMat>();
            if (alphaMat == null)
                alphaMat = target.AddComponent<ObstacleAlphaMat>();

            alphaMat.SetTransparent(true,_obstacleMaterial);
        }
        foreach(GameObject obj in _previousHits)
        {
            if(obj == null || _currentHits.Contains(obj)) continue;

            ObstacleAlphaMat alphaMat = 
                obj.GetComponent<ObstacleAlphaMat>();
            if (alphaMat != null)
                alphaMat.SetTransparent(false,_obstacleMaterial);
        }

        _previousHits.Clear();

        foreach(GameObject obj in _currentHits)
            _previousHits.Add(obj);
    }
}
