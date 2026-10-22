/**
 * file   : InGameUI.cs
 * brief  : ゲームシーンでのUIを全体管理
 * 
 * auther : Takitani Shohei
 */
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class InGameUI : MonoBehaviour
{

	//____________________________________
	// structure

	// ゲーム開始時に受け取るデータ
	[System.Serializable]
	public struct InGameUIData
	{
		[Tooltip("ステージの番号")] public int stageNum;
		[Tooltip("目的地の名称")] public string destination;
		[Tooltip("目的地の番号")] public string identity;
	}

	// 更新されるPlayerのデータ
	[System.Serializable]
	public struct InGameUI_PlayerData
	{
		[Tooltip("体力")] public float stamina;
		[Tooltip("迷惑度")] public float nuisance;
	}



	//____________________________________
	// variables

	[Header("----- Text -----")]
	[SerializeField] private TMP_Text _stageText;
	[SerializeField] private TMP_Text _destinationText;
	[SerializeField] private TMP_Text _identityText;
	[SerializeField] private TMP_Text _remainingTimeText;


	[Header("----- Gauge -----")]
	[SerializeField] private Slider _staminaGauge;
	[SerializeField] private Slider _nuisanceGauge;


	[Header("----- Initialize -----")]
	[SerializeField, Min(1)] private int _stageNum = 1;
	[SerializeField] private string _destination = "Office";
	[SerializeField] private string _identity = "AT-13";


	[SerializeField] private float _stamina = 100.0f;
	[SerializeField] private float _nuisance = 0f;
	[SerializeField, Min(0f)] private float _staminaRecovery = 10f;
	[SerializeField, Min(0f)] private float _nuisanceDecrease = 5f;





	//____________________________________
	// constant

	private const float MAX_VALUE = 100f;


	//____________________________________
	// private variable

	private bool _isGameUISetUp = false;
	private bool _isPlayerSetUp = false;

	private bool _canPlay = false;

	private bool _timeStartFlag = false;

	private UITimer _uiTimer;


	public UITimer UITimer {  get { return _uiTimer; } }



	//____________________________________
	// basic function

	private void Awake()
	{
		_staminaGauge.minValue = 0f;
		_staminaGauge.maxValue = MAX_VALUE;
		_nuisanceGauge.minValue = 0f;
		_nuisanceGauge.maxValue = MAX_VALUE;

		SetStageNum(_stageNum);
		SetDestination(_destination);
		SetIdentity(_identity);
		SetStamina(_stamina);
		SetNuisance(_nuisance);
	}

	// Update is called once per frame
	void Update()
	{
		if (!_isGameUISetUp || !_isPlayerSetUp)
		{
			return;
		}

		// データがない場合、UI単独で更新する
		if (_timeStartFlag || _uiTimer == null)
		{
//			SetRemainingTime(_remainingTime - Time.deltaTime);
		}
		SetStamina(Mathf.MoveTowards(
			_stamina, MAX_VALUE, _staminaRecovery * Time.deltaTime));
		SetNuisance(Mathf.MoveTowards(
			_nuisance, 0f, _nuisanceDecrease * Time.deltaTime));
    }

	private void LateUpdate()
	{
		// フローのUpdate後に、細心の残り時間を更新
		if(_uiTimer != null)
		{
			SetRemainingTime(_uiTimer.RemainingTime);
		}
	}


	public void TimerStart()
	{
		_timeStartFlag = true;
	}




	//____________________________________
	// setter
	
	/// <summary>
	/// インゲームで使用するUIのデータをまとめて受け取る
	/// シーン開始時に呼び出し
	/// </summary>
	/// <param name="data"></param>
	public void SetInGameUIData(InGameUIData data)
	{
		SetStageNum(data.stageNum);
		SetDestination(data.destination);
		SetIdentity(data.identity);

		_isGameUISetUp = true;
		_canPlay = true;
		_timeStartFlag = false;
	}

	/// <summary>
	/// 変動するPlayerのデータをまとめて受け取る
	/// </summary>
	/// <param name="data"></param>
	public void SetInGameUI_PlayerData(InGameUI_PlayerData data)
	{
		SetStamina(data.stamina);
		SetNuisance(data.nuisance);

		_isPlayerSetUp = true;
	}

	public void SetUITimer(UITimer uiTimer)
	{
		_uiTimer = uiTimer;
		if(_uiTimer != null)
		{
			SetRemainingTime(_uiTimer.RemainingTime);
		}
	}

	public void SetStamina(float value)
	{
		_stamina = Mathf.Clamp(value, 0f, MAX_VALUE);
		_staminaGauge.value = _stamina;
	}

	public void SetNuisance(float value)
	{
		_nuisance = Mathf.Clamp(value, 0f, MAX_VALUE);
		_nuisanceGauge.value = _nuisance;
	}

	public void SetRemainingTime(float seconds)
	{
		float time = Mathf.Max(0f, seconds);
		_remainingTimeText.text = $"{time:F1}";
	}

	public void SetStageNum(int value)
	{
		_stageNum = value;
		_stageText.text = $"STAGE {_stageNum}";
	}

	public void SetDestination(string value)
	{
		_destination = value;
		_destinationText.text = _destination;
	}

	public void SetIdentity(string value)
	{
		_identity = value;
		_identityText.text = _identity;
	}



	//____________________________________
	// getter

	public InGameUIData GetInGameUIData()
	{
		return new InGameUIData
		{
			stageNum = _stageNum,
			destination = _destination,
			identity = _identity,
		};
	}

	public bool CanPlay
	{
		get => _canPlay;
		set => _canPlay = value;
	}


}
