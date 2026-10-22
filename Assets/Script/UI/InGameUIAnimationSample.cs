/**
 * file   : InGameUIAnimatioinSample.cs
 * brief  : 動作確認用のファイル
 * 　　　　　落ち着いたら消す
 * 　　　　　
 * auther : Takitani Shohei
 */
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;


public class InGameUIAnimationSample : MonoBehaviour
{
    [SerializeField] private GameObject _inGameUiPrefab;
	[SerializeField] private float _timer = 30.0f;

    private InGameUI _inGameUI;
    private InGameUIPopup _inGamePopup;

	private bool _isTimeStart = false;

	private readonly UITimer _uiTimer = new UITimer();


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameObject instance = Instantiate(_inGameUiPrefab);

        _inGameUI = instance.GetComponentInChildren<InGameUI>();
        _inGamePopup = instance.GetComponentInChildren<InGameUIPopup>();

		_uiTimer.RemainingTime = _timer;

		InGameUI.InGameUIData data;
		data.destination = "友達100人できるかな";
		data.identity = "00-24";
		data.stageNum = 8;

		InGameUI.InGameUI_PlayerData playerData;
		playerData.stamina = 50f;
		playerData.nuisance = 0f;

		_inGameUI.SetInGameUIData(data);
		_inGameUI.SetInGameUI_PlayerData(playerData);
		_inGameUI.SetUITimer(_uiTimer);
	}

    // Update is called once per frame
    void Update()
    {

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
			_inGamePopup.Play();    // ここで実行

		}

		if(Keyboard.current != null &&
			Keyboard.current.enterKey.wasPressedThisFrame)
		{
			
			InGameUI.InGameUIData data;
			data.destination = "友達100人できるだろ！！！！";
			data.identity = "VD-32";
			data.stageNum = 8;

			_uiTimer.RemainingTime = 50f;
			_inGameUI.SetInGameUIData(data);
		}

		if (Keyboard.current != null &&
			Keyboard.current.aKey.wasPressedThisFrame)
		{
//			_isTimeStart  = _isTimeStart == false ? true : false;
			_isTimeStart ^= true;
		}

		if (_isTimeStart)
		{
			_uiTimer.RemainingTime = Mathf.Max(0f, _uiTimer.RemainingTime - Time.deltaTime);
		}

	}
}
