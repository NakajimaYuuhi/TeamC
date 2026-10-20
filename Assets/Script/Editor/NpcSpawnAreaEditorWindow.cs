using UnityEditor;
using UnityEngine;

public class NpcSpawnAreaEditorWindow : EditorWindow
{
    private NpcSpawnAreaData data;
    private NpcSpawnAreaData draft;
    private SerializedObject dataObject;
    private Vector2 listScrollPosition;
    private Vector2 detailScrollPosition;
    private int selectedAreaIndex;
    private bool showDebug = true;

    [MenuItem("Tools/NPC配置範囲作成")]
    private static void Open()
    {
        GetWindow<NpcSpawnAreaEditorWindow>("NPC配置範囲");
    }

    private void OnEnable()
    {
        minSize = new Vector2(600f, 300f);
        SceneView.duringSceneGui += DrawAreas;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawAreas;
        if (draft != null)
        {
            DestroyImmediate(draft);
        }

        dataObject = null;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        var selectedData = (NpcSpawnAreaData)EditorGUILayout.ObjectField(
            new GUIContent("配置範囲SO", "編集する既存のSOです。未指定の場合は新しいSOを作成します。"),
            data,
            typeof(NpcSpawnAreaData),
            false);

        if (selectedData != data)
        {
            data = selectedData;
            dataObject = null;
            selectedAreaIndex = 0;
            SceneView.RepaintAll();
        }

        var currentData = GetCurrentData();
        if (dataObject == null || dataObject.targetObject != currentData)
        {
            dataObject = new SerializedObject(currentData);
        }

        EditorGUI.BeginChangeCheck();
        showDebug = EditorGUILayout.Toggle("SceneViewに範囲を表示", showDebug);
        dataObject.Update();
        DrawAreaFields(dataObject.FindProperty("areas"));
        var hasChanges = EditorGUI.EndChangeCheck();
        var hasDataChanges = dataObject.ApplyModifiedProperties();
        if (hasChanges || hasDataChanges)
        {
            SceneView.RepaintAll();
        }

        var hasValidSizes = HasValidSizes(currentData);
        if (!hasValidSizes)
        {
            EditorGUILayout.HelpBox("Boxの各サイズには0以上の値を指定してください。", MessageType.Error);
        }

        using (new EditorGUI.DisabledScope(!hasValidSizes))
        {
            if (GUILayout.Button(data == null ? "配置範囲SOを作成" : "配置範囲SOを保存"))
            {
                SaveData(currentData);
            }
        }
    }

    private void DrawAreaFields(SerializedProperty areas)
    {
        selectedAreaIndex = areas.arraySize == 0
            ? -1
            : Mathf.Clamp(selectedAreaIndex, 0, areas.arraySize - 1);

        using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(200f)))
            {
                EditorGUILayout.LabelField("Box一覧", EditorStyles.boldLabel);
                listScrollPosition = EditorGUILayout.BeginScrollView(listScrollPosition);
                for (var index = 0; index < areas.arraySize; index++)
                {
                    var tag = areas.GetArrayElementAtIndex(index).FindPropertyRelative("tag");
                    var label = $"Box {index} / {tag.enumDisplayNames[tag.enumValueIndex]}";
                    if (GUILayout.Toggle(selectedAreaIndex == index, label, "Button"))
                    {
                        selectedAreaIndex = index;
                    }
                }

                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Boxを追加"))
                {
                    selectedAreaIndex = areas.arraySize;
                    areas.arraySize++;
                    var area = areas.GetArrayElementAtIndex(selectedAreaIndex);
                    area.FindPropertyRelative("center").vector3Value = Vector3.zero;
                    area.FindPropertyRelative("size").vector3Value = Vector3.one;
                    area.FindPropertyRelative("tag").enumValueIndex = 0;
                }

                using (new EditorGUI.DisabledScope(selectedAreaIndex < 0))
                {
                    if (GUILayout.Button("選択中のBoxを削除"))
                    {
                        areas.DeleteArrayElementAtIndex(selectedAreaIndex);
                        selectedAreaIndex = Mathf.Min(selectedAreaIndex, areas.arraySize - 1);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                if (selectedAreaIndex < 0)
                {
                    EditorGUILayout.HelpBox("左側の「Boxを追加」から配置範囲を作成してください。", MessageType.Info);
                    return;
                }

                var area = areas.GetArrayElementAtIndex(selectedAreaIndex);
                var center = area.FindPropertyRelative("center");
                var size = area.FindPropertyRelative("size");
                var tag = area.FindPropertyRelative("tag");

                EditorGUILayout.LabelField($"Box {selectedAreaIndex} の設定", EditorStyles.boldLabel);
                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition);
                center.vector3Value = EditorGUILayout.Vector3Field(
                    new GUIContent("中心位置", "Boxの中心のワールド座標をXYZで直接指定します。"),
                    center.vector3Value);
                size.vector3Value = EditorGUILayout.Vector3Field(
                    new GUIContent("サイズ", "XYZ方向の全幅です。各成分に0以上を指定してください。"),
                    size.vector3Value);
                EditorGUILayout.PropertyField(tag);
                EditorGUILayout.EndScrollView();
            }
        }
    }

    private NpcSpawnAreaData GetCurrentData()
    {
        if (data != null)
        {
            return data;
        }

        if (draft == null)
        {
            draft = CreateInstance<NpcSpawnAreaData>();
            draft.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        }

        return draft;
    }

    private static bool HasValidSizes(NpcSpawnAreaData currentData)
    {
        foreach (var area in currentData.Areas)
        {
            var size = area.Size;
            if (size.x < 0f || size.y < 0f || size.z < 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void SaveData(NpcSpawnAreaData currentData)
    {
        if (data == null)
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "NPC配置範囲を保存",
                "NPC配置範囲",
                "asset",
                "NPC配置範囲SOの保存先を選択してください。");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            data = Instantiate(currentData);
            data.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(data, path);
            dataObject = null;
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = data;
        EditorGUIUtility.PingObject(data);
    }

    private void DrawAreas(SceneView sceneView)
    {
        if (!showDebug)
        {
            return;
        }

        var currentData = GetCurrentData();
        var previousColor = Handles.color;
        var labelStyle = new GUIStyle(EditorStyles.label);
        for (var index = 0; index < currentData.Areas.Count; index++)
        {
            var area = currentData.GetArea(index);
            var size = area.Size;
            if (size.x < 0f || size.y < 0f || size.z < 0f)
            {
                continue;
            }

            switch (area.Tag)
            {
                case NpcSpawnAreaTag.WalkArea:
                    Handles.color = new Color(0.3f, 1f, 0.4f);
                    break;
                case NpcSpawnAreaTag.StandArea:
                    Handles.color = new Color(0.3f, 0.7f, 1f);
                    break;
                case NpcSpawnAreaTag.GroupArea:
                    Handles.color = new Color(1f, 0.6f, 0.2f);
                    break;
            }

            labelStyle.normal.textColor = Handles.color;
            Handles.DrawWireCube(area.Center, size);
            Handles.Label(area.Center, $"Box {index} / {area.Tag}", labelStyle);
        }

        Handles.color = previousColor;
    }
}
