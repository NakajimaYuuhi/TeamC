using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class ResultCanvasView : MonoBehaviour
{
    [Header("リザルト表示")]
    [SerializeField, InspectorName("リザルト状態")]
    [Tooltip("表示する成功・失敗の状態を保持するSOです。")]
    private ResultStateData resultState;

    [SerializeField, InspectorName("成功表示")]
    [Tooltip("成功時に表示するGameObjectです。")]
    private GameObject successs;

    [SerializeField, InspectorName("失敗表示")]
    [Tooltip("失敗時に表示するGameObjectです。")]
    private GameObject failed;

    private void OnEnable()
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        successs.SetActive(resultState.IsSuccess);
        failed.SetActive(resultState.IsFailed);
    }
}
