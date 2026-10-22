using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultUI : MonoBehaviour
{
    [SerializeField, Tooltip("ゲームの情報")]
    private GameData _gameData;

    [SerializeField, Tooltip("結果を表示するTMP")]
    private TMPro.TextMeshProUGUI _resultTMP;

    [SerializeField, Tooltip("タイムを表示するTMP")]
    private TMPro.TextMeshProUGUI _timeTMP;

    private void Awake()
    {
        _resultTMP.text = _gameData.Result.IsClear ? "Clear" : "Failed";

        _timeTMP.text = _gameData.Result.pastTime.ToString();
    }


    public void Next()
    {
        SceneManager.LoadScene("PlayTest");
    }
}
