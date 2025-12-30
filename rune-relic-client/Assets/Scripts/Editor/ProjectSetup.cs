#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

namespace RuneRelic.Editor
{
    /// <summary>
    /// Automated project setup wizard.
    /// Run from menu: RuneRelic → Setup Project
    /// </summary>
    public class ProjectSetup : EditorWindow
    {
        private bool _foldersCreated;
        private bool _scenesCreated;
        private bool _prefabsCreated;
        private bool _materialsCreated;
        private bool _managersCreated;

        [MenuItem("RuneRelic/Setup Project (Full)", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<ProjectSetup>("Rune Relic Setup");
            window.minSize = new Vector2(400, 500);
        }

        [MenuItem("RuneRelic/Quick Setup (Auto)", priority = 1)]
        public static void QuickSetup()
        {
            if (EditorUtility.DisplayDialog("Rune Relic Quick Setup",
                "This will automatically:\n\n" +
                "• Create folder structure\n" +
                "• Create MainMenu and Game scenes\n" +
                "• Generate placeholder prefabs\n" +
                "• Create materials with custom shaders\n" +
                "• Setup game managers\n\n" +
                "Continue?", "Setup", "Cancel"))
            {
                RunFullSetup();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Rune Relic Project Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "This wizard will configure your Unity project for Rune Relic.\n" +
                "Click 'Run Full Setup' or run individual steps below.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Run Full Setup", GUILayout.Height(40)))
            {
                RunFullSetup();
            }

            EditorGUILayout.Space(20);
            GUILayout.Label("Individual Steps:", EditorStyles.boldLabel);

            DrawStep("1. Create Folders", ref _foldersCreated, CreateFolders);
            DrawStep("2. Create Scenes", ref _scenesCreated, CreateScenes);
            DrawStep("3. Create Materials", ref _materialsCreated, CreateMaterials);
            DrawStep("4. Create Prefabs", ref _prefabsCreated, CreatePrefabs);
            DrawStep("5. Setup Managers", ref _managersCreated, SetupManagers);

            EditorGUILayout.Space(20);

            EditorGUILayout.HelpBox(
                "After setup, remember to:\n" +
                "• Add NativeWebSocket package\n" +
                "• Import your art assets\n" +
                "• Configure Input System (optional)",
                MessageType.Warning);

            if (GUILayout.Button("Open Package Manager"))
            {
                EditorApplication.ExecuteMenuItem("Window/Package Manager");
            }
        }

        private void DrawStep(string label, ref bool completed, System.Action action)
        {
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !completed;
            if (GUILayout.Button(label, GUILayout.Width(200)))
            {
                action();
                completed = true;
            }
            GUI.enabled = true;

            GUILayout.Label(completed ? "✓ Done" : "Pending",
                completed ? EditorStyles.boldLabel : EditorStyles.label);

            EditorGUILayout.EndHorizontal();
        }

        public static void RunFullSetup()
        {
            EditorUtility.DisplayProgressBar("Setting up Rune Relic", "Creating folders...", 0.1f);
            CreateFolders();

            EditorUtility.DisplayProgressBar("Setting up Rune Relic", "Creating materials...", 0.3f);
            CreateMaterials();

            EditorUtility.DisplayProgressBar("Setting up Rune Relic", "Creating prefabs...", 0.5f);
            CreatePrefabs();

            EditorUtility.DisplayProgressBar("Setting up Rune Relic", "Creating scenes...", 0.7f);
            CreateScenes();

            EditorUtility.DisplayProgressBar("Setting up Rune Relic", "Setting up managers...", 0.9f);
            SetupManagers();

            EditorUtility.ClearProgressBar();

            EditorUtility.DisplayDialog("Setup Complete",
                "Rune Relic project setup complete!\n\n" +
                "Next steps:\n" +
                "1. Window → Package Manager → Add git URL:\n" +
                "   https://github.com/endel/NativeWebSocket.git#upm\n" +
                "2. Import your art assets\n" +
                "3. Press Play to test!",
                "OK");

            // Open Game scene
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        }

        // =====================================================================
        // Step 1: Create Folders
        // =====================================================================

        public static void CreateFolders()
        {
            string[] folders = {
                "Assets/Scenes",
                "Assets/Prefabs",
                "Assets/Prefabs/Players",
                "Assets/Prefabs/Runes",
                "Assets/Prefabs/Shrines",
                "Assets/Prefabs/VFX",
                "Assets/Prefabs/UI",
                "Assets/Materials",
                "Assets/Materials/Players",
                "Assets/Materials/Runes",
                "Assets/Materials/Shrines",
                "Assets/Materials/Environment",
                "Assets/Materials/UI",
                "Assets/Models",
                "Assets/Textures",
                "Assets/Audio",
                "Assets/Audio/Music",
                "Assets/Audio/SFX",
                "Assets/Resources",
                "Assets/StreamingAssets"
            };

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace("\\", "/");
                    string newFolder = Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, newFolder);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] Folders created");
        }

        // =====================================================================
        // Step 2: Create Scenes
        // =====================================================================

        public static void CreateScenes()
        {
            // Create MainMenu scene
            CreateMainMenuScene();

            // Create Game scene
            CreateGameScene();

            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] Scenes created");
        }

        private static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Create UI Canvas
            var canvasObj = CreateUICanvas("MainMenuCanvas");

            // Create UI Manager
            var uiManagerObj = new GameObject("UIManager");
            uiManagerObj.AddComponent<UI.Core.UIManager>();

            // Create Audio Manager
            var audioManagerObj = new GameObject("AudioManager");
            audioManagerObj.AddComponent<Audio.AudioManager>();

            // Create simple menu panel
            var menuPanel = CreatePanel(canvasObj.transform, "MenuPanel", new Vector2(400, 500));

            // Title
            CreateText(menuPanel.transform, "Title", "RUNE RELIC",
                new Vector2(0, 180), 48, TextAnchor.MiddleCenter);

            // Play button
            CreateButton(menuPanel.transform, "PlayButton", "PLAY",
                new Vector2(0, 50), new Vector2(300, 60));

            // Practice button
            CreateButton(menuPanel.transform, "PracticeButton", "PRACTICE",
                new Vector2(0, -20), new Vector2(300, 50));

            // Settings button
            CreateButton(menuPanel.transform, "SettingsButton", "SETTINGS",
                new Vector2(0, -80), new Vector2(300, 50));

            // Quit button
            CreateButton(menuPanel.transform, "QuitButton", "QUIT",
                new Vector2(0, -140), new Vector2(300, 50));

            // Connection status
            CreateText(menuPanel.transform, "ConnectionStatus", "Offline",
                new Vector2(0, -220), 16, TextAnchor.MiddleCenter);

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        private static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Setup camera
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0, 15, -10);
                camera.transform.rotation = Quaternion.Euler(50, 0, 0);
                camera.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
            }

            // Create directional light
            var lightObj = GameObject.Find("Directional Light");
            if (lightObj != null)
            {
                lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
                var light = lightObj.GetComponent<Light>();
                if (light != null)
                {
                    light.intensity = 1f;
                    light.color = new Color(1f, 0.95f, 0.9f);
                }
            }

            // Create arena floor
            var floorObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floorObj.name = "ArenaFloor";
            floorObj.transform.position = Vector3.zero;
            floorObj.transform.localScale = new Vector3(10, 1, 10);
            var floorRenderer = floorObj.GetComponent<Renderer>();
            if (floorRenderer != null)
            {
                floorRenderer.material.color = new Color(0.1f, 0.1f, 0.15f);
            }

            // Create game managers
            var managersObj = new GameObject("--- MANAGERS ---");

            var gameManagerObj = new GameObject("GameManager");
            gameManagerObj.transform.SetParent(managersObj.transform);
            gameManagerObj.AddComponent<Game.GameManager>();

            var entityManagerObj = new GameObject("EntityManager");
            entityManagerObj.transform.SetParent(managersObj.transform);
            entityManagerObj.AddComponent<Game.EntityManager>();

            var arenaControllerObj = new GameObject("ArenaController");
            arenaControllerObj.transform.SetParent(managersObj.transform);
            arenaControllerObj.AddComponent<Game.ArenaController>();

            // Create containers
            var containersObj = new GameObject("--- CONTAINERS ---");
            new GameObject("Players").transform.SetParent(containersObj.transform);
            new GameObject("Runes").transform.SetParent(containersObj.transform);
            new GameObject("Shrines").transform.SetParent(containersObj.transform);

            // Create Game Client (persistent)
            var clientObj = new GameObject("GameClient");
            clientObj.AddComponent<Network.GameClient>();

            // Create UI Canvas
            var canvasObj = CreateUICanvas("GameUICanvas");

            // Create HUD
            var hudPanel = CreatePanel(canvasObj.transform, "HUD", Vector2.zero);
            var hudRect = hudPanel.GetComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            // Score display (top left)
            CreateText(hudPanel.transform, "ScoreText", "Score: 0",
                new Vector2(-Screen.width / 2 + 100, Screen.height / 2 - 30), 24, TextAnchor.MiddleLeft);

            // Time display (top center)
            CreateText(hudPanel.transform, "TimeText", "1:30",
                new Vector2(0, Screen.height / 2 - 30), 32, TextAnchor.MiddleCenter);

            // Form display (top right)
            CreateText(hudPanel.transform, "FormText", "Spark",
                new Vector2(Screen.width / 2 - 100, Screen.height / 2 - 30), 24, TextAnchor.MiddleRight);

            // Add VFX Manager
            var vfxManagerObj = new GameObject("VFXManager");
            vfxManagerObj.AddComponent<VFX.VFXManager>();

            // Add Screen Effects
            if (camera != null)
            {
                camera.gameObject.AddComponent<VFX.ScreenEffects>();
            }

            // Save scene
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");
        }

        // =====================================================================
        // Step 3: Create Materials
        // =====================================================================

        public static void CreateMaterials()
        {
            // Try to find our custom shaders, fallback to Standard
            Shader playerShader = Shader.Find("RuneRelic/PlayerForm") ?? Shader.Find("Standard");
            Shader runeShader = Shader.Find("RuneRelic/RuneGlow") ?? Shader.Find("Standard");
            Shader floorShader = Shader.Find("RuneRelic/ArenaFloor") ?? Shader.Find("Standard");

            // Form materials
            Color[] formColors = {
                new Color(0.9f, 0.95f, 1f),     // Spark
                new Color(0.6f, 0.8f, 1f),      // Glyph
                new Color(0.4f, 0.9f, 0.6f),    // Ward
                new Color(0.7f, 0.4f, 1f),      // Arcane
                new Color(1f, 0.8f, 0.3f)       // Ancient
            };
            string[] formNames = { "Spark", "Glyph", "Ward", "Arcane", "Ancient" };

            for (int i = 0; i < formNames.Length; i++)
            {
                CreateMaterial($"Assets/Materials/Players/Mat_{formNames[i]}.mat",
                    playerShader, formColors[i]);
            }

            // Rune materials
            Color[] runeColors = {
                new Color(0.3f, 0.5f, 1f),      // Wisdom
                new Color(1f, 0.3f, 0.3f),      // Power
                new Color(1f, 1f, 0.3f),        // Speed
                new Color(0.3f, 1f, 0.5f),      // Shield
                new Color(0.7f, 0.3f, 1f),      // Arcane
                Color.white                     // Chaos
            };
            string[] runeNames = { "Wisdom", "Power", "Speed", "Shield", "Arcane", "Chaos" };

            for (int i = 0; i < runeNames.Length; i++)
            {
                CreateMaterial($"Assets/Materials/Runes/Mat_{runeNames[i]}Rune.mat",
                    runeShader, runeColors[i]);
            }

            // Shrine materials
            for (int i = 0; i < 4; i++)
            {
                CreateMaterial($"Assets/Materials/Shrines/Mat_{runeNames[i]}Shrine.mat",
                    playerShader, runeColors[i]);
            }

            // Arena floor material
            CreateMaterial("Assets/Materials/Environment/Mat_ArenaFloor.mat",
                floorShader, new Color(0.1f, 0.1f, 0.15f));

            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] Materials created");
        }

        private static void CreateMaterial(string path, Shader shader, Color color)
        {
            if (File.Exists(path)) return;

            var mat = new Material(shader);
            mat.color = color;

            // Ensure directory exists
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            AssetDatabase.CreateAsset(mat, path);
        }

        // =====================================================================
        // Step 4: Create Prefabs
        // =====================================================================

        public static void CreatePrefabs()
        {
            // Create player form prefabs
            string[] formNames = { "Spark", "Glyph", "Ward", "Arcane", "Ancient" };
            float[] formScales = { 1f, 1.4f, 2f, 2.8f, 4f };

            for (int i = 0; i < formNames.Length; i++)
            {
                CreatePlayerPrefab(formNames[i], formScales[i], i);
            }

            // Create rune prefabs
            string[] runeNames = { "Wisdom", "Power", "Speed", "Shield", "Arcane", "Chaos" };
            for (int i = 0; i < runeNames.Length; i++)
            {
                CreateRunePrefab(runeNames[i], i);
            }

            // Create shrine prefabs
            for (int i = 0; i < 4; i++)
            {
                CreateShrinePrefab(runeNames[i], i);
            }

            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] Prefabs created");
        }

        private static void CreatePlayerPrefab(string formName, float scale, int formIndex)
        {
            string path = $"Assets/Prefabs/Players/Player_{formName}.prefab";
            if (File.Exists(path)) return;

            var obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            obj.name = $"Player_{formName}";
            obj.transform.localScale = Vector3.one * scale;

            // Assign material
            string matPath = $"Assets/Materials/Players/Mat_{formName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                obj.GetComponent<Renderer>().material = mat;
            }

            // Add PlayerVisual component
            obj.AddComponent<Game.PlayerVisual>();

            // Add trail renderer
            var trail = obj.AddComponent<TrailRenderer>();
            trail.startWidth = scale * 0.3f;
            trail.endWidth = 0;
            trail.time = 0.3f;
            trail.material = new Material(Shader.Find("Sprites/Default"));

            PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
        }

        private static void CreateRunePrefab(string runeName, int runeIndex)
        {
            string path = $"Assets/Prefabs/Runes/Rune_{runeName}.prefab";
            if (File.Exists(path)) return;

            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = $"Rune_{runeName}";
            obj.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
            obj.transform.rotation = Quaternion.Euler(45, 0, 45);

            // Remove collider (server handles collision)
            Object.DestroyImmediate(obj.GetComponent<Collider>());

            // Assign material
            string matPath = $"Assets/Materials/Runes/Mat_{runeName}Rune.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                obj.GetComponent<Renderer>().material = mat;
            }

            // Add Rune component
            obj.AddComponent<Entities.Rune>();

            // Add point light
            var lightObj = new GameObject("Light");
            lightObj.transform.SetParent(obj.transform);
            lightObj.transform.localPosition = Vector3.zero;
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 1f;

            PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
        }

        private static void CreateShrinePrefab(string shrineName, int shrineIndex)
        {
            string path = $"Assets/Prefabs/Shrines/Shrine_{shrineName}.prefab";
            if (File.Exists(path)) return;

            var obj = new GameObject($"Shrine_{shrineName}");

            // Base cylinder
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Base";
            baseObj.transform.SetParent(obj.transform);
            baseObj.transform.localPosition = Vector3.zero;
            baseObj.transform.localScale = new Vector3(2, 0.5f, 2);

            // Assign material
            string matPath = $"Assets/Materials/Shrines/Mat_{shrineName}Shrine.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                baseObj.GetComponent<Renderer>().material = mat;
            }

            // Capture zone indicator
            var zoneObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zoneObj.name = "CaptureZone";
            zoneObj.transform.SetParent(obj.transform);
            zoneObj.transform.localPosition = new Vector3(0, -0.2f, 0);
            zoneObj.transform.localScale = new Vector3(6, 0.05f, 6);
            Object.DestroyImmediate(zoneObj.GetComponent<Collider>());

            var zoneRenderer = zoneObj.GetComponent<Renderer>();
            var zoneMat = new Material(Shader.Find("Standard"));
            zoneMat.color = new Color(1, 1, 1, 0.2f);
            zoneMat.SetFloat("_Mode", 3); // Transparent
            zoneRenderer.material = zoneMat;

            // Add Shrine component
            obj.AddComponent<Entities.Shrine>();

            PrefabUtility.SaveAsPrefabAsset(obj, path);
            Object.DestroyImmediate(obj);
        }

        // =====================================================================
        // Step 5: Setup Managers
        // =====================================================================

        public static void SetupManagers()
        {
            // Create UI Theme asset
            string themePath = "Assets/Resources/DefaultTheme.asset";
            if (!File.Exists(themePath))
            {
                var theme = ScriptableObject.CreateInstance<UI.Core.UITheme>();
                AssetDatabase.CreateAsset(theme, themePath);
            }

            // Add scenes to build settings
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Game.unity", true)
            };
            EditorBuildSettings.scenes = scenes;

            Debug.Log("[ProjectSetup] Managers configured");
        }

        // =====================================================================
        // UI Helpers
        // =====================================================================

        private static GameObject CreateUICanvas(string name)
        {
            var canvasObj = new GameObject(name);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // Event system
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvasObj;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            var panelObj = new GameObject(name);
            panelObj.transform.SetParent(parent);

            var rect = panelObj.AddComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size == Vector2.zero ? new Vector2(400, 300) : size;

            var image = panelObj.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            return panelObj;
        }

        private static GameObject CreateText(Transform parent, string name, string content,
            Vector2 position, int fontSize, TextAnchor anchor)
        {
            var textObj = new GameObject(name);
            textObj.transform.SetParent(parent);

            var rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(350, 50);

            var text = textObj.AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;

            return textObj;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 position, Vector2 size)
        {
            var buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);

            var rect = buttonObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.5f, 0.8f);

            var button = buttonObj.AddComponent<Button>();
            button.targetGraphic = image;

            // Button text
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);

            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;

            return buttonObj;
        }
    }
}
#endif
