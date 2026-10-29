using System.Collections;
using UnityEngine;

public class PlayerCryticalStep : MonoBehaviour
{
    [SerializeField]
    private LayerMask _obstacleMask;

    [SerializeField, Tooltip("グリッド一個の大きさ(チェックサイズより大きい場合仮の大きさを使用)")]
    private float _gridSize;

    [SerializeField, Tooltip("確認する範囲の大きさ")]
    private Vector2 _checkSize;

    public bool GetCanStepPosition(Vector3 playerPos, Vector3 playerSize, Vector3 stepDir)
    {
        int checkX = (int)(_checkSize.x / _gridSize), checkZ = (int)(_checkSize.y / _gridSize);
        int canStepSize = (int)(playerSize.x / _gridSize);

        int cntX = 0,cntZ = 0;

        for (int z = 0; z < checkZ; ++z)
        {
            for (int x = 0; x < checkX; ++x)
            {
                Vector3 CheckPos = playerPos + new Vector3(
                    (x * _gridSize * stepDir.x) + (stepDir.x * _gridSize * 0.5f),
                    0.0f, 
                    (z * _gridSize * stepDir.z) + (stepDir.z * _gridSize * 0.5f));
                bool isBlocked = Physics.CheckSphere(CheckPos, _gridSize * 0.45f, _obstacleMask);

                if (!isBlocked)
                {
                    cntX++;

                    if (cntX >= canStepSize)
                    {
                        cntX = 0;
                        cntZ++;
                        z++;

                        if (cntZ >= canStepSize)
                        {
                            // Player取得＆位置を移動
                            Vector3 stepOffset;
                            stepOffset.x = playerSize.x * -stepDir.x;
                            stepOffset.z = playerSize.z * -stepDir.z;
                            stepOffset.y = 0.0f;
                            transform.position = (CheckPos - stepOffset);
                            // 演出開始
                            StartCoroutine(StepEffect());

                            return true;
                        }
                    }
                }
                else
                {
                    z -= cntZ;
                    cntX = cntZ = 0;
                }
            }
        }
        return false;

    }

    private IEnumerator StepEffect()
    {
        yield return null;
    }

    
}
