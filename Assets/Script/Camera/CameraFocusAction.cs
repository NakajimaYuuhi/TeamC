using System.Collections;
using UnityEngine;


[RequireComponent(typeof(CameraController))]
public class CameraFocusAction : MonoBehaviour
{
    [SerializeField,Tooltip("カメラの演出を行う時間")]
    private float _actionTime;

    [SerializeField, Tooltip("スローの時間")]
    private float _slowValue;

    [SerializeField, Tooltip("カメラのフォーカス最大距離(元の距離 - 元の距離＊倍率)")]
    [Range(0.2f, 0.6f)]
    private float _focusDistance = 0.3f;

    [SerializeField,Tooltip("カメラの動きかた(1.0で一番近づく)")]
    private AnimationCurve _focusCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [SerializeField]
    private CameraController _cameraController;

    private float _currentRate;
    private float _prevCameraZoom;

    private void Start()
    {
        if(_cameraController == null)
            _cameraController = GetComponent<CameraController>();
    }

    public void Play()
    {
        var camCtrl = GetComponent<CameraController>();
        _prevCameraZoom = camCtrl.CurrentZoomDistance;
        StartCoroutine(Slow());
        StartCoroutine(Focus());
    }

    private IEnumerator Focus()
    {
        _currentRate = 0.0f;
        var camCtrl = GetComponent<CameraController>();
        while(_currentRate < 1.0f)
        {
            _currentRate = Mathf.Clamp01(_currentRate + Time.fixedDeltaTime);
            camCtrl.SetZoomDistance(_prevCameraZoom - (_prevCameraZoom * _focusDistance * _focusCurve.Evaluate(_currentRate) ));
            yield return null;
        }
        camCtrl.SetZoomDistance(_prevCameraZoom);
    }

    private IEnumerator Slow()
    {
        Time.timeScale = _slowValue;
        Time.fixedDeltaTime = 0.02f * _slowValue;
        yield return new WaitForSecondsRealtime(_actionTime);
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
    }
}
