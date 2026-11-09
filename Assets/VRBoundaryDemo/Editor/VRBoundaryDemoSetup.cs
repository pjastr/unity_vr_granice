using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace VRBoundaryDemo.EditorTools
{
    /// <summary>
    /// Menu "VR Boundary Demo" w edytorze:
    ///  1. Konfiguruj projekt dla Meta Quest - Android/Quest, URP, kontrolery, passthrough, szybsza kompilacja IL2CPP.
    ///  2. Zbuduj scenę demo - otoczenie z prymitywów, panele, logika granic.
    ///     Rig kamery, kontrolery i passthrough dodaje się z okna Building Blocks.
    ///  3. Sprawdź bloki w scenie - raport, których Building Blocks brakuje (+ Floor Level).
    ///  4. Build And Run (USB) - buduje APK (Development + Script Debugging) i uruchamia na goglach.
    /// </summary>
    public static class VRBoundaryDemoSetup
    {
        public const string Root = "Assets/VRBoundaryDemo";
        public const string ScenePath = Root + "/Scenes/BoundaryDemo.unity";
        const string SettingsFolder = Root + "/Settings";
        const string MaterialsFolder = Root + "/Materials";
        const string ApkPath = "Builds/VRBoundaryDemo.apk";
        const string Title = "VR Boundary Demo";

        /// <summary>Obiekty główne tworzone przez skrypt (przy odświeżaniu sceny są usuwane i tworzone od nowa).</summary>
        static readonly string[] DemoRootNames = { "Directional Light", "Environment", "Panels", "Boundaries", "Demo" };

        static readonly Vector3 HeadPos = new Vector3(0f, 1.6f, 0f);
        static readonly Color PanelBg = new Color(0.06f, 0.07f, 0.1f, 0.9f);

        // =====================================================================
        // 1. KONFIGURACJA PROJEKTU
        // =====================================================================

        [MenuItem("VR Boundary Demo/1. Konfiguruj projekt dla Meta Quest", false, 1)]
        public static void ConfigureProject()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = "VRBoundaryDemo";
            PlayerSettings.productName = "VR Boundary Demo";
            PlayerSettings.SetApplicationIdentifier(android, "com.vrboundarydemo.quest");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32; // Android 12L - minimum dla Horizon OS
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // PRZYSPIESZENIE KOMPILACJI:
            // Player Settings > Other Settings > IL2CPP Code Generation = "Faster (smaller) builds".
            // IL2CPP generuje mniej kodu C++ (współdzielone implementacje generyków), więc kompilacja
            // natywna trwa wyraźnie krócej. Koszt: minimalnie wolniejsze działanie kodu generycznego -
            // dla tego demo niezauważalne.
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);

            // Build do debugowania przez USB.
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = true;
            EditorUserBuildSettings.buildAppBundle = false;

            EnsureUrp();
            SetProjectConfig();
            if (File.Exists(ScenePath)) AddSceneToBuild();
            AssetDatabase.SaveAssets();

            const string summary =
                "Ustawienia Quest zapisane:\n" +
                "• IL2CPP, ARM64, min. API 32, Vulkan, Linear, URP\n" +
                "• IL2CPP Code Generation = Faster (smaller) builds (szybsza kompilacja)\n" +
                "• tylko kontrolery (Hand Tracking Support = Controllers Only), Passthrough Support = Supported\n";
            const string next =
                "\nDalej:\n" +
                "• Project Settings > XR Plug-in Management > zakładka Android: zaznacz OpenXR oraz grupę \"Meta XR\".\n" +
                "• Meta > Tools > Project Setup Tool: \"Fix All\" (Android).\n" +
                "• VR Boundary Demo > 2. Zbuduj scenę demo.";

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (EditorUtility.DisplayDialog(Title,
                        summary + "\nPrzełączyć platformę na Android teraz? Może to potrwać kilka minut.\n" + next,
                        "Przełącz na Android", "Później"))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }
            else
            {
                EditorUtility.DisplayDialog(Title, summary + next, "OK");
            }
        }

        [MenuItem("VR Boundary Demo/Kompilacja IL2CPP: przełącz szybsze buildy ↔ szybsze działanie", false, 30)]
        public static void ToggleIl2CppCodeGeneration()
        {
            var android = NamedBuildTarget.Android;
            bool fastBuilds = PlayerSettings.GetIl2CppCodeGeneration(android) == Il2CppCodeGeneration.OptimizeSize;
            var next = fastBuilds ? Il2CppCodeGeneration.OptimizeSpeed : Il2CppCodeGeneration.OptimizeSize;
            PlayerSettings.SetIl2CppCodeGeneration(android, next);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog(Title,
                next == Il2CppCodeGeneration.OptimizeSize
                    ? "IL2CPP Code Generation = Faster (smaller) builds\nKrótsza kompilacja - zalecane podczas pracy nad projektem."
                    : "IL2CPP Code Generation = Faster runtime\nDłuższa kompilacja, nieco szybszy kod - do wersji końcowej.",
                "OK");
        }

        // =====================================================================
        // 2. BUDOWA / ODŚWIEŻENIE SCENY
        // =====================================================================

        [MenuItem("VR Boundary Demo/2. Zbuduj scenę demo", false, 2)]
        public static void BuildDemoScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool exists = File.Exists(ScenePath);
            if (exists && !EditorUtility.DisplayDialog(Title,
                    "Scena już istnieje. Odświeżyć zawartość demo?\n\n" +
                    "Building Blocks (Camera Rig, Controller Tracking, Passthrough) zostaną zachowane.",
                    "Odśwież", "Anuluj"))
                return;

            EnsureUrp();
            Material lit = EnsureLitMaterial();
            Material transparent = EnsureTransparentMaterial();
            DemoUI.LitMaterial = lit;
            DemoUI.TransparentMaterial = transparent;

            try
            {
                UnityEngine.SceneManagement.Scene scene;
                if (exists)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath);
                    foreach (var go in scene.GetRootGameObjects())
                        if (DemoRootNames.Contains(go.name)) Object.DestroyImmediate(go);
                }
                else
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }

                BuildLighting();
                var env = new GameObject("Environment");
                BuildEnvironment(env.transform);

                // Granice: systemowa (Play Area) i własna (rysowana kontrolerem).
                var boundaries = new GameObject("Boundaries").transform;

                var sysGo = new GameObject("SystemBoundary");
                sysGo.transform.SetParent(boundaries, false);
                var system = sysGo.AddComponent<SystemBoundary>();
                var sysVisual = sysGo.AddComponent<BoundaryVisual>();
                sysVisual.color = new Color(0.2f, 0.85f, 1f);

                var customGo = new GameObject("CustomBoundary");
                customGo.transform.SetParent(boundaries, false);
                var custom = customGo.AddComponent<CustomBoundary>(); // dodaje też BoundaryVisual
                var customVisual = customGo.GetComponent<BoundaryVisual>();
                customVisual.color = new Color(1f, 0.6f, 0.15f);
                customVisual.maxWallAlpha = 0.45f;

                var panels = new GameObject("Panels");
                BuildPanels(panels.transform);

                var demo = new GameObject("Demo");
                var res = demo.AddComponent<DemoResources>();
                res.litMaterial = lit;
                res.transparentMaterial = transparent;
                demo.AddComponent<Haptics>();

                var monitor = demo.AddComponent<BoundaryMonitor>();
                monitor.systemBoundary = system;
                monitor.customBoundary = custom;

                var pt = demo.AddComponent<PassthroughBoundaryControl>();
                pt.environmentRoot = env;

                demo.AddComponent<PlayerLocomotion>();

                var actions = demo.AddComponent<DemoActions>();
                actions.systemBoundary = system;
                actions.systemVisual = sysVisual;
                actions.customBoundary = custom;
                actions.monitor = monitor;
                actions.passthrough = pt;
                actions.panelsRoot = panels;

                SetProjectConfig();

                EnsureFolder(Root + "/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild();
            }
            finally
            {
                DemoUI.LitMaterial = null;
                DemoUI.TransparentMaterial = null;
            }

            string report = CheckScene(true, out bool ready);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(Title,
                "Scena gotowa: " + ScenePath + "\n\n" + report +
                (ready ? "" : "\n\nDodaj brakujące bloki: Meta > Tools > Building Blocks (INSTRUKCJA.md, krok 5)."),
                "OK");
        }

        // =====================================================================
        // 3. SPRAWDZENIE BLOKÓW
        // =====================================================================

        [MenuItem("VR Boundary Demo/3. Sprawdź bloki w scenie", false, 3)]
        public static void CheckSceneMenu()
        {
            string report = CheckScene(true, out bool _);
            if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog(Title + " – Building Blocks", report, "OK");
        }

        /// <summary>
        /// Sprawdza otwartą scenę: wymagane Building Blocks, duplikaty, zbędne kamery.
        /// Przy fix = true ustawia też Tracking Origin = Floor Level i włącza passthrough w OVRManager.
        /// </summary>
        static string CheckScene(bool fix, out bool ready)
        {
            var sb = new StringBuilder();

            var rigs = Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool rigOk = rigs.Length == 1;
            Line(sb, rigOk, "Camera Rig",
                rigs.Length == 0 ? "brak – dodaj blok Camera Rig" :
                rigs.Length > 1 ? $"{rigs.Length} rigi – zostaw jeden" : "OK");

            Type helperType = FindType("OVRControllerHelper");
            int controllers = helperType != null
                ? Object.FindObjectsByType(helperType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length
                : 0;
            Line(sb, controllers > 0, "Controller Tracking", controllers > 0 ? "OK" : "brak modeli kontrolerów – dodaj blok Controller Tracking (zalecane)");

            var layers = Object.FindObjectsByType<OVRPassthroughLayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Line(sb, layers.Length > 0, "Passthrough",
                layers.Length > 0 ? "OK" : "brak – potrzebny do przycisków X/Y (passthrough i ukrywanie granicy)");

            var strayCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => rigs.All(r => !c.transform.IsChildOf(r.transform))).ToList();
            if (strayCameras.Count > 0)
                Line(sb, false, "Main Camera", "w scenie jest kamera poza rigiem (" + strayCameras[0].name + ") – usuń ją");

            var manager = Object.FindAnyObjectByType<OVRManager>();
            if (manager != null && fix)
            {
                SetEnum(manager, "FloorLevel", false, "_trackingOriginType", "m_TrackingOriginType", "trackingOriginType");
                if (layers.Length > 0 && !manager.isInsightPassthroughEnabled)
                {
                    Undo.RecordObject(manager, "Enable Passthrough");
                    manager.isInsightPassthroughEnabled = true;
                    EditorUtility.SetDirty(manager);
                }
                SetProjectConfig();
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            if (manager == null && rigs.Length > 0)
                Line(sb, false, "OVRManager", "brak – dodaj blok Camera Rig ponownie");

            ready = rigOk && strayCameras.Count == 0;
            sb.Append(ready ? "\nScena jest gotowa do uruchomienia." : "\nScena wymaga uzupełnienia.");
            return sb.ToString();
        }

        static void Line(StringBuilder sb, bool ok, string what, string info) =>
            sb.Append(ok ? "[OK]    " : "[BRAK] ").Append(what).Append(" – ").AppendLine(info);

        // =====================================================================
        // 4. BUILD AND RUN
        // =====================================================================

        [MenuItem("VR Boundary Demo/4. Build And Run (USB)", false, 4)]
        public static void BuildAndRun()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog(Title, "Najpierw zbuduj scenę (krok 2).", "OK");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog(Title,
                    "Aktywna platforma to nie Android. Uruchom krok 1 i przełącz platformę (albo File > Build Profiles).", "OK");
                return;
            }
            if (EditorSceneManager.GetActiveScene().path == ScenePath)
            {
                string report = CheckScene(false, out bool ready);
                if (!ready && !EditorUtility.DisplayDialog(Title, report + "\n\nZbudować mimo to?", "Buduj", "Anuluj"))
                    return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ApkPath) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.AutoRunPlayer | BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var codeGen = PlayerSettings.GetIl2CppCodeGeneration(NamedBuildTarget.Android);
            BuildReport buildReport = BuildPipeline.BuildPlayer(options);
            var summary = buildReport.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[VRBoundaryDemo] Build OK w {summary.totalTime.TotalSeconds:0} s " +
                          $"({summary.totalSize / (1024f * 1024f):0.0} MB, IL2CPP: {codeGen}) -> {ApkPath}. Aplikacja uruchamia się na goglach.");
            else
                Debug.LogError($"[VRBoundaryDemo] Build nieudany: {summary.result}. Sprawdź Console (błędy) i czy gogle są widoczne w Build Profiles > Run Device.");
        }

        [MenuItem("VR Boundary Demo/Otwórz scenę demo", false, 20)]
        public static void OpenDemoScene()
        {
            if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog(Title, "Scena jeszcze nie istnieje (krok 2).", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("VR Boundary Demo/Otwórz instrukcję (INSTRUKCJA.md)", false, 21)]
        public static void OpenDocs()
        {
            string path = Path.GetFullPath("INSTRUKCJA.md");
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("Nie znaleziono " + path);
        }

        // =====================================================================
        // URP + MATERIAŁY
        // =====================================================================

        static UniversalRenderPipelineAsset EnsureUrp()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                EnsureFolder(SettingsFolder);
                string assetPath = SettingsFolder + "/VRBoundaryDemo_URP.asset";
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                if (urp == null)
                {
                    var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, SettingsFolder + "/VRBoundaryDemo_URP_Renderer.asset");
                    urp = UniversalRenderPipelineAsset.Create(rendererData);
                    AssetDatabase.CreateAsset(urp, assetPath);
                }
                GraphicsSettings.defaultRenderPipeline = urp;
            }

            // Ustawienia przyjazne dla Quest (mobilne GPU, rendering stereo).
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            urp.renderScale = 1f;
            urp.shadowDistance = 15f;
            EditorUtility.SetDirty(urp);

            // Poziomy jakości bez własnego potoku -> używają domyślnego (naszego URP).
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != urp)
                    QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            return urp;
        }

        static Material EnsureLitMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/DemoLit.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader) { name = "DemoLit" };
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Półprzezroczysty, dwustronny URP/Unlit (alfa z _BaseColor) - na ściany granic.</summary>
        static Material EnsureTransparentMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/BoundaryWall.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                mat = new Material(shader) { name = "BoundaryWall" };
                AssetDatabase.CreateAsset(mat, path);
            }

            SetFloat(mat, "_Surface", 1f);   // Transparent
            SetFloat(mat, "_Blend", 0f);     // Alpha
            SetFloat(mat, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(mat, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(mat, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(mat, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(mat, "_ZWrite", 0f);
            SetFloat(mat, "_Cull", (float)CullMode.Off);
            SetFloat(mat, "_AlphaClip", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f, 0.3f));
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static void SetFloat(Material m, string prop, float v)
        {
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }

        // =====================================================================
        // USTAWIENIA META (OVRProjectConfig)
        // =====================================================================

        /// <summary>
        /// OVRProjectConfig (Quest Features w OVRManager), przez refleksję - odporne na zmiany API:
        ///  - Hand Tracking Support = Controllers Only (demo obsługiwane wyłącznie kontrolerami),
        ///  - Passthrough Support = Supported (jeśli było None) - wymagane przez Boundary API.
        /// </summary>
        static void SetProjectConfig()
        {
            try
            {
                Type t = FindType("OVRProjectConfig");
                if (t == null) { Debug.LogWarning("[VRBoundaryDemo] Brak OVRProjectConfig - ustaw Quest Features ręcznie w OVRManager."); return; }

                object config = null;
                var getter = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetProjectConfig");
                if (getter != null) config = getter.Invoke(null, DefaultArgs(getter));
                if (config == null)
                {
                    var prop = t.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (prop != null) config = prop.GetValue(null);
                }
                if (config == null) { Debug.LogWarning("[VRBoundaryDemo] Nie udało się pobrać OVRProjectConfig."); return; }

                bool hands = SetConfigEnum(t, config, "handtrackingsupport", "ControllersOnly", false);
                bool pt = SetConfigEnum(t, config, "passthroughsupport", "Supported", true);
                if (!hands) Debug.LogWarning("[VRBoundaryDemo] Ustaw ręcznie: OVRManager > Quest Features > Hand Tracking Support = Controllers Only.");
                if (!pt) Debug.LogWarning("[VRBoundaryDemo] Sprawdź ręcznie: OVRManager > Quest Features > Passthrough Support ≠ None.");

                var commit = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "CommitProjectConfig" && m.GetParameters().Length == 1);
                if (commit != null) commit.Invoke(null, new[] { config });
                else if (config is Object unityObj) { EditorUtility.SetDirty(unityObj); AssetDatabase.SaveAssets(); }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VRBoundaryDemo] Ustawienie Quest Features nie powiodło się: " + e.Message);
            }
        }

        /// <summary>Ustawia pola-enumy, których nazwa zawiera token. onlyFromNone: zmienia tylko wartość "None".</summary>
        static bool SetConfigEnum(Type t, object config, string token, string valueName, bool onlyFromNone)
        {
            bool found = false;
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (!f.FieldType.IsEnum) continue;
                if (f.Name.ToLowerInvariant().Replace("_", "").IndexOf(token, StringComparison.Ordinal) < 0) continue;
                if (!Enum.GetNames(f.FieldType).Contains(valueName)) continue;
                found = true;
                if (onlyFromNone && f.GetValue(config).ToString() != "None") continue;
                f.SetValue(config, Enum.Parse(f.FieldType, valueName));
            }
            return found;
        }

        // =====================================================================
        // OTOCZENIE
        // =====================================================================

        static void BuildLighting()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.shadows = LightShadows.None;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.6f, 0.7f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.4f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.22f);
        }

        static void BuildEnvironment(Transform env)
        {
            var floor = DemoUI.CreatePrimitive(PrimitiveType.Plane, "Floor", env, Vector3.zero, new Vector3(2f, 1f, 2f), new Color(0.24f, 0.26f, 0.3f));
            DemoUI.DisableShadows(floor);

            // Siatka co 1 m - ułatwia ocenę rozmiaru obszaru gry.
            var grid = new GameObject("Grid").transform;
            grid.SetParent(env, false);
            Color line = new Color(0.36f, 0.39f, 0.45f);
            for (int i = -8; i <= 8; i++)
            {
                DemoUI.CreatePrimitive(PrimitiveType.Cube, "LineX", grid, new Vector3(i, 0.002f, 0f), new Vector3(0.01f, 0.002f, 16f), line, false);
                DemoUI.CreatePrimitive(PrimitiveType.Cube, "LineZ", grid, new Vector3(0f, 0.002f, i), new Vector3(16f, 0.002f, 0.01f), line, false);
            }
            DemoUI.DisableShadows(grid.gameObject);

            var start = DemoUI.CreatePrimitive(PrimitiveType.Cylinder, "StartMarker", env, new Vector3(0f, 0.003f, 0f),
                new Vector3(0.5f, 0.002f, 0.5f), new Color(0.15f, 0.55f, 0.55f), false);
            DemoUI.DisableShadows(start);

            // Kilka słupków jako punkty odniesienia w wirtualnej scenie (widać, że granica jedzie z graczem).
            Color pillar = new Color(0.45f, 0.42f, 0.5f);
            foreach (var p in new[] { new Vector3(-3f, 0f, 3f), new Vector3(3f, 0f, 3f), new Vector3(-3f, 0f, -3f), new Vector3(3f, 0f, -3f) })
                DemoUI.CreatePrimitive(PrimitiveType.Cube, "Pillar", env, p + Vector3.up * 1f, new Vector3(0.3f, 2f, 0.3f), pillar);
        }

        // =====================================================================
        // PANELE
        // =====================================================================

        static void BuildPanels(Transform root)
        {
            // --- Stan granic (z lewej) ---
            Vector3 iPos = new Vector3(-1.5f, 1.45f, 1.7f);
            var info = DemoUI.CreatePanel("InfoPanel", root, iPos, DemoUI.FacePoint(iPos, HeadPos), new Vector2(1000f, 1000f), PanelBg);
            var infoText = DemoUI.CreateFillText(info, "Text", 30f, 27, TextAnchor.UpperLeft, Color.white, "GRANICE");
            info.gameObject.AddComponent<BoundaryInfoPanel>().text = infoText;

            // --- Pomoc (na wprost) ---
            Vector3 helpPos = new Vector3(0f, 1.6f, 2.4f);
            var help = DemoUI.CreatePanel("HelpPanel", root, helpPos, DemoUI.FacePoint(helpPos, HeadPos), new Vector2(1400f, 1000f), PanelBg);
            DemoUI.CreateFillText(help, "Text", 30f, 28, TextAnchor.UpperLeft, Color.white, HelpText);

            // --- Dziennik zdarzeń (z prawej) ---
            Vector3 logPos = new Vector3(1.5f, 1.45f, 1.7f);
            var log = DemoUI.CreatePanel("LogPanel", root, logPos, DemoUI.FacePoint(logPos, HeadPos), new Vector2(1000f, 1000f), PanelBg);
            DemoUI.CreateText(log, "Header", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -70f), new Vector2(-30f, -15f), 30, TextAnchor.UpperLeft, new Color(0.6f, 0.85f, 1f),
                "<b>DZIENNIK ZDARZEŃ</b>  (logcat: [VRBoundaryDemo])");
            var logText = DemoUI.CreateText(log, "Lines", Vector2.zero, Vector2.one, new Vector2(30f, 20f), new Vector2(-30f, -80f), 25, TextAnchor.UpperLeft, Color.white, "");
            log.gameObject.AddComponent<DemoLogPanel>().text = logText;
        }

        const string HelpText =
            "<size=40><b>VR BOUNDARY DEMO</b></size>   <color=#9AB>Meta Quest 3S  •  OVRInput / OVRBoundary / Passthrough</color>\n\n" +
            "<color=#7FD8FF>Niebieska</color> linia = granica systemowa (Play Area)     <color=#FFB060>pomarańczowa</color> = Twoja granica\n" +
            "Ściany pojawiają się, gdy zbliżasz głowę lub kontroler; kontroler wibruje przy krawędzi.\n\n" +
            "<b><color=#FFD27F>PRAWY KONTROLER – własna granica</color></b>\n" +
            "Trigger – dodaj narożnik w miejscu celownika na podłodze\n" +
            "Grip – cofnij ostatni punkt     przytrzymaj grip 1 s – wyczyść\n" +
            "A – testy bliskości: Play Area ↔ własna granica\n" +
            "B – pokaż/ukryj wizualizację Play Area\n" +
            "Gałka ←/→ – obrót o 45°     klik gałki – powrót na start\n\n" +
            "<b><color=#C8A0FF>LEWY KONTROLER – system</color></b>\n" +
            "X – passthrough wł./wył.     Y – poproś system o ukrycie granicy\n" +
            "Trigger – odczytaj granicę systemową ponownie\n" +
            "Grip (trzymaj) – pokaż wszystkie ściany\n" +
            "Menu ☰ – układ odniesienia Floor Level ↔ Stage\n" +
            "Gałka – ruch w scenie     klik gałki – pokaż/ukryj panele";

        // =====================================================================
        // POMOCNICZE
        // =====================================================================

        static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        static object[] DefaultArgs(MethodInfo m) =>
            m.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();

        static bool SetEnum(Object target, string valueName, bool warn, params string[] propertyNames)
        {
            if (target == null) return false;
            var so = new SerializedObject(target);
            foreach (string pn in propertyNames)
            {
                var p = so.FindProperty(pn);
                if (p == null || p.propertyType != SerializedPropertyType.Enum) continue;
                int i = Array.IndexOf(p.enumNames, valueName);
                if (i < 0) continue;
                if (p.enumValueIndex != i)
                {
                    p.enumValueIndex = i;
                    so.ApplyModifiedProperties();
                }
                return true;
            }
            if (warn) Debug.LogWarning($"[VRBoundaryDemo] Nie ustawiono {valueName} na {target.GetType().Name} - ustaw ręcznie w Inspectorze.");
            return false;
        }
    }

    /// <summary>Po otwarciu projektu: otwiera scenę demo albo proponuje konfigurację.</summary>
    [InitializeOnLoad]
    static class VRBoundaryDemoFirstRun
    {
        const string Key = "VRBoundaryDemo.FirstRunChecked";

        static VRBoundaryDemoFirstRun()
        {
            if (Application.isBatchMode || SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = EditorSceneManager.GetActiveScene();
            bool untitled = string.IsNullOrEmpty(active.path);

            if (File.Exists(VRBoundaryDemoSetup.ScenePath))
            {
                if (untitled && !active.isDirty) EditorSceneManager.OpenScene(VRBoundaryDemoSetup.ScenePath);
                return;
            }

            if (EditorUtility.DisplayDialog("VR Boundary Demo",
                    "Scena demo jeszcze nie istnieje.\n\nKolejność (szczegóły w INSTRUKCJA.md):\n" +
                    "1. VR Boundary Demo > 1. Konfiguruj projekt dla Meta Quest\n" +
                    "2. VR Boundary Demo > 2. Zbuduj scenę demo\n" +
                    "3. Building Blocks: Camera Rig, Controller Tracking, Passthrough\n\n" +
                    "Skonfigurować projekt teraz?",
                    "Konfiguruj teraz", "Później"))
            {
                VRBoundaryDemoSetup.ConfigureProject();
            }
        }
    }
}
