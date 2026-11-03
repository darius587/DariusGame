using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class GameMenuSetup
    {
        private const string SettingsPrefab = "Assets/Prefabs/Phase4/SettingsPanel.prefab";
        private const string ControlsPrefab = "Assets/Prefabs/Phase4/ControlsPanel.prefab";
        private static readonly Color PanelColor = new Color32(20, 34, 57, 246);
        private static readonly Color Gold = new Color32(255, 211, 103, 255);
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        [MenuItem("Tools/Zhuzbai/Setup Main Menu and UI")]
        public static void Apply()
        {
            U.RequireEditMode();
            if (!File.Exists(U.Level)) throw new InvalidOperationException("MainLevel must exist. This command never regenerates it.");
            U.EditScene(U.Level, scene => {
                if (U.All<FoundationHud>(scene).Length != 1 || U.All<PixelNumberDisplay>(scene).Length != 2)
                    throw new InvalidOperationException("Apply Pixel HUD first. Existing heart/coin displays are required and will be preserved.");
            }, false);
            U.Folder("Assets/UI/Phase4"); U.Folder("Assets/Prefabs/Phase4");
            EnsureIcons();
            EnsurePanelPrefab(SettingsPrefab, true);
            EnsurePanelPrefab(ControlsPrefab, false);
            GameObject audio = GameAudioSetup.EnsurePrefab();
            bool newMenu = !File.Exists(U.Menu);
            U.EditScene(U.Menu, scene => {
                if (!newMenu && U.All<GameMenuController>(scene).Length == 0)
                    throw new InvalidOperationException("Existing MainMenu has no Phase 4 owner. It will not be overwritten.");
                BuildScene(scene, false);
                GameAudioSetup.ConnectScene(scene, audio, false);
            }, true, true);
            U.EditScene(U.Level, scene => {
                BuildScene(scene, true);
                GameAudioSetup.ConnectScene(scene, audio, true);
            });
            RegisterScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("Main Menu and Game UI applied. Open MainMenu alone and press Play. MainLevel layout, Pixel HUD values, and player tuning are preserved.");
        }

        internal static void BuildScene(Scene scene, bool gameplay)
        {
            var owners = U.All<GameMenuController>(scene);
            if (owners.Length > 1) throw new InvalidOperationException("Multiple menu controllers found in " + scene.name);
            RectTransform root;
            GameMenuController controller;
            if (owners.Length == 1) { controller = owners[0]; root = (RectTransform)controller.transform; }
            else { root = Rect(null, "Phase4 Interface"); controller = root.gameObject.AddComponent<GameMenuController>(); }
            Canvas canvas = Get<Canvas>(root.gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100; canvas.pixelPerfect = true;
            ScaleCanvas(canvas);
            Get<GraphicRaycaster>(root.gameObject);
            if (!gameplay) BuildBackdrop(root);
            var shade = Rect(root, "Modal Shade"); Stretch(shade);
            var shadeImage = Get<Image>(shade.gameObject);
            shadeImage.color = new Color(0.015f, 0.03f, 0.07f, gameplay ? 0.72f : 0.12f);
            shadeImage.raycastTarget = true;
            var safe = Rect(root, "Safe Area"); Stretch(safe); Get<MenuSafeArea>(safe.gameObject);
            RectTransform main = null, pause = null, buttons = null;
            if (gameplay)
            {
                pause = Panel(safe, "Pause", new Vector2(640, 650));
                Label(pause, "Title", "PAUSED", new Vector2(0, -60), new Vector2(560, 70), 52, Gold);
                MakeButton(pause, "Resume", "RESUME", -185, MenuCommand.Resume, controller);
                MakeButton(pause, "Settings", "SETTINGS", -285, MenuCommand.Settings, controller);
                MakeButton(pause, "Controls", "CONTROLS", -385, MenuCommand.Controls, controller);
                MakeButton(pause, "Main Menu", "MAIN MENU", -485, MenuCommand.MainMenu, controller);
                buttons = Rect(safe, "Corner Buttons");
                Place(buttons, Vector2.one, Vector2.one, new Vector2(-24, -24), new Vector2(152, 68));
                IconButton(buttons, "MENU", new Vector2(0, 0), "MenuIcon", MenuCommand.Pause, controller);
                IconButton(buttons, "HELP", new Vector2(84, 0), "HelpIcon", U.All<LevelDefinition>(scene).Length > 0 ? MenuCommand.HowToPlay : MenuCommand.Controls, controller);
                var players = U.All<PlayerFoundation>(scene);
                if (players.Length != 1 || players[0].GetComponent<PlayerInput>() == null)
                    throw new InvalidOperationException("One existing PlayerInput player is required. Apply Phase 2 first.");
                U.Reference(controller, "player", players[0]);
                U.Reference(controller, "playerInput", players[0].GetComponent<PlayerInput>());
                // Only UI display settings and visibility change; HUD system references are untouched.
                var hud = U.All<FoundationHud>(scene).Single();
                ScaleCanvas(hud.GetComponent<Canvas>());
                var hudLayout = hud.GetComponentInChildren<PixelHudSafeArea>(true);
                if (hudLayout != null)
                {
                    var hudData = new SerializedObject(hudLayout);
                    hudData.FindProperty("preferredScale").floatValue = 1.5f;
                    hudData.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var text in hud.GetComponentsInChildren<Text>(true))
                    if (text.name == "Phase Notice") text.gameObject.SetActive(false);
            }
            else
            {
                main = Panel(safe, "Main", new Vector2(740, 850));
                Label(main, "Eyebrow", "A LITTLE WORLD. A BIG ADVENTURE.", new Vector2(0, -54), new Vector2(660, 38), 21, new Color32(167, 210, 231, 255));
                GameBrandingSetup.ConfigureTitle(main);
                GameBrandingSetup.ConfigurePlayerSettings();
                MakeButton(main, "Play", "PLAY", -350, MenuCommand.Play, controller);
                MakeButton(main, "Settings", "SETTINGS", -450, MenuCommand.Settings, controller);
                MakeButton(main, "Controls", "CONTROLS", -550, MenuCommand.Controls, controller);
                MakeButton(main, "Quit", "QUIT", -650, MenuCommand.Quit, controller);
                Label(main, "Footer", "EXPLORE  /  COLLECT  /  ADVENTURE", new Vector2(0, -790), new Vector2(660, 30), 19, new Color32(167, 210, 231, 255));
                if (U.All<Camera>(scene).Length == 0)
                {
                    var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener));
                    camera.tag = "MainCamera";
                    camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                    camera.GetComponent<Camera>().backgroundColor = new Color32(103, 184, 240, 255);
                    camera.GetComponent<Camera>().orthographic = true;
                    camera.transform.position = new Vector3(0, 0, -10);
                }
            }
            var settings = InstancePanel(safe, "Settings", SettingsPrefab);
            var controls = InstancePanel(safe, "Controls", ControlsPrefab);
            U.Reference(controller, "mainPanel", main != null ? main.gameObject : null);
            U.Reference(controller, "pausePanel", pause != null ? pause.gameObject : null);
            U.Reference(controller, "settingsPanel", settings.gameObject);
            U.Reference(controller, "controlsPanel", controls.gameObject);
            U.Reference(controller, "modalShade", shade.gameObject);
            U.Reference(controller, "topButtons", buttons != null ? buttons.gameObject : null);
            U.Reference(safe.GetComponent<MenuSafeArea>(), "cornerButtons", buttons);
            var data = new SerializedObject(controller);
            data.FindProperty("gameplayScene").boolValue = gameplay;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var action in safe.GetComponentsInChildren<MenuButtonAction>(true)) U.Reference(action, "controller", controller);
            foreach (var selectable in safe.GetComponentsInChildren<Selectable>(true))
                if (!(selectable is Button)) Get<MenuCancelHandler>(selectable.gameObject);
            if (main != null) main.gameObject.SetActive(true);
            if (pause != null) pause.gameObject.SetActive(false);
            settings.gameObject.SetActive(false); controls.gameObject.SetActive(false);
            shade.gameObject.SetActive(!gameplay);
            ConnectEventSystem(scene, gameplay ? buttons.gameObject : main.gameObject);
            if (gameplay) MultiDeviceControlsSetup.ApplySceneIfInstalled(scene);
            EditorUtility.SetDirty(controller);
        }
        private static void ScaleCanvas(Canvas canvas)
        {
            var scaler = Get<CanvasScaler>(canvas.gameObject);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            EditorUtility.SetDirty(scaler);
        }
        private static void ConnectEventSystem(Scene scene, GameObject first)
        {
            var systems = U.All<EventSystem>(scene);
            if (systems.Length > 1) throw new InvalidOperationException("Multiple EventSystems found in " + scene.name);
            var system = systems.Length == 1 ? systems[0] : new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var legacy in system.GetComponents<StandaloneInputModule>()) UnityEngine.Object.DestroyImmediate(legacy);
            var module = Get<InputSystemUIInputModule>(system.gameObject);
            UiInputActionSetup.Configure(module);
            system.firstSelectedGameObject = first.GetComponentInChildren<Selectable>(true)?.gameObject;
            EditorUtility.SetDirty(module); EditorUtility.SetDirty(system);
        }
        private static void EnsurePanelPrefab(string path, bool settings)
        {
            if (File.Exists(path))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || (settings && prefab.GetComponent<AudioSettingsPanel>() == null) || prefab.GetComponent<MenuPanelFit>() == null)
                    throw new InvalidOperationException("Existing panel prefab has unexpected ownership: " + path);
                return;
            }
            RectTransform root = Panel(null, settings ? "Settings" : "Controls", new Vector2(740, settings ? 720 : 850));
            try
            {
                Label(root, "Title", settings ? "SETTINGS" : "CONTROLS", new Vector2(0, -50), new Vector2(660, 76), 48, Gold);
                if (settings)
                {
                    var panel = root.gameObject.AddComponent<AudioSettingsPanel>();
                    Label(root, "SFX Label", "GAME SOUNDS (SFX)", new Vector2(-40, -160), new Vector2(560, 42), 26, Color.white);
                    var sfx = MakeSlider(root, "SFX", -225);
                    var sfxValue = Label(root, "SFX Percent", "80%", new Vector2(270, -160), new Vector2(100, 42), 26, Gold);
                    Label(root, "Music Label", "MUSIC", new Vector2(-40, -310), new Vector2(560, 42), 26, Color.white);
                    var music = MakeSlider(root, "Music", -375);
                    var musicValue = Label(root, "Music Percent", "45%", new Vector2(270, -310), new Vector2(100, 42), 26, Gold);
                    var toggleRect = Rect(root, "Fullscreen");
                    Place(toggleRect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(500, 52));
                    var toggle = Get<Toggle>(toggleRect.gameObject);
                    var toggleHitArea = Get<Image>(toggleRect.gameObject); toggleHitArea.color = Color.clear;
                    var box = Rect(toggleRect, "Box"); Place(box, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(44, 44));
                    var boxImage = Get<Image>(box.gameObject); boxImage.color = new Color32(70, 95, 120, 255);
                    var mark = Rect(box, "Mark"); Stretch(mark); mark.offsetMin = new Vector2(9, 9); mark.offsetMax = new Vector2(-9, -9);
                    var markImage = Get<Image>(mark.gameObject); markImage.color = Gold; markImage.raycastTarget = false;
                    var text = Label(toggleRect, "Label", "FULLSCREEN", Vector2.zero, new Vector2(430, 52), 26, Color.white);
                    Place(text.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 0), new Vector2(430, 52));
                    text.alignment = TextAnchor.MiddleLeft;
                    toggle.targetGraphic = boxImage; toggle.graphic = markImage;
                    U.Reference(panel, "sfxSlider", sfx); U.Reference(panel, "musicSlider", music);
                    U.Reference(panel, "fullscreen", toggle); U.Reference(panel, "sfxValue", sfxValue); U.Reference(panel, "musicValue", musicValue);
                    MakeButton(root, "Back", "BACK", -610, MenuCommand.Back, null);
                }
                else
                {
                    var body = Label(root, "Help Text", "KEYBOARD & MOUSE\nA / D or arrow keys - Move\nSpace - Jump\nX or left mouse button - Fire\n\nMOBILE\nMobile joystick - Move\nJUMP button - Jump\nFIRE button - Fire\n\nGAMEPAD\nLeft stick / South / West", new Vector2(0, -160), new Vector2(610, 520), 27, new Color32(222, 238, 246, 255));
                    body.alignment = TextAnchor.UpperLeft;
                    MakeButton(root, "Back", "BACK", -735, MenuCommand.Back, null);
                }
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, path, out bool success);
                if (!success) throw new IOException("Could not save " + path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
        private static Slider MakeSlider(RectTransform parent, string name, float y)
        {
            var rect = Rect(parent, name + " Slider");
            Place(rect, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(580, 50));
            var hitArea = Get<Image>(rect.gameObject); hitArea.color = Color.clear;
            var track = Rect(rect, "Track");
            Place(track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580, 14));
            var trackImage = Get<Image>(track.gameObject); trackImage.color = new Color32(62, 84, 109, 255);
            var area = Rect(rect, "Fill Area"); Stretch(area); area.offsetMin = new Vector2(16, 18); area.offsetMax = new Vector2(-16, -18);
            var fill = Rect(area, "Fill"); Stretch(fill);
            var fillImage = Get<Image>(fill.gameObject); fillImage.color = Gold; fillImage.raycastTarget = false;
            var handleArea = Rect(rect, "Handle Area"); Stretch(handleArea); handleArea.offsetMin = new Vector2(16, 0); handleArea.offsetMax = new Vector2(-16, 0);
            var handle = Rect(handleArea, "Handle"); handle.sizeDelta = new Vector2(28, 44);
            var handleImage = Get<Image>(handle.gameObject); handleImage.color = new Color32(255, 241, 198, 255);
            var slider = Get<Slider>(rect.gameObject);
            slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }
        private static RectTransform InstancePanel(RectTransform parent, string name, string prefabPath)
        {
            var found = parent.Find(name) as RectTransform;
            if (found != null) return found;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), parent);
            go.name = name;
            return (RectTransform)go.transform;
        }
        internal static RectTransform Panel(Transform parent, string name, Vector2 size)
        {
            var rect = Rect(parent, name);
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = Get<Image>(rect.gameObject); image.color = PanelColor; image.raycastTarget = true;
            Get<MenuPanelFit>(rect.gameObject);
            var accent = Rect(rect, "Pixel Edge");
            accent.anchorMin = new Vector2(0, 1); accent.anchorMax = Vector2.one; accent.pivot = new Vector2(0.5f, 1);
            accent.sizeDelta = new Vector2(-8, 4); accent.anchoredPosition = new Vector2(0, -4);
            var stripe = Get<Image>(accent.gameObject); stripe.color = Gold; stripe.raycastTarget = false;
            return rect;
        }
        internal static Button MakeButton(RectTransform parent, string name, string text, float y, MenuCommand command, GameMenuController controller)
        {
            var rect = Rect(parent, name);
            Place(rect, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(500, 76));
            return ConfigureButton(rect, text, command, controller);
        }
        private static Button ConfigureButton(RectTransform rect, string text, MenuCommand command, GameMenuController controller)
        {
            var face = Get<Image>(rect.gameObject); face.color = Color.white; face.raycastTarget = true;
            var button = Get<Button>(rect.gameObject); button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = new Color32(44, 76, 108, 255);
            colors.highlightedColor = new Color32(74, 116, 143, 255);
            colors.selectedColor = new Color32(74, 116, 143, 255);
            colors.pressedColor = new Color32(29, 52, 82, 255);
            colors.fadeDuration = 0.06f; button.colors = colors;
            var action = Get<MenuButtonAction>(rect.gameObject);
            U.Reference(action, "controller", controller);
            var data = new SerializedObject(action); data.FindProperty("command").enumValueIndex = (int)command; data.ApplyModifiedPropertiesWithoutUndo();
            if (!string.IsNullOrEmpty(text))
            {
                var label = Label(rect, "Label", text, Vector2.zero, rect.sizeDelta, 30, Color.white);
                Stretch(label.rectTransform);
                var shadow = Get<Shadow>(label.gameObject); shadow.effectColor = new Color(0, 0, 0, 0.65f); shadow.effectDistance = new Vector2(2, -2);
            }
            var edge = Rect(rect, "Top Edge"); edge.anchorMin = new Vector2(0, 1); edge.anchorMax = Vector2.one;
            edge.pivot = new Vector2(0.5f, 1); edge.sizeDelta = new Vector2(-8, 3); edge.anchoredPosition = new Vector2(0, -4);
            var edgeImage = Get<Image>(edge.gameObject); edgeImage.color = new Color32(146, 190, 209, 160); edgeImage.raycastTarget = false;
            return button;
        }
        private static void IconButton(RectTransform parent, string name, Vector2 position, string spriteName, MenuCommand command, GameMenuController controller)
        {
            var rect = Rect(parent, name);
            Place(rect, new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(68, 68));
            ConfigureButton(rect, "", command, controller);
            var icon = Rect(rect, "Icon");
            Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            var image = Get<Image>(icon.gameObject); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Phase4/" + spriteName + ".png");
            image.preserveAspect = true; image.raycastTarget = false;
        }
        internal static Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var rect = Rect(parent, name);
            Place(rect, new Vector2(0.5f, 1), new Vector2(0.5f, 1), position, size);
            var text = Get<Text>(rect.gameObject);
            text.font = Font; text.text = content; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.color = color; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        internal static RectTransform Rect(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing != null) return existing as RectTransform ?? throw new InvalidOperationException("Expected UI object: " + name);
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            if (parent != null) go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static T Get<T>(GameObject go) where T : Component
        {
            // Unity's missing-component wrappers require overloaded ==, not C# ?? semantics.
            T component = go.GetComponent<T>();
            if (component == null) component = go.AddComponent<T>();
            if (component == null) throw new InvalidOperationException("Could not add " + typeof(T).Name + " to " + go.name);
            return component;
        }
        internal static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
        }
        private static void BuildBackdrop(RectTransform root)
        {
            var background = Rect(root, "Pixel Landscape"); Stretch(background);
            var sky = Get<Image>(background.gameObject); sky.color = new Color32(103, 184, 240, 255); sky.raycastTarget = false;
            for (int i = 0; i < 5; i++)
            {
                Decoration(background, "Mountain " + i, "Mountain", new Vector2(i / 4f, 0), new Vector2(0, 120), new Vector2(750, 560));
                Decoration(background, "Cloud " + i, "Cloud", new Vector2((i + 0.4f) / 5, 1), new Vector2(0, -130 - (i % 2) * 110), new Vector2(300, 112));
            }
            var dirt = Decoration(background, "Dirt", "Dirt", new Vector2(0.5f, 0), Vector2.zero, new Vector2(1920, 110));
            dirt.anchorMin = Vector2.zero; dirt.anchorMax = new Vector2(1, 0); dirt.sizeDelta = new Vector2(0, 110);
            dirt.GetComponent<Image>().type = Image.Type.Tiled;
            var grass = Decoration(background, "Grass", "Grass", new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(1920, 64));
            grass.anchorMin = Vector2.zero; grass.anchorMax = new Vector2(1, 0); grass.sizeDelta = new Vector2(0, 64);
            grass.GetComponent<Image>().type = Image.Type.Tiled;
            Decoration(background, "Brick Left", "Brick", new Vector2(0.12f, 0.36f), Vector2.zero, new Vector2(256, 64)).GetComponent<Image>().type = Image.Type.Tiled;
            Decoration(background, "Brick Right", "Brick", new Vector2(0.88f, 0.45f), Vector2.zero, new Vector2(256, 64)).GetComponent<Image>().type = Image.Type.Tiled;
        }
        private static RectTransform Decoration(Transform parent, string name, string art, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var rect = Rect(parent, name); Place(rect, anchor, new Vector2(0.5f, anchor.y), offset, size);
            var image = Get<Image>(rect.gameObject);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Foundation/" + art + ".png");
            if (image.sprite == null) throw new InvalidOperationException("Missing existing background sprite: " + art);
            image.raycastTarget = false;
            return rect;
        }
        private static void EnsureIcons()
        {
            foreach (string name in new[] { "MenuIcon", "HelpIcon" })
            {
                string path = "Assets/UI/Phase4/" + name + ".png";
                if (File.Exists(path)) continue;
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
                try
                {
                    var colors = new Color32[256];
                    string[] question = { "01110", "10001", "00001", "00010", "00100", "00000", "00100" };
                    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        bool set = name == "MenuIcon" ? x >= 2 && x <= 13 && (y == 3 || y == 4 || y == 7 || y == 8 || y == 11 || y == 12) :
                            x >= 3 && x < 13 && y >= 1 && y < 15 && question[6 - (y - 1) / 2][(x - 3) / 2] == '1';
                        colors[y * 16 + x] = set ? new Color32(255, 239, 190, 255) : new Color32(0, 0, 0, 0);
                    }
                    texture.SetPixels32(colors); texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
        private static EditorBuildSettingsScene[] SceneList(EditorBuildSettingsScene[] current) =>
            new[] { new EditorBuildSettingsScene(U.Menu, true), new EditorBuildSettingsScene(U.Level, true) }
                .Concat((current ?? Array.Empty<EditorBuildSettingsScene>()).Where(scene => scene.path != U.Menu && scene.path != U.Level)).ToArray();
        internal static void RegisterScenes()
        {
            EditorBuildSettings.scenes = SceneList(EditorBuildSettings.scenes);
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile == null || !profile.overrideGlobalScenes) continue;
                profile.scenes = SceneList(profile.scenes); EditorUtility.SetDirty(profile);
            }
        }
        [MenuItem("Tools/Zhuzbai/Validate Main Menu and UI")]
        public static void Validate()
        {
            U.RequireEditMode();
            foreach (string path in new[] { U.Menu, U.Level }) U.EditScene(path, ValidateScene, false);
            CheckSceneList(EditorBuildSettings.scenes);
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null && profile.overrideGlobalScenes) CheckSceneList(profile.scenes);
            }
            Debug.Log("Main Menu and Game UI validated in both scenes: buttons, modal panels, settings, Input System UI, safe areas, Pixel HUD, and Build Profile scene lists.");
        }
        private static void CheckSceneList(EditorBuildSettingsScene[] scenes)
        {
            if (scenes.Length < 2 || scenes[0].path != U.Menu || !scenes[0].enabled || scenes[1].path != U.Level || !scenes[1].enabled)
                throw new InvalidOperationException("Build scene list must start with enabled MainMenu and MainLevel.");
        }
        internal static void ValidateScene(Scene scene)
        {
            bool gameplay = scene.path != U.Menu;
            var controllers = U.All<GameMenuController>(scene);
            var systems = U.All<EventSystem>(scene);
            if (controllers.Length != 1 || systems.Length != 1 || systems[0].GetComponent<InputSystemUIInputModule>() == null ||
                systems[0].GetComponent<StandaloneInputModule>() != null)
                throw new InvalidOperationException("Expected one menu controller and one Input System EventSystem in " + scene.name);
            var module = systems[0].GetComponent<InputSystemUIInputModule>();
            UiInputActionSetup.Validate(module, scene.name);
            var controller = controllers[0];
            var data = new SerializedObject(controller);
            foreach (string field in new[] { "settingsPanel", "controlsPanel", "modalShade", gameplay ? "pausePanel" : "mainPanel" })
                if (data.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing menu panel: " + field);
            if (data.FindProperty("gameplayScene").boolValue != gameplay) throw new InvalidOperationException("Incorrect menu scene mode.");
            var canvas = controller.GetComponent<Canvas>();
            var scaler = controller.GetComponent<CanvasScaler>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay || controller.GetComponent<GraphicRaycaster>() == null ||
                scaler == null || scaler.referenceResolution != new Vector2(1920, 1080) || scaler.matchWidthOrHeight != 0.5f ||
                scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || controller.GetComponentInChildren<MenuSafeArea>(true) == null)
                throw new InvalidOperationException("Invalid Canvas scale/anchors/safe-area configuration.");
            var actions = controller.GetComponentsInChildren<MenuButtonAction>(true);
            MenuCommand[] expected = gameplay ? new[] { MenuCommand.Pause, MenuCommand.Controls, MenuCommand.Resume, MenuCommand.Settings, MenuCommand.MainMenu, MenuCommand.Back } :
                new[] { MenuCommand.Play, MenuCommand.Settings, MenuCommand.Controls, MenuCommand.Quit, MenuCommand.Back };
            foreach (MenuCommand command in expected)
                if (!actions.Any(action => new SerializedObject(action).FindProperty("command").enumValueIndex == (int)command))
                    throw new InvalidOperationException("Missing functional button: " + command);
            foreach (var action in actions)
                if (new SerializedObject(action).FindProperty("controller").objectReferenceValue != controller || action.GetComponent<Button>() == null)
                    throw new InvalidOperationException("Button controller is not connected: " + action.name);
            var settings = controller.GetComponentsInChildren<AudioSettingsPanel>(true);
            if (settings.Length != 1) throw new InvalidOperationException("Expected one settings panel per scene.");
            var settingsData = new SerializedObject(settings[0]);
            foreach (string field in new[] { "sfxSlider", "musicSlider", "fullscreen", "sfxValue", "musicValue" })
                if (settingsData.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing setting: " + field);
            var sfx = settingsData.FindProperty("sfxSlider").objectReferenceValue as Slider;
            var music = settingsData.FindProperty("musicSlider").objectReferenceValue as Slider;
            if (sfx == music || sfx.minValue != 0 || sfx.maxValue != 1 || music.minValue != 0 || music.maxValue != 1)
                throw new InvalidOperationException("SFX and music must have distinct 0-100% sliders.");
            if (gameplay)
            {
                if (U.All<PixelNumberDisplay>(scene).Length != 2 || U.All<FoundationHud>(scene).Length != 1 ||
                    data.FindProperty("player").objectReferenceValue == null || data.FindProperty("playerInput").objectReferenceValue == null)
                    throw new InvalidOperationException("Existing Pixel HUD/player input references were not retained.");
                if (U.All<Text>(scene).Any(text => text.name == "Phase Notice" && text.gameObject.activeSelf))
                    throw new InvalidOperationException("Old always-visible controls instructions must be hidden.");
                var input = (PlayerInput)data.FindProperty("playerInput").objectReferenceValue;
                if (input.actions == null || input.actions.name != "PlayerControls" || input.defaultActionMap != "Player" || input.notificationBehavior != PlayerNotifications.SendMessages)
                    throw new InvalidOperationException("Existing PlayerInput setup is invalid.");
            }
        }
    }
}

