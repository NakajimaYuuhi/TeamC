using UnityEngine;

public class ResultStateData : ScriptableObject
{
    [Header("リザルト状態")]
    [SerializeField, InspectorName("成功")]
    private bool isSuccess;

    [SerializeField, InspectorName("失敗")]
    private bool isFailed;

    public bool IsSuccess
    {
        get => isSuccess;
        set => isSuccess = value;
    }

    public bool IsFailed
    {
        get => isFailed;
        set => isFailed = value;
    }
}
