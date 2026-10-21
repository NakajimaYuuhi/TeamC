/**
 * file   : MiniMapManager.cs
 * brief  : ミニマップの描画、ミニマップ用カメラの作成
 * 
 * auther : Takitani Shohei
 */
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class MiniMapManager : MonoBehaviour
{
    [Header("----- Camera -----")]
    [SerializeField] private Vector3 _camPos = new Vector3(0f, 50f, 0f);
    [SerializeField] private Vector3 _camRot = new Vector3(90f, 0f, 0f);

    [Tooltip("表示範囲の縦幅(半分)")]
    [SerializeField, Min(0.1f)] private float _orthograpicSize = 25f;
    [SerializeField, Min(1f)] private float _farClipPlane = 200f;


    [Header("----- Render Texture -----")]
    [Tooltip("カメラの映像を保存する画像の縦横サイズ(解像度)")]
    [SerializeField, Min(16)] private int _texSize = 256;

    [Header("----- UI -----")]
    [Tooltip("UI上に表示する大きさ")]
    [SerializeField] private Vector2 _displaySize = new Vector2(256f, 256f);
    [Tooltip("画面右上からの表示位置")]
    [SerializeField] private Vector2 _displayPos = new Vector2(-20f, -20f);



    //______________________________
    // private variables

    private Camera _miniMapCamera;
    private RenderTexture _renderTexture;
    private GameObject _canvasObject;
    private RawImage _rawImage;

    private GameObject _targetObj;



	//______________________________
	// basic function

	private void OnEnable()
	{
        SetActive(true);
	}

	private void OnDisable()
	{
        SetActive(false);
	}

	private void OnDestroy()
	{
		if(_miniMapCamera != null)
        {
            _miniMapCamera.enabled = false;
            _miniMapCamera.targetTexture = null;
            Destroy(_miniMapCamera.gameObject);
        }

        if(_rawImage != null)
        {
            _rawImage.texture = null;
        }

        if(_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        if (_canvasObject != null)
        {
            Destroy(_canvasObject);
        }
	}

	private void LateUpdate()
	{
		if(_targetObj == null || _miniMapCamera == null)
        {
            return;
        }


        _camPos.x = _targetObj.transform.position.x;
        _camPos.z = _targetObj.transform.position.z;

        _miniMapCamera.transform.position = _camPos;
	}


	//______________________________
	// unique function

	/// <summary>
	/// 固定カメラ、描画先、表示用UIを生成する
	/// </summary>
	/// <param name="target">minimapの中心、追従となるオブジェクト</param>
	public void Initialize(GameObject target = null)
    {
        if(target != null)
        {
            _targetObj = target;
        }

        if(_miniMapCamera != null)
        {
            return;
        }

        int texSize = Mathf.Clamp(_texSize, 16, SystemInfo.maxTextureSize);

        // 描画先の作成
        _renderTexture = new RenderTexture(texSize, texSize, 24);
        _renderTexture.name = "MiniMapRenderTexture";
        _renderTexture.filterMode=  FilterMode.Bilinear;    // 拡大・縮小したとき、隣接する画素を補間
        _renderTexture.wrapModeW = TextureWrapMode.Clamp;   // 画像の端で、反対側の画像が繰り返されないように

        if (!_renderTexture.Create())
        {
            Debug.LogError("[MiniMapManager::Initialize] failed to create RenderTexture.", this);
            Destroy(_renderTexture);
            _renderTexture = null;
            return;
        }

        // 上で作成したRenderTextureを、カメラの出力先とUIの表示先に設定
        CreateCamera();
        CreateUI();

        // 無効状態で初期化された場合、勝手に表示しない
        SetActive(isActiveAndEnabled);
    }



	//______________________________
	// private function

	/// <summary>
	/// 指定位置から真上を写す、平行投影カメラを作成、設置
	/// </summary>
	private void CreateCamera()
    {
        GameObject cameraObject = new GameObject(
            "MinimapCamera",
            typeof(Camera),
            typeof(UniversalAdditionalCameraData));

        _miniMapCamera = cameraObject.GetComponent<Camera>();
        _miniMapCamera.enabled = false; // 一旦停止

        _miniMapCamera.transform.SetPositionAndRotation(    // 角度、位置固定
            _camPos, Quaternion.Euler(_camRot));

        // 平行投影として設定
        _miniMapCamera.orthographic = true;
        _miniMapCamera.orthographicSize = Mathf.Max(0.1f, _orthograpicSize);

        // カメラの描画距離
        _miniMapCamera.nearClipPlane = 0.1f;
        _miniMapCamera.farClipPlane = Mathf.Max(1f, _farClipPlane);

        // オブジェクトが映っていない部分は灰色で埋める
        _miniMapCamera.clearFlags = CameraClearFlags.SolidColor;
        _miniMapCamera.backgroundColor = Color.white;

        // 画像へ直接出力せず、生成したRenderTextureへ描画
        _miniMapCamera.targetTexture = _renderTexture;

        // URPの独立したBaseカメラとして使用
        UniversalAdditionalCameraData camData = cameraObject.GetComponent<UniversalAdditionalCameraData>();

        camData.renderType = CameraRenderType.Base; // overlayでなく、独立したBaseとして作成
		camData.renderPostProcessing = false;       // ミニマップにはポストプロセスは適応しない
    }



    /// <summary>
    /// CanvasとRawImageを生成し、カメラの映像を接続する
    /// </summary>
    private void CreateUI()
    {
        _canvasObject = new GameObject(
            "MiniMapCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = _canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;  // カメラを指定せず、画面上に直接UIを表示
        canvas.sortingOrder = 10;   // 小さいsortingOrderのCanvasより手前に表示

        CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
        // 1920x1080を基準に、画面解像度に合わせてUIを拡張
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // RenderTextureを表示するRawImageを生成
        GameObject imageObject = new GameObject(
            "MiniMapImage",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));

        // falseを指定し、親Canvasを基準にしたTransformで配置
        imageObject.transform.SetParent(_canvasObject.transform, false);

        _rawImage = imageObject.GetComponent<RawImage>();
        _rawImage.texture = _renderTexture; // カメラの描画している画像を、そのまま表示先にする
        _rawImage.raycastTarget = false;    // 表示専用

        // 画面右上を基準に配置
        RectTransform rectTransform = _rawImage.rectTransform;
        rectTransform.anchorMin = Vector2.one;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = Vector2.one;

        // 基準位置からの余白と、表示サイズを指定
        rectTransform.anchoredPosition = _displayPos;
        rectTransform.sizeDelta = _displaySize;
    }


    /// <summary>
    /// 生成したカメラとUIの有効状態を切り替え
    /// </summary>
    /// <param name="isActive"></param>
    private void SetActive(bool isActive)
    {
        if(_miniMapCamera != null)
        {
            _miniMapCamera.enabled = isActive;
        }
        if(_canvasObject != null)
        {
            _canvasObject.SetActive(isActive);
        }
    }



}
