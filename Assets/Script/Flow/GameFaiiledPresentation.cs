using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static UnityEngine.LowLevelPhysics2D.PhysicsLayers;

public class GameFaiiledPresentation : MonoBehaviour
{
    [SerializeField, Tooltip("背景をぼかすための画像付きキャンバス")]
    private GameObject _canvas;

    [SerializeField]
    private AnimationCurve _rollingCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [SerializeField]
    private AnimationCurve _downCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [SerializeField]
    private float _animSpeed = 0.01f;

    [SerializeField,InspectorName("最初の傾き")]
    private float _startSlope = 20.0f;

    [SerializeField]
    private float _zoomDis = 5.0f;

    [SerializeField]
    private float _rotValue = 360.0f;
    

    public IEnumerator PlayFailedAnimation()
    {
        Time.timeScale = 0.0f;
        Time.fixedDeltaTime = 0.0f;

        // キャンバスとプレイヤー取得
        // すでにあるCanvasを一度消す
        FindFirstObjectByType<Canvas>().enabled = false;
        var player = GameObject.FindWithTag("Player");

        // プレイヤーのレイヤーをせ設定してプレイヤーのみ映るようにする
        SetLayerChildren(player, LayerMask.NameToLayer("Player"));

        // 新たにスクリーンスペースがカメラののCanvasを生成
        var mainCamera = Camera.main;
        var canvas = Instantiate(_canvas,mainCamera.gameObject.transform);
        canvas.GetComponentInChildren<Canvas>().worldCamera = mainCamera;

        // スタックに追加したプレハブのカメラを追加
        var layerCamera = canvas.GetComponentInChildren<Camera>();
        var cameraData = mainCamera.GetUniversalAdditionalCameraData();
        cameraData.cameraStack.Add(layerCamera);

        float rate = 0;
        // カメラを近づける
        // プレイヤーを傾ける
        var playerTrans = player.transform;
        var playerRotation = playerTrans.eulerAngles;
        var camCtrl = mainCamera.GetComponent<CameraController>();
        float StartDis = camCtrl.CurrentZoomDistance;
        while(rate < 1.0f)
        {
            rate += 0.1f;
            camCtrl.SetZoomDistance(Mathf.Lerp(StartDis, _zoomDis, rate));
            rate = Mathf.Clamp01(rate + 0.1f);
            playerRotation.z = Mathf.Lerp(playerRotation.z, _startSlope, rate);
            player.transform.eulerAngles = playerRotation;
            yield return null;
        }

        rate = 0.0f;
        float playerStartRot = playerRotation.y;
        // プレイヤーのキャラクターがくるっと回りながら倒れる演出
        while (rate < 1.0f)
        {
            rate = Mathf.Clamp01(rate + _animSpeed);

            playerRotation.y = playerStartRot + (_rollingCurve.Evaluate(rate) * _rotValue);
            player.transform.eulerAngles = playerRotation;

            yield return null;
        }

        rate = 0.0f;
        // 回転完了後にアニメーションカーブに合わせて倒れる
        playerStartRot = playerRotation.z;
        while (rate < 1.0f)
        {
            rate = Mathf.Clamp01(rate + _animSpeed);

            playerRotation.z = Mathf.Lerp(playerStartRot,90.0f,_downCurve.Evaluate(rate));
            player.transform.eulerAngles = playerRotation;

            yield return null;
        }

        // タイムスケールを戻す
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        // トランジション再生
        UIChaosGatherTransition.Instance.LoadScene("GameOver");
    }

    private void SetLayerChildren(GameObject obj,int newlayer)
    {
        if (obj == null) return;

        obj.layer = newlayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerChildren(child.gameObject, newlayer);
        }
    }
}
