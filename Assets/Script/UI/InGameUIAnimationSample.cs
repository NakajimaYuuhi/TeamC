/*
 * file   : InGameUIAnimatioinSample.cs
 * brief  : 動作確認用のファイル
 * 　　　　　落ち着いたら消す
 * 　　　　　
 * auther : Takitani Shohei
 */
using UnityEngine;
using UnityEngine.InputSystem;


public class InGameUIAnimationSample : MonoBehaviour
{
    [SerializeField] private GameObject _inGameUiPrefab;

    private InGameUI _inGameUI;
    private InGameUIPopup _inGamePopup;



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject instance = Instantiate(_inGameUiPrefab);

        _inGameUI = instance.GetComponentInChildren<InGameUI>();
        _inGamePopup = instance.GetComponentInChildren<InGameUIPopup>();

		InGameUI.InGameUIData data;
		data.destination = "testName";
		data.identity = "00-24";
		data.remainingTime = 50f;
		data.stageNum = 8;

		InGameUI.InGameUI_PlayerData playerData;
		playerData.stamina = 50f;
		playerData.nuisance = 0f;

		_inGameUI.SetInGameUIData(data);
		_inGameUI.SetInGameUI_PlayerData(playerData);
	}

    // Update is called once per frame
    void Update()
    {
		/*
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
			if (_inGamePopup == null)
			{
				Debug.LogError(
					"test: InGamePopup is not assigned.", this);
				return;
			}

            Debug.Log("play");	
			_inGamePopup.Play();	// ここで実行

		}
		*/
	}
}
