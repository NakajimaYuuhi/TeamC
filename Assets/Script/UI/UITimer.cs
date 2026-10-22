/**
 * file   : UITimer.cs
 * brief  : フローとUIで共有するデータ
 * 
 * auther : Takitani Shohei
 */
using UnityEngine;

public class UITimer
{
	//____________________________________
	// private variable

	private float _remainingTime = 99.9f;



	//____________________________________
	// property

	public float RemainingTime
	{
		get => _remainingTime;
		set => _remainingTime = Mathf.Max(0f, value);
	}

}
