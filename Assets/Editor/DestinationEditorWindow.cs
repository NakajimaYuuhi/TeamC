using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class DestinationEditorWindow : EditorWindow
{
    private DestinationManager destinationManager;
    private Transform destinationTransform;
    private Vector3 destinationPosition;
    private float achievementCenterOffsetX;
    private float achievementCenterOffsetZ;
    private float targetTime = 60f;
    private float achievementSizeX = 1f;
    private float achievementSizeZ = 1f;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DrawAchievementRange;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawAchievementRange;
        SceneView.RepaintAll();
    }

    [MenuItem("ツール/目的地ステージ作成")]
    private static void Open()
    {
        GetWindow<DestinationEditorWindow>("目的地作成");
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();

        destinationManager = (DestinationManager)EditorGUILayout.ObjectField(
            "目的地マネージャー",
            destinationManager,
            typeof(DestinationManager),
            true);

        EditorGUILayout.Space();
        destinationTransform = (Transform)EditorGUILayout.ObjectField(
            "目的地オブジェクト",
            destinationTransform,
            typeof(Transform),
            true);

        if (destinationTransform == null)
        {
            destinationPosition = EditorGUILayout.Vector3Field("目的地座標", destinationPosition);
        }
        else
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("目的地座標", destinationTransform.position);
            }
        }

        achievementCenterOffsetX = EditorGUILayout.FloatField(
            "達成範囲中心オフセット X",
            achievementCenterOffsetX);
        achievementCenterOffsetZ = EditorGUILayout.FloatField(
            "達成範囲中心オフセット Z",
            achievementCenterOffsetZ);
        targetTime = EditorGUILayout.FloatField("目標時間（秒）", targetTime);
        achievementSizeX = EditorGUILayout.FloatField("達成範囲 X", achievementSizeX);
        achievementSizeZ = EditorGUILayout.FloatField("達成範囲 Z", achievementSizeZ);

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!CanCreate()))
        {
            if (GUILayout.Button("目的地データを作成"))
            {
                CreateDestinationData();
            }
        }

        if (destinationManager == null)
        {
            EditorGUILayout.HelpBox("目的地マネージャーを指定してください。", MessageType.Info);
        }
        else if (targetTime < 0f || achievementSizeX < 0f || achievementSizeZ < 0f)
        {
            EditorGUILayout.HelpBox("目標時間と達成範囲には0以上の値を指定してください。", MessageType.Error);
        }
    }

    private void DrawAchievementRange(SceneView sceneView)
    {
        if (achievementSizeX <= 0f || achievementSizeZ <= 0f)
        {
            return;
        }

        var destination = destinationTransform != null
            ? destinationTransform.position
            : destinationPosition;
        var position = destination
            + new Vector3(achievementCenterOffsetX, 0f, achievementCenterOffsetZ);

        var halfSizeX = achievementSizeX * 0.5f;
        var halfSizeZ = achievementSizeZ * 0.5f;
        var corner1 = position + new Vector3(-halfSizeX, 0f, -halfSizeZ);
        var corner2 = position + new Vector3(-halfSizeX, 0f, halfSizeZ);
        var corner3 = position + new Vector3(halfSizeX, 0f, halfSizeZ);
        var corner4 = position + new Vector3(halfSizeX, 0f, -halfSizeZ);

        var previousColor = Handles.color;
        Handles.color = Color.yellow;
        Handles.DrawLine(corner1, corner2);
        Handles.DrawLine(corner2, corner3);
        Handles.DrawLine(corner3, corner4);
        Handles.DrawLine(corner4, corner1);
        Handles.color = previousColor;
    }

    private bool CanCreate()
    {
        return destinationManager != null
            && targetTime >= 0f
            && achievementSizeX >= 0f
            && achievementSizeZ >= 0f;
    }

    private void CreateDestinationData()
    {
        var path = EditorUtility.SaveFilePanelInProject(
            "目的地データを保存",
            "目的地データ",
            "asset",
            "目的地データの保存先を選択してください。");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var data = CreateInstance<DestinationData>();
        var dataObject = new SerializedObject(data);
        dataObject.FindProperty("destinationPosition").vector3Value = destinationTransform != null
            ? destinationTransform.position
            : destinationPosition;
        dataObject.FindProperty("achievementCenterOffsetX").floatValue = achievementCenterOffsetX;
        dataObject.FindProperty("achievementCenterOffsetZ").floatValue = achievementCenterOffsetZ;
        dataObject.FindProperty("targetTime").floatValue = targetTime;
        dataObject.FindProperty("achievementSizeX").floatValue = achievementSizeX;
        dataObject.FindProperty("achievementSizeZ").floatValue = achievementSizeZ;
        dataObject.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(data, path);

        Undo.RecordObject(destinationManager, "目的地データを登録");
        var managerObject = new SerializedObject(destinationManager);
        var destinations = managerObject.FindProperty("destinations");
        var newIndex = destinations.arraySize;
        destinations.InsertArrayElementAtIndex(newIndex);
        destinations.GetArrayElementAtIndex(newIndex).objectReferenceValue = data;
        managerObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(destinationManager);
        if (destinationManager.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(destinationManager.gameObject.scene);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
    }
}
