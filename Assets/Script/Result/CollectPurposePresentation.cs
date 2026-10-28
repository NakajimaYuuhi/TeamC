using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CollectPurposePresentation : MonoBehaviour
{
    [SerializeField]
    private AnimationCurve _focusCurve = AnimationCurve.EaseInOut(0,0,1,1);

    [SerializeField, Range(0.3f, 0.6f)]
    private float _focusDistance = 0.4f;

    [SerializeField]
    private float _slowValue = 0.2f;

    public IEnumerator Play()
    {
        Time.timeScale = _slowValue;
        Time.fixedDeltaTime = _slowValue * 0.02f;

        float currentRate = 0.0f;
        var camCtrl = Camera.main.GetComponent<CameraController>();
        float prevCamDis = camCtrl.CurrentZoomDistance;
        while (currentRate < 1.0f)
        {
            currentRate = Mathf.Clamp01(currentRate + Time.deltaTime);
            camCtrl.SetZoomDistance(prevCamDis - (prevCamDis * _focusDistance * _focusCurve.Evaluate(currentRate)));
            yield return null;
        }

        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;

        UIChaosGatherTransition.Instance.LoadScene("Result");
    }
}
