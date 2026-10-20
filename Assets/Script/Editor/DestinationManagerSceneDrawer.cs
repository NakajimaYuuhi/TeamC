using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DestinationManager))]
public class DestinationManagerSceneDrawer : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        var destinationManager = (DestinationManager)target;

        var previousColor = Handles.color;
        Handles.color = Color.yellow;
        foreach (var destination in destinationManager.Destinations)
        {
            if (destination == null
                || destination.AchievementSizeX <= 0f
                || destination.AchievementSizeZ <= 0f)
            {
                continue;
            }

            var position = destination.AchievementCenterPosition;
            var halfSizeX = destination.AchievementSizeX * 0.5f;
            var halfSizeZ = destination.AchievementSizeZ * 0.5f;
            var corner1 = position + new Vector3(-halfSizeX, 0f, -halfSizeZ);
            var corner2 = position + new Vector3(-halfSizeX, 0f, halfSizeZ);
            var corner3 = position + new Vector3(halfSizeX, 0f, halfSizeZ);
            var corner4 = position + new Vector3(halfSizeX, 0f, -halfSizeZ);

            Handles.DrawLine(corner1, corner2);
            Handles.DrawLine(corner2, corner3);
            Handles.DrawLine(corner3, corner4);
            Handles.DrawLine(corner4, corner1);
        }
        Handles.color = previousColor;
    }
}
