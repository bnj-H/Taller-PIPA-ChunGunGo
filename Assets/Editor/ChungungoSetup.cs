using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ChungungoSetup
{
    const string ModelPath = "Assets/_Project/Art/Characters/Chungungo/Chungungo_Rigged.fbx";
    const string AlbedoPath = "Assets/_Project/Art/Characters/Chungungo/Textures/Chungungo_Albedo.png";
    const string NormalPath = "Assets/_Project/Art/Characters/Chungungo/Textures/Chungungo_Normal.png";
    const string MrPath = "Assets/_Project/Art/Characters/Chungungo/Textures/Chungungo_MetallicRoughness.png";
    const string MaterialPath = "Assets/_Project/Art/Characters/Chungungo/Chungungo_PBR.mat";
    const string PrefabPath = "Assets/_Project/Prefabs/Characters/Chungungo.prefab";
    const string ScenePath = "Assets/_Project/Scenes/Main.unity";

    static ChungungoSetup()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Run;
    }
    [MenuItem("Tools/Chon-gu/Integrar Chungungo en Main")]
    static void RunFromMenu() { Run(); }

    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.materialImportMode != ModelImporterMaterialImportMode.None) { importer.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
        if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (importer.optimizeMeshPolygons != true) { importer.optimizeMeshPolygons = true; changed = true; }
        if (importer.optimizeMeshVertices != true) { importer.optimizeMeshVertices = true; changed = true; }
        if (importer.meshCompression != ModelImporterMeshCompression.Medium) { importer.meshCompression = ModelImporterMeshCompression.Medium; changed = true; }
        if (importer.isReadable) { importer.isReadable = false; changed = true; }
        if (changed) { importer.SaveAndReimport(); EditorApplication.delayCall += Run; return; }

        ConfigureTexture(AlbedoPath, true, TextureImporterType.Default);
        ConfigureTexture(NormalPath, false, TextureImporterType.NormalMap);
        ConfigureTexture(MrPath, false, TextureImporterType.Default);
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Characters/Chungungo/ChungungoPBR.shader");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null && shader != null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MaterialPath); }
        if (mat == null) { Debug.LogError("No se pudo crear el material Chungungo."); return; }
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));
        mat.SetTexture("_MetallicRoughnessMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MrPath));
        EditorUtility.SetDirty(mat);
        EnsurePrefab(mat);
        IntegrateIntoOpenScene();
    }

    static void EnsurePrefab(Material mat)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null) { Debug.LogError("No se pudo importar el FBX riggeado."); return; }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var oldModel = root.transform.Find("Model");
            bool replace = oldModel == null;
            if (oldModel != null)
            {
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldModel.gameObject);
                replace = sourcePath != ModelPath;
            }
            if (replace)
            {
                if (oldModel != null)
                {
                    PrefabUtility.UnpackPrefabInstance(oldModel.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    Object.DestroyImmediate(oldModel.gameObject);
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.scene);
                instance.name = "Model";
                instance.transform.SetParent(root.transform, false);
                oldModel = instance.transform;
            }
            var renderers = oldModel.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers) r.sharedMaterial = mat;
            var bounds = new Bounds(); bool first = true;
            foreach (var smr in oldModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) EncapsulateMeshBounds(ref bounds, ref first, oldModel, smr.transform, smr.sharedMesh.bounds);
            foreach (var mf in oldModel.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) EncapsulateMeshBounds(ref bounds, ref first, oldModel, mf.transform, mf.sharedMesh.bounds);
            if (!first && bounds.size.y > 0.001f)
            {
                float scale = 2.1f / bounds.size.y; // 20% above the previous 1.75 m target.
                oldModel.localScale = Vector3.one * scale;
                oldModel.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            }
            oldModel.localRotation = Quaternion.identity;
            if (root.GetComponent<ChungungoProceduralAnimation>() == null) root.AddComponent<ChungungoProceduralAnimation>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("Chungungo: prefab now uses the rigged FBX, grounded to mesh bounds and procedural bones enabled.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void EncapsulateMeshBounds(ref Bounds bounds, ref bool first, Transform model, Transform meshTransform, Bounds meshBounds)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3((i & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                                    (i & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                                    (i & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
            p = model.InverseTransformPoint(meshTransform.TransformPoint(p));
            if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
        }
    }

    static void ConfigureTexture(string path, bool srgb, TextureImporterType type)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        bool changed = false;
        if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
        if (ti.textureType != type) { ti.textureType = type; changed = true; }
        if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; changed = true; }
        if (changed) ti.SaveAndReimport();
    }

    static void IntegrateIntoOpenScene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForIntegration = !scene.IsValid() || !scene.isLoaded;
        if (openedForIntegration)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogError("No se pudo abrir Main.unity para integrar Chungungo."); return; }
        if (scene.isDirty)
        {
            if (openedForIntegration) EditorSceneManager.CloseScene(scene, true);
            Debug.LogWarning("Main.unity tiene cambios sin guardar; guarda la escena y vuelve a ejecutar Tools/Chon-gu/Integrar Chungungo en Main.");
            return;
        }
        PlayerController player = null;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            player = rootObject.GetComponentInChildren<PlayerController>(true);
            if (player != null) break;
        }
        if (player == null)
        {
            if (openedForIntegration) EditorSceneManager.CloseScene(scene, true);
            Debug.LogWarning("Prefab creado, pero no se encontró PlayerController en Main.");
            return;
        }
        foreach (Transform child in player.transform)
        {
            if (child.name == "Visual") child.gameObject.SetActive(false);
            else if (child.name == "Chungungo") Object.DestroyImmediate(child.gameObject);
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            if (openedForIntegration) EditorSceneManager.CloseScene(scene, true);
            return;
        }
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(player.transform, false);
        instance.transform.localPosition = new Vector3(0, -1, 0);
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Chungungo riggeado integrado en Main; PlayerController y su lógica se conservaron.");
        if (openedForIntegration) EditorSceneManager.CloseScene(scene, true);
    }
}




