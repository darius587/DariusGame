using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;
using UI = Zhuzbai.Platformer.Editor.GameMenuSetup;
using Object = UnityEngine.Object;

namespace Zhuzbai.Platformer.Editor
{
    public static class MultiDeviceControlsSetup
    {
        private const string Art = "Assets/UI/MultiDeviceControls/";
        private const string Marker = Art + "PixelDisc.png";
        private const string PlayerPrefab = "Assets/Prefabs/Foundation/Player.prefab";
        private const string Actions = "Assets/Settings/PlayerControls.inputactions";
        private static string[] Scenes => new[] { U.Level, "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity" }
            .Concat(File.Exists("Assets/Scenes/Level4.unity") ? new[] { "Assets/Scenes/Level4.unity" } : Array.Empty<string>()).ToArray();
        private const string RootName = "Multi-Device Controls";
        private static bool Installed => File.Exists(Marker);

        [MenuItem("Tools/Zhuzbai/Setup Mobile Controls")]
        public static void Apply()
        {
            U.RequireEditMode(); ValidateBindings();
            foreach (string path in Scenes) U.EditScene(path, scene => {
                Require(U.All<PlayerFoundation>(scene).Length == 1 && U.All<GameMenuController>(scene).Length == 1 && U.All<EventSystem>(scene).Length == 1,
                    "Expected the existing player, Phase 4 interface and EventSystem in " + scene.name);
            }, false);
            EnsureArt(); ApplyPrefabIfInstalled();
            foreach (string path in Scenes) U.EditScene(path, ApplySceneIfInstalled);
            AssetDatabase.SaveAssets(); Validate();
            Debug.Log("Multi-device controls installed in all three existing levels. Keyboard, mouse and virtual Gamepad share PlayerControls. See MULTI_DEVICE_CONTROLS_SETUP.md.");
        }
        internal static void ApplyPrefabIfInstalled()
        {
            if (!Installed) return;
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try { ConfigurePlayer(root.GetComponent<PlayerFoundation>()); PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab, out bool saved); Require(saved, "Could not save existing player prefab."); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void ConfigurePlayer(PlayerFoundation player)
        {
            Require(player != null, "Existing PlayerFoundation required.");
            var input = player.GetComponent<PlayerInput>();
            Require(input != null && player.GetComponents<PlayerInput>().Length == 1, "Expected one existing PlayerInput.");
            Require(input.actions == AssetDatabase.LoadAssetAtPath<InputActionAsset>(Actions), "PlayerInput must use existing PlayerControls.");
            input.neverAutoSwitchControlSchemes = true;
            input.defaultControlScheme = "";
            input.defaultActionMap = "Player";
            input.notificationBehavior = PlayerNotifications.SendMessages;
            input.enabled = true;
            Get<MultiDeviceInput>(player.gameObject); Dirty(input);
        }
        internal static void ApplySceneIfInstalled(Scene scene)
        {
            if (!Installed) return;
            var player = U.All<PlayerFoundation>(scene).Single(); ConfigurePlayer(player);
            var controller = U.All<GameMenuController>(scene).Single();
            var safe = controller.transform.Find("Safe Area") as RectTransform;
            Require(safe != null && safe.GetComponent<MenuSafeArea>() != null, "Existing menu safe area required in " + scene.name);
            var existing = safe.Find(RootName);
            Require(existing == null || existing.GetComponent<MultiDeviceControlsPanel>() != null, "Controls name occupied by an unrelated object.");
            var root = UI.Rect(safe, RootName); Stretch(root); root.SetAsFirstSibling();
            var panel = Get<MultiDeviceControlsPanel>(root.gameObject); Get<CanvasGroup>(root.gameObject);
            U.Reference(panel, "player", player);
            var left = UI.Rect(root, "Joystick Dock");
            UI.Place(left, Vector2.zero, Vector2.zero, new Vector2(36, 32), new Vector2(260, 260));
            Image(left, new Color(0.04f, 0.08f, 0.1f, 0.55f), false);
            var edge = UI.Rect(left, "Ring"); Center(edge, new Vector2(248, 248));
            Image(edge, new Color(0.14f, 0.25f, 0.23f, 0.65f), false);
            var inner = UI.Rect(left, "Inner"); Center(inner, new Vector2(222, 222));
            Image(inner, new Color(0.03f, 0.06f, 0.08f, 0.7f), false);
            var target = UI.Rect(left, "Stick"); Center(target, new Vector2(260, 260));
            var targetImage = Get<Image>(target.gameObject); targetImage.color = Color.clear; targetImage.raycastTarget = true;
            var stick = Get<SafeOnScreenStick>(target.gameObject);
            SetPath(stick, "<Gamepad>/leftStick");
            stick.movementRange = 72; stick.useIsolatedInputActions = false;
            stick.behaviour = OnScreenStick.Behaviour.RelativePositionWithStaticOrigin;
            Dirty(stick);
            var knob = UI.Rect(target, "Knob"); Center(knob, new Vector2(112, 112));
            Image(knob, new Color32(70, 213, 99, 220), false);
            var shine = UI.Rect(knob, "Highlight"); Center(shine, new Vector2(88, 88));
            Image(shine, new Color32(121, 245, 127, 160), false);
            var right = UI.Rect(root, "Action Dock");
            UI.Place(right, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 36), new Vector2(400, 190));
            var fire = Button(right, "FIRE", new Vector2(100, 95), "<Gamepad>/buttonWest");
            var jump = Button(right, "JUMP", new Vector2(300, 95), "<Gamepad>/buttonSouth");
            U.Reference(panel, "stick", stick); U.Reference(panel, "jump", jump); U.Reference(panel, "fire", fire);
            U.Reference(panel, "leftDock", left); U.Reference(panel, "rightDock", right);
            var system = U.All<EventSystem>(scene).Single();
            Require(system.GetComponent<StandaloneInputModule>() == null, "Legacy UI input module detected; use existing UI repair command.");
            var module = system.GetComponent<InputSystemUIInputModule>();
            Require(module != null, "Existing InputSystemUIInputModule required.");
            UiInputActionSetup.Configure(module);
            module.pointerBehavior = UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
            Dirty(module);
        }
        private static SafeOnScreenButton Button(RectTransform parent, string name, Vector2 position, string path)
        {
            var rect = UI.Rect(parent, name);
            UI.Place(rect, Vector2.zero, new Vector2(0.5f, 0.5f), position, new Vector2(176, 176));
            Image(rect, Color.white, true);
            var button = Get<SafeOnScreenButton>(rect.gameObject); SetPath(button, path);
            var label = UI.Label(rect, "Label", name, Vector2.zero, new Vector2(160, 65), 36, Color.white);
            UI.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160, 65));
            var shadow = Get<Shadow>(label.gameObject); shadow.effectColor = new Color(0, 0, 0, 0.65f); shadow.effectDistance = new Vector2(3, -3);
            StyleButton(button);
            return button;
        }
        private static void StyleButton(SafeOnScreenButton button)
        {
            var image = button.GetComponent<Image>(); Require(image != null, "Existing on-screen button background is missing.");
            bool isFire = button.controlPath == "<Gamepad>/buttonWest";
            Color background = isFire ? new Color32(134, 239, 172, 255) : new Color32(34, 197, 94, 255);
            background.a = isFire ? 0.75f : 0.70f;
            image.color = background;
            var outline = Get<Outline>(button.gameObject);
            outline.effectColor = isFire ? new Color32(209, 250, 229, 255) : new Color32(134, 239, 172, 255);
            outline.effectDistance = new Vector2(1, -1); outline.useGraphicAlpha = false;
            var label = button.transform.Find("Label");
            Require(label != null && label.GetComponent<Text>() != null, "Existing on-screen button label is missing.");
            var text = label.GetComponent<Text>(); text.color = isFire ? new Color32(16, 58, 36, 255) : Color.white;
            Dirty(image); Dirty(outline); Dirty(text);
        }
        [MenuItem("Tools/Zhuzbai/Setup Mobile Button Style")]
        public static void ApplyButtonStyle()
        {
            U.RequireEditMode();
            foreach (string path in Scenes) U.EditScene(path, scene => {
                var panels = U.All<MultiDeviceControlsPanel>(scene);
                Require(panels.Length == 1, "Apply Multi-Device Controls first in " + scene.name);
                var data = new SerializedObject(panels[0]);
                foreach (string field in new[] { "jump", "fire" })
                {
                    var button = data.FindProperty(field).objectReferenceValue as SafeOnScreenButton;
                    Require(button != null, "Missing " + field + " button in " + scene.name);
                    StyleButton(button);
                }
            });
            AssetDatabase.SaveAssets();
            Debug.Log("JUMP/FIRE green palettes applied in all three levels: JUMP 70% (pressed 80%), FIRE 75% (pressed 85%). Layout and joystick preserved.");
        }
        private static void SetPath(OnScreenControl control, string path)
        {
            // Serializing avoids the property setter creating virtual devices in Edit Mode.
            var data = new SerializedObject(control); data.FindProperty("m_ControlPath").stringValue = path; data.ApplyModifiedPropertiesWithoutUndo(); Dirty(control);
        }
        private static void Image(RectTransform rect, Color color, bool raycast)
        {
            var image = Get<Image>(rect.gameObject); image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Marker);
            image.color = color; image.raycastTarget = raycast;
        }
        private static T Get<T>(GameObject go) where T : Component { var component = go.GetComponent<T>(); return component != null ? component : go.AddComponent<T>(); }
        private static void Center(RectTransform rect, Vector2 size) => UI.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Dirty(Object value) { EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value); }
        private static void EnsureArt()
        {
            U.Folder(Art.TrimEnd('/'));
            if (!File.Exists(Marker))
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                try
                {
                    var pixels = new Color32[1024];
                    for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                        if (Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) <= 15.7f) pixels[y * 32 + x] = Color.white;
                    texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Marker, texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
            }
            AssetDatabase.ImportAsset(Marker, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Marker); importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void ValidateBindings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Actions);
            Require(asset != null && asset.controlSchemes.Count == 0, "Expected existing scheme-free PlayerControls for simultaneous devices.");
            var map = asset.FindActionMap("Player", true);
            foreach (var spec in new[] { ("Move", InputActionType.Value, new[] { "2DVector", "<Keyboard>/a", "<Keyboard>/d", "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow", "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Gamepad>/leftStick" }),
                ("Jump", InputActionType.Button, new[] { "<Keyboard>/space", "<Gamepad>/buttonSouth" }),
                ("Fire", InputActionType.Button, new[] { "<Mouse>/leftButton", "<Keyboard>/x", "<Gamepad>/buttonWest" }) })
            {
                var action = map.FindAction(spec.Item1, true); Require(action.type == spec.Item2, "Incorrect action type: " + spec.Item1);
                Require(action.expectedControlType == (spec.Item1 == "Move" ? "Vector2" : "Button"), "Incorrect expected control type: " + spec.Item1);
                foreach (string path in spec.Item3) Require(action.bindings.Any(b => b.effectivePath == path), "Missing " + spec.Item1 + " binding: " + path);
                Require(action.bindings.All(b => string.IsNullOrEmpty(b.groups)), "Binding groups would restrict simultaneous devices: " + spec.Item1);
            }
        }
        [MenuItem("Tools/Zhuzbai/Validate Mobile Controls")]
        public static void Validate()
        {
            U.RequireEditMode(); ValidateBindings();
            foreach (string path in Scenes) U.EditScene(path, scene => {
                var player = U.All<PlayerFoundation>(scene).Single(); var input = player.GetComponent<PlayerInput>();
                Require(input != null && input.enabled && player.GetComponents<PlayerInput>().Length == 1 && player.GetComponents<MultiDeviceInput>().Length == 1 && input.neverAutoSwitchControlSchemes && input.notificationBehavior == PlayerNotifications.SendMessages && input.defaultActionMap == "Player" && input.actions == AssetDatabase.LoadAssetAtPath<InputActionAsset>(Actions), "Invalid simultaneous PlayerInput configuration in " + scene.name);
                var panels = U.All<MultiDeviceControlsPanel>(scene); Require(panels.Length == 1, "Missing or duplicate control panel in " + scene.name);
                var panel = panels[0]; Require(panel.transform.parent.GetComponent<MenuSafeArea>() != null && panel.GetComponentInParent<Canvas>() == U.All<GameMenuController>(scene).Single().GetComponent<Canvas>() && panel.GetComponent<Canvas>() == null, "Controls must reuse the existing safe-area Canvas.");
                Require(Enumerable.Range(0, panel.transform.parent.childCount).Count(i => panel.transform.parent.GetChild(i).name == RootName) == 1, "Duplicate named controls roots.");
                var rect = (RectTransform)panel.transform; Require(rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one, "Controls do not stretch across safe area.");
                var sticks = U.All<OnScreenStick>(scene); var buttons = U.All<OnScreenButton>(scene);
                Require(sticks.Length == 1 && sticks[0] is SafeOnScreenStick && sticks[0].controlPath == "<Gamepad>/leftStick" && !sticks[0].useIsolatedInputActions && sticks[0].movementRange == 72, "Invalid/duplicate on-screen stick.");
                Require(buttons.Length == 2 && buttons.All(b => b is SafeOnScreenButton) && buttons.Count(b => b.controlPath == "<Gamepad>/buttonSouth") == 1 && buttons.Count(b => b.controlPath == "<Gamepad>/buttonWest") == 1, "Invalid/duplicate JUMP/FIRE controls.");
                var data = new SerializedObject(panel);
                foreach (string field in new[] { "player", "stick", "jump", "fire", "leftDock", "rightDock" }) Require(data.FindProperty(field).objectReferenceValue != null, "Missing panel reference: " + field);
                Require(data.FindProperty("player").objectReferenceValue == player, "Panel points at a different player.");
                var left = (RectTransform)data.FindProperty("leftDock").objectReferenceValue; var right = (RectTransform)data.FindProperty("rightDock").objectReferenceValue;
                Require(left.anchorMin == Vector2.zero && left.anchorMax == Vector2.zero && right.anchorMin == new Vector2(1, 0) && right.anchorMax == new Vector2(1, 0), "Incorrect bottom-corner anchors.");
                var systems = U.All<EventSystem>(scene); Require(systems.Length == 1 && systems[0].GetComponent<StandaloneInputModule>() == null, "Invalid/duplicate EventSystem.");
                var module = systems[0].GetComponent<InputSystemUIInputModule>(); Require(module != null && module.pointerBehavior == UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack, "Input System multi-touch UI configuration missing.");
                UiInputActionSetup.Validate(module, scene.name);
                var canvas = panel.GetComponentInParent<Canvas>(); var scaler = canvas.GetComponent<CanvasScaler>();
                Require(canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.GetComponent<GraphicRaycaster>() != null && scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && panel.GetComponent<CanvasGroup>() != null, "Invalid control Canvas/raycaster/scaling.");
                Require(panel.GetComponentsInChildren<Image>(true).All(image => image.sprite != null || image.color.a == 0), "Missing control art.");
            }, false);
            Debug.Log("Multi-device structural validation passed in all three levels: shared bindings, controls, device policy, UI module, safe-area anchors and references. Run the Play Mode checklist.");
        }
    }
}
