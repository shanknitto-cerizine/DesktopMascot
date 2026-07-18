#if UNITY_EDITOR
using DesktopMascot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DesktopMascotSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/DesktopMascot.unity";

    [MenuItem("Tools/Desktop Mascot/Create or Rebuild Sample Scene")]
    public static void CreateScene()
    {
        EnsureMascotLayer();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var systems = new GameObject("Systems");
        var window = systems.AddComponent<Win32Window>();
        var app = systems.AddComponent<MascotApplication>();
        var clickThrough = systems.AddComponent<ClickThroughController>();

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.transform.position = new Vector3(0, 1.1f, -4f);
        camera.transform.LookAt(new Vector3(0, 1f, 0));

        var lightObject = new GameObject("Directional Light");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);

        var root = new GameObject("MascotRoot");
        var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        placeholder.name = "PLACEHOLDER_Replace_With_VRM_Prefab";
        placeholder.transform.SetParent(root.transform, false);
        placeholder.transform.localPosition = new Vector3(0, 1f, 0);
        placeholder.layer = Mathf.Max(0, LayerMask.NameToLayer("Mascot"));
        placeholder.AddComponent<MascotInteraction>();

        var serializedApp = new SerializedObject(app);
        serializedApp.FindProperty("window").objectReferenceValue = window;
        serializedApp.FindProperty("mascotRoot").objectReferenceValue = root.transform;
        serializedApp.ApplyModifiedPropertiesWithoutUndo();

        var serializedClick = new SerializedObject(clickThrough);
        serializedClick.FindProperty("targetCamera").objectReferenceValue = camera;
        serializedClick.FindProperty("window").objectReferenceValue = window;
        serializedClick.FindProperty("mascotLayer").intValue = LayerMask.GetMask("Mascot");
        serializedClick.ApplyModifiedPropertiesWithoutUndo();

        var interaction = placeholder.GetComponent<MascotInteraction>();
        var serializedInteraction = new SerializedObject(interaction);
        serializedInteraction.FindProperty("window").objectReferenceValue = window;
        serializedInteraction.ApplyModifiedPropertiesWithoutUndo();

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 500;
        PlayerSettings.defaultScreenHeight = 700;
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.companyName = "DesktopMascotSample";
        PlayerSettings.productName = "DesktopMascot";

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeGameObject = root;
        Debug.Log("DesktopMascot sample scene created. Replace the placeholder under MascotRoot with an imported VRM Prefab.");
    }

    private static void EnsureMascotLayer()
    {
        if (LayerMask.NameToLayer("Mascot") >= 0) return;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (var i = 8; i < 32; i++)
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = "Mascot";
            tagManager.ApplyModifiedProperties();
            return;
        }

        throw new System.InvalidOperationException("Mascot用の空きUser Layerがありません。");
    }

    [MenuItem("Tools/Desktop Mascot/Build Windows x86_64")]
    public static void BuildWindows()
    {
        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Build/DesktopMascot/DesktopMascot.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildPipeline.BuildPlayer(options);
    }
}
#endif
