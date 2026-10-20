//_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/
// file : SOManagerWindow.cs
// brief: Assets内のScriptableObjectを一覧表示し、選択したSOを編集する管理Windowです。
// athor : 山本郁也
// data 2026/05/13
//_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/_/
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets内のScriptableObjectを一覧表示し、選択したSOを編集する管理Windowです。
/// </summary>
public sealed class SOManagerWindow : EditorWindow
{
    private readonly List<ScriptableObject> allScriptableObjects = new();
    private readonly List<Type> scriptableObjectTypes = new();

    private ScriptableObject selectedObject;
    private Editor selectedEditor;

    private Vector2 listScroll;
    private Vector2 inspectorScroll;

    private int selectedTypeIndex;
    private string searchText = "";
    private string searchFolder = "Assets";

    [MenuItem("Tools/SO Manager")]
    private static void Open()
    {
        GetWindow<SOManagerWindow>("SO Manager");
    }

    private void OnEnable()
    {
        RefreshTypes();
        RefreshAssets();
    }

    private void OnDisable()
    {
        DestroySelectedEditor();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawObjectList();
            DrawInspectorArea();
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            searchFolder = EditorGUILayout.TextField("Search Folder", searchFolder);

            using (new EditorGUILayout.HorizontalScope())
            {
                searchText = EditorGUILayout.TextField("Search", searchText);

                if (GUILayout.Button("Refresh", GUILayout.Width(80)))
                {
                    RefreshTypes();
                    RefreshAssets();
                }

                if (GUILayout.Button("Save", GUILayout.Width(80)))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            DrawTypePopup();
        }
    }

    private void DrawTypePopup()
    {
        List<string> typeNames = new();

        typeNames.Add("All");

        foreach (Type type in scriptableObjectTypes)
        {
            typeNames.Add(type.FullName);
        }

        selectedTypeIndex = EditorGUILayout.Popup("Type", selectedTypeIndex, typeNames.ToArray());
    }

    private void DrawObjectList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(330)))
        {
            EditorGUILayout.LabelField("ScriptableObjects", EditorStyles.boldLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            foreach (ScriptableObject so in GetFilteredObjects())
            {
                if (so == null)
                {
                    continue;
                }

                bool isSelected = so == selectedObject;

                GUIStyle style = isSelected ? EditorStyles.helpBox : EditorStyles.label;

                using (new EditorGUILayout.HorizontalScope(style))
                {
                    if (GUILayout.Button(so.name, EditorStyles.label))
                    {
                        SelectObject(so);
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(45)))
                    {
                        EditorGUIUtility.PingObject(so);
                        Selection.activeObject = so;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawInspectorArea()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("左の一覧からScriptableObjectを選択してください。", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField("Selected", selectedObject, selectedObject.GetType(), false);

                if (GUILayout.Button("Select", GUILayout.Width(70)))
                {
                    Selection.activeObject = selectedObject;
                }

                if (GUILayout.Button("Ping", GUILayout.Width(70)))
                {
                    EditorGUIUtility.PingObject(selectedObject);
                }
            }

            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

            if (selectedEditor != null)
            {
                EditorGUI.BeginChangeCheck();

                selectedEditor.OnInspectorGUI();

                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(selectedObject);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void RefreshTypes()
    {
        scriptableObjectTypes.Clear();

        Type baseType = typeof(ScriptableObject);

        foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
        {
            if (type.IsAbstract)
            {
                continue;
            }

            if (type == baseType)
            {
                continue;
            }

            scriptableObjectTypes.Add(type);
        }

        scriptableObjectTypes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
    }

    private void RefreshAssets()
    {
        allScriptableObjects.Clear();

        string[] folders = string.IsNullOrEmpty(searchFolder)
            ? new[] { "Assets" }
            : new[] { searchFolder };

        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", folders);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

            if (so == null)
            {
                continue;
            }

            allScriptableObjects.Add(so);
        }

        allScriptableObjects.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
    }

    private IEnumerable<ScriptableObject> GetFilteredObjects()
    {
        IEnumerable<ScriptableObject> result = allScriptableObjects;

        if (selectedTypeIndex > 0)
        {
            Type selectedType = scriptableObjectTypes[selectedTypeIndex - 1];

            result = result.Where(so => so != null && selectedType.IsAssignableFrom(so.GetType()));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            result = result.Where(so =>
                so != null &&
                so.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            );
        }

        return result;
    }

    private void SelectObject(ScriptableObject so)
    {
        if (selectedObject == so)
        {
            return;
        }

        selectedObject = so;

        DestroySelectedEditor();

        if (selectedObject != null)
        {
            selectedEditor = Editor.CreateEditor(selectedObject);
        }
    }

    private void DestroySelectedEditor()
    {
        if (selectedEditor == null)
        {
            return;
        }

        DestroyImmediate(selectedEditor);
        selectedEditor = null;
    }
}
