using UnityEngine;

public class PlayerCryticalStep : MonoBehaviour
{
    [SerializeField, Tooltip("グリッド一個の大きさ(チェックサイズより大きいばあい仮の大きさを使用)"]
    private float _gridSize;

    [SerializeField, Tooltip("確認する範囲の大きさ")]
    private Vector2 _checkSize;

    public bool GetCanStepPosition(Vector3 playerPos, Vector3 playerSize, Vector2 stepDir)
    {
        int checkX = (int)(_checkSize.x / _gridSize), checkZ = (int)(_checkSize.x / _gridSize);
        int canStepSize = (int)(playerSize.x / _gridSize);

        bool isCanTeleport;
        int cntX = 0,cntZ = 0;

        for (int z = 0; z < checkZ; ++z)
        {
            for (int x = 0; x < checkX; ++x)
            {
                Vector3 CheckPos = playerPos + new Vector3(stepDir.x * (x * _gridSize + (_gridSize * 0.5f)), 0.5f, stepDir.y * (z * _gridSize + (_gridSize * 0.5f)));
                bool isBlocked = Physics.CheckSphere(checkPos, _gridSize * 0.45f, obstacleMask);

                if (!isBlocked)
                {
                    cntX++;

                    if (cntX >= canStepSize)
                    {
                        cntZ++;
                        z++;

                        if (cntZ >= canStepSize)
                        {
                            // Player取得＆位置を移動
                            // 演出開始
                            return isCanTeleport;
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

    }
}
