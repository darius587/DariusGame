using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zhuzbai.Platformer.Editor
{
    public static class PixelHudSetup
    {
        private const string ScenePath = "Assets/Scenes/MainLevel.unity";
        private const string Art = "Assets/UI/PixelHUD/";
        private const string SafeAreaName = "Pixel HUD Safe Area";
        private static readonly string[] Digits = {
            "111/101/101/101/111", "010/110/010/010/111", "111/001/111/100/111",
            "111/001/111/001/111", "101/101/111/001/001", "111/100/111/001/111",
            "111/100/111/101/111", "111/001/010/010/010", "111/101/111/101/111",
            "111/101/111/001/111", "001/001/010/100/100"
        };

        [MenuItem("Tools/Zhuzbai/Setup Player HUD")]
        public static void Apply()
        {
            RequireEditMode();
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("MainLevel must exist. Pixel HUD never generates a level.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            FoundationHud hud = FindHud(scene);
            var data = new SerializedObject(hud);
            var health = data.FindProperty("health").objectReferenceValue as PlayerHealth;
            var session = data.FindProperty("session").objectReferenceValue as LevelSession;
            if (health == null || session == null || health.gameObject.scene != scene || session.gameObject.scene != scene)
                throw new InvalidOperationException("Restore the existing HUD's MainLevel health/session references before applying.");
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                throw new InvalidOperationException("The existing HUD must have a Screen Space Overlay Canvas.");
            var coin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Foundation/Coin.png");
            if (coin == null) throw new InvalidOperationException("Existing Coin.png sprite is missing; no gameplay assets will be replaced.");
            Transform existing = hud.transform.Find(SafeAreaName);
            if (existing != null && existing.GetComponent<PixelHudSafeArea>() == null)
                throw new InvalidOperationException("The Pixel HUD Safe Area name is occupied by an unrelated object.");

            EnsureFolder(Art.TrimEnd('/'));
            EnsureFolder(Art + "Glyphs");
            Sprite heart = CreateSprite(Art + "Heart.png", 16, 16, HeartPixel);
            var glyphs = new Sprite[11];
            for (int i = 0; i < glyphs.Length; i++)
            {
                string[] rows = Digits[i].Split('/');
                glyphs[i] = CreateSprite(Art + "Glyphs/" + (i == 10 ? "Slash" : i.ToString()) + ".png", 3, 5,
                    (x, y) => rows[4 - y][x] == '1' ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0));
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply Pixel HUD");
            var safe = RectChild(hud.transform, SafeAreaName);
            Stretch(safe);
            var safeComponent = Component<PixelHudSafeArea>(safe.gameObject);
            var row = RectChild(safe, "Counters");
            Position(row, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(364, 56));

            PixelNumberDisplay hp = Counter(row, "Health", 0, heart, glyphs,
                new Color32(255, 231, 225, 255), new Color32(219, 93, 101, 100));
            PixelNumberDisplay coins = Counter(row, "Coins", 188, coin, glyphs,
                new Color32(255, 230, 157, 255), new Color32(231, 181, 66, 100));
            SetReference(safeComponent, "counters", row);
            var layoutData = new SerializedObject(safeComponent);
            layoutData.FindProperty("preferredScale").floatValue = 1;
            layoutData.ApplyModifiedProperties();

            var banner = RectChild(safe, "Level Complete");
            Position(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 64));
            var backdrop = Component<Image>(banner.gameObject);
            Undo.RecordObject(backdrop, "Style completion banner");
            backdrop.color = new Color(0.035f, 0.055f, 0.09f, 0.88f);
            backdrop.raycastTarget = false;
            var labelRect = RectChild(banner, "Label");
            Stretch(labelRect);
            var label = Component<Text>(labelRect.gameObject);
            Undo.RecordObject(label, "Style completion label");
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "LEVEL COMPLETE!";
            label.fontSize = 28;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color32(255, 230, 157, 255);
            label.raycastTarget = false;
            Undo.RecordObject(banner.gameObject, "Hide completion until exit");
            banner.gameObject.SetActive(false);

            // Keep the old object/references available for undo and Phase 1 compatibility, but hide it.
            var oldStatus = data.FindProperty("status").objectReferenceValue as Text;
            if (oldStatus != null)
            {
                Undo.RecordObject(oldStatus.gameObject, "Replace plain HUD status");
                oldStatus.gameObject.SetActive(false);
            }
            // Move the existing controls hint below the panels, without changing its content.
            Transform hint = hud.transform.Find("Phase Notice") ?? safe.Find("Phase Notice");
            if (hint != null && hint.TryGetComponent<Text>(out var hintText))
            {
                if (hint.parent != safe) Undo.SetTransformParent(hint, safe, "Keep controls hint in safe area");
                var rect = (RectTransform)hint;
                Undo.RecordObject(rect, "Position controls hint below counters");
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -92);
                rect.sizeDelta = new Vector2(-48, 56);
                Undo.RecordObject(hintText, "Fit controls hint on mobile");
                hintText.fontSize = 16;
                hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
                hintText.raycastTarget = false;
            }

            SetReference(hud, "healthCounter", hp);
            SetReference(hud, "coinCounter", coins);
            SetReference(hud, "completionBanner", banner.gameObject);
            var scaler = Component<CanvasScaler>(canvas.gameObject);
            Undo.RecordObject(scaler, "Configure responsive HUD scaling");
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Undo.RecordObject(canvas, "Use pixel-aligned HUD canvas");
            canvas.pixelPerfect = true;

            int total = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CoinCollectible>(true))
                .Count(item => item.gameObject.activeInHierarchy);
            hp.SetValue($"{health.MaximumHealth}/{health.MaximumHealth}", true);
            coins.SetValue($"00/{total:00}", true);
            ValidateHud(hud, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MainLevel HUD changes.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = row.gameObject;
            Debug.Log("Pixel HUD applied and saved. Health and coin counters use the existing systems; gameplay objects were preserved.");
        }

        [MenuItem("Tools/Zhuzbai/Validate Player HUD")]
        public static void Validate()
        {
            RequireEditMode();
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open MainLevel before validating the Pixel HUD.");
            ValidateHud(FindHud(scene), scene);
            Debug.Log("Pixel HUD validated: counters, pixel glyphs, icons, safe-area anchors, Canvas scaling, health/session references, and completion banner.");
        }

        [MenuItem("Tools/Zhuzbai/Test Player Health UI")]
        public static void TestDamage()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode first. This test calls the existing health system without saving any changes.");
            var hud = FindHud(SceneManager.GetSceneByPath(ScenePath));
            var data = new SerializedObject(hud);
            var health = data.FindProperty("health").objectReferenceValue as PlayerHealth;
            if (health == null) throw new InvalidOperationException("HUD health reference is missing.");
            health.TakeDamage(1);
            Debug.Log($"HUD damage test: health is {health.CurrentHealth}/{health.MaximumHealth}. Existing invulnerability and respawn rules apply.");
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Stop Play Mode and close Prefab Mode before applying or validating the HUD.");
        }
        private static FoundationHud FindHud(Scene scene)
        {
            var items = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FoundationHud>(true)).ToArray();
            if (items.Length != 1) throw new InvalidOperationException($"Expected one existing FoundationHud in MainLevel; found {items.Length}.");
            return items[0];
        }
        private static void ValidateHud(FoundationHud hud, Scene scene)
        {
            var data = new SerializedObject(hud);
            foreach (string property in new[] { "health", "session", "healthCounter", "coinCounter", "completionBanner" })
            {
                var value = data.FindProperty(property).objectReferenceValue;
                GameObject owner = value is Component component ? component.gameObject : value as GameObject;
                if (owner == null || owner.scene != scene) throw new InvalidOperationException("Missing MainLevel HUD reference: " + property);
            }
            if (!hud.enabled || !hud.gameObject.activeInHierarchy) throw new InvalidOperationException("FoundationHud must be enabled and active.");
            var canvas = hud.GetComponent<Canvas>();
            var scaler = hud.GetComponent<CanvasScaler>();
            if (canvas == null || !canvas.enabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.pixelPerfect ||
                scaler == null || !scaler.enabled || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                throw new InvalidOperationException("Pixel HUD requires an enabled pixel-perfect overlay Canvas with responsive CanvasScaler.");
            var oldStatus = data.FindProperty("status").objectReferenceValue as Text;
            if (oldStatus != null && oldStatus.gameObject.activeSelf) throw new InvalidOperationException("Old plain status text is still visible.");
            var safe = hud.transform.Find(SafeAreaName) as RectTransform;
            var row = safe != null ? safe.Find("Counters") as RectTransform : null;
            if (safe == null || !safe.gameObject.activeSelf || safe.GetComponent<PixelHudSafeArea>() == null ||
                row == null || !row.gameObject.activeSelf || row.anchorMin != new Vector2(0, 1) || row.anchorMax != new Vector2(0, 1))
                throw new InvalidOperationException("Missing safe-area wrapper or top-left counters row.");
            var layout = new SerializedObject(safe.GetComponent<PixelHudSafeArea>());
            if (layout.FindProperty("counters").objectReferenceValue != row) throw new InvalidOperationException("Safe-area layout is not connected to counters.");
            if (hud.GetComponentsInChildren<PixelNumberDisplay>(true).Length != 2)
                throw new InvalidOperationException("Expected exactly two pixel number counters; check for duplicates.");
            foreach (string name in new[] { "Health", "Coins" })
            {
                var panel = row.Find(name) as RectTransform;
                var icon = panel != null ? panel.Find("Icon")?.GetComponent<Image>() : null;
                var number = panel != null ? panel.Find("Value")?.GetComponent<PixelNumberDisplay>() : null;
                if (panel == null || panel.sizeDelta != new Vector2(176, 56) || panel.GetComponent<Image>() == null ||
                    panel.GetComponent<Image>().color.a <= 0 || panel.GetComponent<Image>().color.a >= 1 ||
                    icon == null || icon.sprite == null || number == null || !number.enabled || !number.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Invalid counter panel, icon or bitmap display: " + name);
                if (panel.anchoredPosition != new Vector2(name == "Health" ? 0 : 188, 0))
                    throw new InvalidOperationException("Counter spacing must be horizontal with a consistent 12-pixel gap.");
                string property = name == "Health" ? "healthCounter" : "coinCounter";
                if (data.FindProperty(property).objectReferenceValue != number) throw new InvalidOperationException("Counter is wired to the wrong display: " + name);
                var digits = new SerializedObject(number);
                var glyphs = digits.FindProperty("glyphs");
                var slots = digits.FindProperty("slots");
                if (glyphs.arraySize != 11 || slots.arraySize < 5) throw new InvalidOperationException("Incomplete pixel number glyphs/slots.");
                for (int i = 0; i < 11; i++) ValidateSprite(glyphs.GetArrayElementAtIndex(i).objectReferenceValue as Sprite);
                for (int i = 0; i < slots.arraySize; i++)
                    if (slots.GetArrayElementAtIndex(i).objectReferenceValue == null) throw new InvalidOperationException("Missing digit Image slot.");
                ValidateSprite(icon.sprite);
                string iconPath = name == "Health" ? Art + "Heart.png" : "Assets/Sprites/Foundation/Coin.png";
                if (icon.sprite != AssetDatabase.LoadAssetAtPath<Sprite>(iconPath))
                    throw new InvalidOperationException("Incorrect health/coin icon: " + name);
                if (icon.raycastTarget) throw new InvalidOperationException("HUD icons must not intercept mobile touches.");
            }
            var banner = (GameObject)data.FindProperty("completionBanner").objectReferenceValue;
            if (banner.GetComponentInChildren<Text>(true) == null) throw new InvalidOperationException("Missing level-completion label.");
        }
        private static void ValidateSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture.filterMode != FilterMode.Point || sprite.texture.mipmapCount != 1)
                throw new InvalidOperationException("HUD sprites must use Point filtering without mipmaps.");
        }

        private static PixelNumberDisplay Counter(RectTransform row, string name, float x, Sprite icon, Sprite[] glyphs, Color tint, Color accent)
        {
            var shadow = RectChild(row, name + " Shadow");
            Position(shadow, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + 2, -3), new Vector2(176, 56));
            var shadowImage = Component<Image>(shadow.gameObject);
            Undo.RecordObject(shadowImage, "Style HUD shadow");
            shadowImage.color = new Color(0, 0, 0, 0.2f);
            shadowImage.raycastTarget = false;
            var panel = RectChild(row, name);
            Position(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, 0), new Vector2(176, 56));
            var background = Component<Image>(panel.gameObject);
            Undo.RecordObject(background, "Style counter background");
            background.color = new Color(0.035f, 0.055f, 0.09f, 0.76f);
            background.raycastTarget = false;
            var line = RectChild(panel, "Highlight");
            Position(line, new Vector2(0, 1), new Vector2(0, 1), new Vector2(2, -2), new Vector2(172, 2));
            var highlight = Component<Image>(line.gameObject);
            Undo.RecordObject(highlight, "Style pixel accent");
            highlight.color = accent;
            highlight.raycastTarget = false;
            var picture = RectChild(panel, "Icon");
            Position(picture, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(32, 32));
            var image = Component<Image>(picture.gameObject);
            Undo.RecordObject(image, "Assign HUD icon");
            image.sprite = icon;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            var value = RectChild(panel, "Value");
            Position(value, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(108, 24));
            var display = Component<PixelNumberDisplay>(value.gameObject);
            var serialized = new SerializedObject(display);
            var spriteArray = serialized.FindProperty("glyphs");
            spriteArray.arraySize = glyphs.Length;
            for (int i = 0; i < glyphs.Length; i++) spriteArray.GetArrayElementAtIndex(i).objectReferenceValue = glyphs[i];
            var slotArray = serialized.FindProperty("slots");
            slotArray.arraySize = Mathf.Max(5, slotArray.arraySize);
            for (int i = 0; i < slotArray.arraySize; i++)
            {
                var slot = RectChild(value, "Glyph " + i);
                slotArray.GetArrayElementAtIndex(i).objectReferenceValue = Component<Image>(slot.gameObject);
            }
            serialized.FindProperty("color").colorValue = tint;
            serialized.ApplyModifiedProperties();
            return display;
        }
        private static T Component<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(go);
        }
        private static RectTransform RectChild(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found as RectTransform ?? throw new InvalidOperationException("Expected UI RectTransform: " + name);
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create Pixel HUD element");
            return (RectTransform)go.transform;
        }
        private static void Position(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(rect, "Position Pixel HUD");
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
        private static void Stretch(RectTransform rect)
        {
            Undo.RecordObject(rect, "Stretch Pixel HUD");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        private static Sprite CreateSprite(string path, int width, int height, Func<int, int, Color32> pixel)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Existing HUD art path is occupied: " + path);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var colors = new Color32[width * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++) colors[y * width + x] = pixel(x, y);
                texture.SetPixels32(colors);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Color32 HeartPixel(int x, int y)
        {
            string[] rows = { "..###..###..", ".##########.", "############", "############", "############",
                ".##########.", "..########..", "...######...", "....####....", ".....##....." };
            int column = x - 2, row = 12 - y;
            bool Filled(int px, int py) => py >= 0 && py < rows.Length && px >= 0 && px < 12 && rows[py][px] == '#';
            if (!Filled(column, row)) return new Color32(0, 0, 0, 0);
            if (!Filled(column - 1, row) || !Filled(column + 1, row) || !Filled(column, row - 1) || !Filled(column, row + 1))
                return new Color32(119, 30, 49, 255);
            if (row <= 2 && column >= 2 && column <= 4) return new Color32(255, 170, 164, 255);
            return row >= 6 ? new Color32(194, 42, 63, 255) : new Color32(239, 65, 85, 255);
        }
    }
}
