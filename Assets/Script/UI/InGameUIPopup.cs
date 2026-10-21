/*
 * file   : InGameUIPopup.cs
 * brief  : 目的地設定時のUIアニメーション
 * 
 * auther : Takitani Shohei
 */
using UnityEngine;
using System.Collections;
using TMPro;



public class InGameUIPopup : MonoBehaviour
{
    //____________________________________
    // variable

    [Header("----- Delay -----")]
    [Tooltip("移動終了後に表示する部分")]
    [SerializeField] private CanvasGroup _hudContent;
    [SerializeField] private CanvasGroup _desableContent;


    [Header("----- Popup Position -----")]
    [Tooltip("最初にポップアップを表示する位置")]
    [SerializeField] private Vector2 _startPos;
    [Tooltip("ポップアップ時の形")]
    [SerializeField] private Vector2 _startSize;

    [Header("----- Porpose Text -----")]
    [SerializeField] private TMP_Text _porposeText;
    [SerializeField] private string _baseText = "Aim for ";
    [SerializeField] private float _spacingIdentityToDestination;


    [Header("----- Duration -----")]
    [Tooltip("Board全体がフェードインするまでの秒数")]
    [SerializeField, Min(0f)] private float _fadeInDuration = 0.4f;
    [Tooltip("出現した位置での待機時間")]
    [SerializeField, Min(0f)] private float _waitDuration = 1.5f;
	[Tooltip("開始位置から常設位置まで移動する秒数")]
	[SerializeField, Min(0f)] private float _moveDuration = 0.6f;
	[Tooltip("移動後、追加情報が表示されるまでの時間")]
	[SerializeField, Min(0f)] private float _hudFadeDuration = 0.4f;



	//____________________________________
	// private variable

	[Tooltip("Boardの位置を操作するための参照")]
	private RectTransform _rectTransform;
	[Tooltip("Boardと、その子全体の透明度を操作するための参照")]
	private CanvasGroup _boardGroup;
	[Tooltip("アニメーション終了後の常設位置")]
	private Vector2 _endPos;
	[Tooltip("アニメーション終了後の大きさ")]
	private Vector2 _endSize;

    private InGameUI _inGameUi;



    private bool _isInitialized;
    private bool _hasPlayed;




	//____________________________________
	// private function

	private void Awake()
	{
        _inGameUi = GetComponent<InGameUI>();

		_rectTransform = GetComponent<RectTransform>();
        _boardGroup = GetComponent<CanvasGroup>();

        // 後から表示するグループは、Boardとは別に設定する
        if(_hudContent == null || _hudContent == _boardGroup ||
            !_hudContent.transform.IsChildOf(transform))
        {
            Debug.LogError("[InGameUIPopup] Assign a child CanvasGroup to HUD content.");

            _boardGroup.alpha = 0f;
            enabled = false;
            return;
        }

        // 位置を移動させる前に、常設の位置を保存
        _endPos = _rectTransform.anchoredPosition;
        _endSize = _rectTransform.sizeDelta;

        _boardGroup.alpha = 0f;
        _boardGroup.interactable = false;       // 表示専用を明示し、操作や背後のUI入力を遮らない
        _boardGroup.blocksRaycasts = false;

        _hudContent.alpha = 0f;
        _hudContent.interactable = false;
        _hudContent.blocksRaycasts = false;
        _hudContent.ignoreParentGroups = false; // 親のCanvasGroupの透明度も適用

        _desableContent.alpha = 1f;
        _desableContent.interactable = false;
        _desableContent.blocksRaycasts = false;
        _desableContent.ignoreParentGroups = false;

        _isInitialized = true;
    }


    //____________________________________
    // unique function

    [ContextMenu("Play Popup")]
    public void Play()
    {
        if(!Application.isPlaying ||
            !_isInitialized || !isActiveAndEnabled)
        {
            return;
        }

        // 演出完了後も含め、同じインスタンスの再実行は拒否
        if (_hasPlayed)
        {
            return;
        }

        _hasPlayed = true;
        // 待機を挟みながら、アニメーションを順番に実行
        StartCoroutine(PlaySequence());
    }



	//____________________________________
	// private function

    /// <summary>
    /// 出現、待機、移動、追加情報の表示を順番に実行する
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlaySequence()
    {
        // 指定した開始位置へ移動させる
        _rectTransform.anchoredPosition = _startPos;
        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _startSize.x);
        _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _startSize.y);

        // 表示テキストを設定
		InGameUI.InGameUIData data = _inGameUi.GetInGameUIData();
//		_porposeText.text = $"Reach {data.identity} [{data.destination}] within {data.remainingTime:F0} seconds!!";
		_porposeText.text = $"{data.remainingTime:F0}秒で {data.identity} 「{data.destination}」を目指せ！";

		// Boardの出現が終わるまで、この先の処理を待つ
		yield return FadeIn(_boardGroup, _fadeInDuration);

        // 現実の時間で待機する
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, _waitDuration));

        
        StartCoroutine(FadeOut(_desableContent, _moveDuration * 0.3f));

        float elapsed = 0f;

        while(elapsed < _moveDuration)
        {
            // 前フレームからの経過量を加算
            elapsed += Time.unscaledDeltaTime;

            // 移動の開始と終了を滑らかに
            float pregress = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / _moveDuration));

            // 進行度に応じて、開始位置と常設位置の間へ配置する
            _rectTransform.anchoredPosition =
                Vector2.Lerp(_startPos, _endPos, pregress);
            Vector2 size = Vector2.Lerp(_startSize, _endSize, pregress);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);


            // 次のフレームまで処理を中断
            yield return null;
        }

        // 移動時間が0の場合も、最終位置へ確実に配置
        _rectTransform.anchoredPosition = _endPos;


        // 常設位置へ移動後、ゲージなどの追加情報を表示
        yield return FadeIn(_hudContent, _hudFadeDuration);
    }

    private IEnumerator FadeIn(CanvasGroup target, float duration)
    {
        float elapsed = 0f;
        target.alpha  = 0f;


        while(elapsed < duration)
        {
            // ゲームの時間倍率に影響されずに進める
            elapsed += Time.unscaledDeltaTime;
            // Clamp01で変化量を0～1に
            target.alpha = Mathf.Clamp01(elapsed / duration);

            yield return null;
        }

        // 時間が0の場合も、最終的に完全表示する
        target.alpha = 1f;
    }

    private IEnumerator FadeOut(CanvasGroup target, float duration)
    {
        float elapeed = 0f;
        target.alpha = 1f;

        while(elapeed < duration)
        {
            elapeed += Time.unscaledDeltaTime;
            target.alpha = 1f - Mathf.Clamp01(elapeed / duration);

            yield return null;
        }

        target.alpha = 0f;
    }

}
