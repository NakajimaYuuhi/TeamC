using UnityEngine;

// MonoBehaviourではなく描画ヘルパーにして、WarpGapFinderと同じsettingsを参照させる。
// この関数は、settingsを保持しているMonoBehaviourのOnDrawGizmosから呼び出す。
public static class WarpGridDebugDrawer
{
    private const int MaxGridLines = 500;

    public static void Draw(WarpGapSettings settings)
    {
        if (settings == null) return;
        if (!settings.drawDebugArea && !settings.drawDebugGrid) return;
        if (settings.gridCellSize <= 0f) return;

        Rect r = settings.GetAreaRectXZ();
        if (r.width <= 0f || r.height <= 0f) return;

        // 判定はXZの2次元矩形。高さは線の表示位置にしか使用しない。
        float y = settings.GetStartPosition().y + settings.debugLineYOffset;
        Vector3 bottomLeft = new Vector3(r.xMin, y, r.yMin);
        Vector3 bottomRight = new Vector3(r.xMax, y, r.yMin);
        Vector3 topLeft = new Vector3(r.xMin, y, r.yMax);
        Vector3 topRight = new Vector3(r.xMax, y, r.yMax);

        if (settings.drawDebugArea)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            // 始点と終点。順序が逆でも可視化は成立する。
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(settings.GetStartPosition().x, y,
                settings.GetStartPosition().z), 0.12f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(new Vector3(settings.GetEndPosition().x, y,
                settings.GetEndPosition().z), 0.12f);
        }

        if (!settings.drawDebugGrid) return;
        float cell = settings.gridCellSize;
        // Spatial Hashと同じ、ワールド座標の0を基準とするセル境界を描く。
        int xStart = Mathf.CeilToInt(r.xMin / cell);
        int xEnd = Mathf.FloorToInt(r.xMax / cell);
        int zStart = Mathf.CeilToInt(r.yMin / cell);
        int zEnd = Mathf.FloorToInt(r.yMax / cell);

        // 異常な範囲・セルサイズでEditorの描画が重くなることを防ぐ。
        long lineCount = (long)xEnd - xStart + 1 + (long)zEnd - zStart + 1;
        if (lineCount > MaxGridLines) return;

        Gizmos.color = new Color(0.65f, 0.65f, 0.65f, 0.65f);
        for (int x = xStart; x <= xEnd; x++)
        {
            float wx = x * cell;
            if (wx <= r.xMin || wx >= r.xMax) continue;
            Gizmos.DrawLine(new Vector3(wx, y, r.yMin), new Vector3(wx, y, r.yMax));
        }
        for (int z = zStart; z <= zEnd; z++)
        {
            float wz = z * cell;
            if (wz <= r.yMin || wz >= r.yMax) continue;
            Gizmos.DrawLine(new Vector3(r.xMin, y, wz), new Vector3(r.xMax, y, wz));
        }
    }
}
