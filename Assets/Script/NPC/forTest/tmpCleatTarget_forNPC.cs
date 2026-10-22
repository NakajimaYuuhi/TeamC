using UnityEngine;

public class tmpCleatTarget_forNPC : MonoBehaviour
{
    // オンにするキャンバス
    [SerializeField] private GameObject _canvasToActivate;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Entered: " + other.gameObject.name);

        // Playerタグのオブジェクトがトリガーに入った場合
        if (other.CompareTag("Player"))
        {
            // キャンバスのコンポーネントをオンにする
            if (_canvasToActivate != null)
            {
                Debug.Log("Activating canvas: " + _canvasToActivate.name);
                // キャンバスのコンポーネントをオンにする
                _canvasToActivate.GetComponent<Canvas>().enabled = true;

            }
            else
            {
                Debug.LogWarning("Canvas to activate is not assigned.");
            }
        }
    }
}
