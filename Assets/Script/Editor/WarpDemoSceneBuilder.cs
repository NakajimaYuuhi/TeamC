#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public static class WarpDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Demo/WarpDemo.unity";
    private const string PlayerPrefabPath = "Assets/Resources/Prefab/PFB_PlayerTmp.prefab";
    private const string PlayerDataPath = "Assets/Resources/SO/Player/SO_PlayerTest.asset";
    private const string GhostMaterialPath = "Assets/Resources/Materials/M_WarpGhost.mat";

    [MenuItem("Tools/Warp/Build Warp Demo Scene")]
    public static void BuildDemoScene()
    {
        // フォルダの準備
        EnsureFolderExists("Assets/Scenes/Demo");
        EnsureFolderExists("Assets/Resources/Materials");

        // 半透明プレイヤー用マテリアル
        Material ghostMat = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
        if (ghostMat == null)
        {
            ghostMat = CreateGhostMaterial();
            AssetDatabase.CreateAsset(ghostMat, GhostMaterialPath);
        }

        // 新規シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 1. 環境光・ライト
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.0f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // 2. 通路の床と壁
        float corridorWidth = 6f;
        float corridorLength = 36f;
        float wallHeight = 3.5f;

        var environment = new GameObject("Environment");

        // 床
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(environment.transform, false);
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(corridorWidth + 4f, 1f, corridorLength + 6f);
        SetLayerRecursively(floor, LayerMask.NameToLayer("Default"));

        // 左壁
        var leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftWall.name = "LeftWall";
        leftWall.transform.SetParent(environment.transform, false);
        leftWall.transform.position = new Vector3(-(corridorWidth * 0.5f + 0.5f), wallHeight * 0.5f, 0f);
        leftWall.transform.localScale = new Vector3(1f, wallHeight, corridorLength + 6f);
        SetLayerRecursively(leftWall, LayerMask.NameToLayer("Default"));

        // 右壁
        var rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightWall.name = "RightWall";
        rightWall.transform.SetParent(environment.transform, false);
        rightWall.transform.position = new Vector3(corridorWidth * 0.5f + 0.5f, wallHeight * 0.5f, 0f);
        rightWall.transform.localScale = new Vector3(1f, wallHeight, corridorLength + 6f);
        SetLayerRecursively(rightWall, LayerMask.NameToLayer("Default"));

        // 3. 通路のNPCスポナー
        var spawnerGo = new GameObject("CrowdSpawner");
        var spawner = spawnerGo.AddComponent<WarpDemoCrowdSpawner>();
        spawner.EditorSetup(Vector3.zero, new Vector2(corridorWidth, corridorLength), 35);

        // 4. プレイヤー
        GameObject playerGo = null;
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab != null)
        {
            playerGo = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            playerGo.name = "Player";
        }
        else
        {
            playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.tag = "Player";
            var rb = playerGo.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            playerGo.AddComponent<PlayerMove>();
        }

        playerGo.transform.position = new Vector3(0f, 0.05f, -corridorLength * 0.4f);

        // PlayerWarp と WarpPenalty の追加・設定
        var warp = playerGo.GetComponent<PlayerWarp>();
        if (warp == null) warp = playerGo.AddComponent<PlayerWarp>();

        var penalty = playerGo.GetComponent<WarpPenalty>();
        if (penalty == null) penalty = playerGo.AddComponent<WarpPenalty>();

        // 固定探索範囲を設定（通路全域）
        warp.Settings.areaStart = new Vector3(-corridorWidth * 0.5f, 0f, -corridorLength * 0.5f);
        warp.Settings.areaEnd = new Vector3(corridorWidth * 0.5f, 0f, corridorLength * 0.5f);
        warp.Settings.gridCellSize = 2.0f;
        warp.Settings.npcMask = LayerMask.GetMask("NPC");
        warp.Settings.obstacleMask = LayerMask.GetMask("Default");

        // 5. カメラ
        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var cam = cameraGo.AddComponent<Camera>();
        cameraGo.AddComponent<AudioListener>();
        cameraGo.transform.position = new Vector3(0f, 5f, -corridorLength * 0.4f - 8f);
        cameraGo.transform.rotation = Quaternion.Euler(20f, 0f, 0f);

        var camCtrl = cameraGo.AddComponent<CameraController>();

        // シーンを保存
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan><b>[WarpDemo]</b> デモシーンを作成しました: {ScenePath}</color>");
        EditorUtility.DisplayDialog("Warp Demo Scene",
            $"デモシーンを作成しました！\n\n保存先: {ScenePath}\n\n「Eキー」またはゲームパッドの「□ボタン」でワープします。\n移動入力（WASD / スティック）と同じ方向に隙間がある時だけ発動します。", "OK");
    }

    private static void EnsureFolderExists(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static Material CreateGhostMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "M_WarpGhost" };
        WarpGhost.ConfigureTransparent(mat, WarpGhost.DefaultColor);
        return mat;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
#endif
