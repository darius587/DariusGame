using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class EnemyArenaSetup
    {
        public const string ScenePath = "Assets/Scenes/EnemyAI.unity";
        private const string ArenaName = "EnemyAI";
        private const string Prefabs = "Assets/Prefabs/Enemies";
        private const string Sprites = "Assets/Sprites/";
        private const string CoinPath = "Assets/Prefabs/Foundation/Coin.prefab";
        private const string GatePath = "Assets/Prefabs/Foundation/ExitGate.prefab";
        private static Material PixelMaterial => Load<Material>(Sprites + "Foundation/PixelUnlit.mat");

        [MenuItem("Tools/Zhuzbai/Setup Enemy Arena")]
        public static void Generate()
        {
            U.RequireEditMode();
            if (File.Exists(ScenePath))
            {
                Validate();
                return;
            }

            Require(!SceneManager.GetSceneByPath(U.Menu).isDirty, "Save MainMenu before running the arena setup.");
            foreach (string name in new[] { "Patroller", "Shooter", "Charger", "EnemyProjectile" })
                Require(!File.Exists(PrefabPath(name)), "Existing prefab will not be overwritten: " + PrefabPath(name));
            U.EditScene(U.Level, scene => ValidateTemplate(scene), false);
            U.EditScene(U.Menu, scene =>
                Require(MainPanel(U.All<GameMenuController>(scene).Single()) != null, "MainMenu has no Main panel."), false);

            int walls = EnsureWallsLayer();
            U.Folder(Prefabs);
            EnemyArenaPresentationSetup.EnsureSprites();
            Require(AssetDatabase.CopyAsset(U.Level, ScenePath), "Could not copy MainLevel to EnemyAI.");
            U.EditScene(ScenePath, scene =>
            {
                GameObject projectile = CreateProjectile();
                GameObject patroller = CreateEnemy<PatrollerEnemy>("Patroller", "Phase5/Enemy", 0.92f,
                    new Vector2(0.78f, 0.80f), new Color32(100, 240, 185, 255), walls);
                GameObject shooter = CreateEnemy<ShooterEnemy>("Shooter", EnemyArenaPresentationSetup.RobotArt, 1f,
                    new Vector2(0.80f, 0.96f), Color.white, walls, projectile);
                GameObject charger = CreateEnemy<ChargerEnemy>("Charger", "Phase5/Bomb", 1.06f,
                    new Vector2(0.92f, 0.95f), new Color32(255, 188, 93, 255), walls);
                BuildArena(scene, walls, patroller, shooter, charger);
                ValidateArena(scene);
            });
            RegisterScene();
            U.EditScene(U.Menu, scene =>
            {
                GameMenuSetup.BuildStageSelection(U.All<GameMenuController>(scene).Single());
            });
            Validate();
        }

        [MenuItem("Tools/Zhuzbai/Validate Enemy Arena")]
        public static void Validate()
        {
            U.RequireEditMode();
            U.EditScene(ScenePath, ValidateArena, false);
            U.EditScene(U.Menu, GameMenuSetup.ValidateStageSelection, false);
            var scenes = EditorBuildSettings.scenes;
            string[] campaign = { U.Menu, U.Level, "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity", "Assets/Scenes/Level4.unity" };
            Require(scenes.Take(campaign.Length).Where(item => item.enabled).Select(item => item.path).SequenceEqual(campaign),
                "The campaign build order has changed.");
            Require(scenes.Count(item => item.enabled && item.path == ScenePath) == 1, "Enable EnemyAI in the global Scene List.");
            Debug.Log("Enemy Arena validated: three enemies, three coins, cover, menu, audio, touch controls, and scene registration.");
        }

        private static void BuildArena(Scene scene, int walls, GameObject patroller, GameObject shooter, GameObject charger)
        {
            var session = U.All<LevelSession>(scene).Single();
            var definition = session.GetComponent<LevelDefinition>();
            var player = U.All<PlayerFoundation>(scene).Single();
            var follow = U.All<CameraFollow2D>(scene).Single();
            var camera = follow.GetComponent<Camera>();
            foreach (string name in new[] { "Terrain", "Brick Platforms", "Background", "Coins" })
                Object.DestroyImmediate(session.transform.Find(name).gameObject);
            foreach (var gate in U.All<ExitGate>(scene)) Object.DestroyImmediate(gate.gameObject);

            Transform arena = Child(null, "Enemy Arena", Vector2.zero);
            Transform terrain = Child(arena, "Terrain and Cover", Vector2.zero);
            Solid(terrain, "Arena Floor", new Vector2(14, -0.75f), new Vector2(28, 1.5f), new Color32(112, 142, 167, 255), walls);
            Solid(terrain, "Shooter Cover Wall", new Vector2(12, 0.75f), new Vector2(0.9f, 1.5f), new Color32(170, 188, 218, 255), walls);
            Solid(terrain, "Patroller Overlook", new Vector2(5.7f, 1.45f), new Vector2(3.1f, 0.35f), new Color32(119, 185, 169, 255), walls);
            Solid(terrain, "Charger Bypass", new Vector2(22.1f, 1.85f), new Vector2(3.6f, 0.35f), new Color32(205, 163, 124, 255), walls);
            // Reuse the template's physical boundaries at the edges of the shorter room.
            ResizeBoundary(scene, "LeftBoundary", -0.5f, walls);
            ResizeBoundary(scene, "RightBoundary", 28.5f, walls);
            var fall = U.All<FallHazard>(scene).Single();
            fall.transform.position = new Vector3(14, -5, 0);
            fall.GetComponent<BoxCollider2D>().size = new Vector2(34, 2);

            Transform backdrop = Child(arena, "Backdrop", Vector2.zero);
            var stone = Sprite(backdrop, "Dark Stone", "ShadowKing/Stone", new Vector2(14, 4.1f), -30);
            stone.drawMode = SpriteDrawMode.Tiled;
            stone.size = new Vector2(28, 12);
            stone.color = new Color32(44, 52, 76, 255);
            foreach (float x in new[] { 2.1f, 9.4f, 16.6f, 25.9f })
            {
                var pillar = Sprite(backdrop, "Stone Pillar", "ShadowKing/Pillar", new Vector2(x, 2.6f), -25);
                pillar.transform.localScale = new Vector3(1.5f, 2.2f, 1);
                pillar.color = new Color32(81, 87, 115, 255);
                Sprite(backdrop, "Torch", "ShadowKing/Torch", new Vector2(x, 3.2f), -20);
            }

            Transform objectives = Child(arena, "Objectives", Vector2.zero);
            Vector2[] coinPositions = { new Vector2(5.7f, 2.12f), new Vector2(12, 2.05f), new Vector2(22.1f, 2.55f) };
            for (int i = 0; i < coinPositions.Length; i++)
                Instance(Load<GameObject>(CoinPath), objectives, coinPositions[i]).name = "Coin " + (i + 1);
            Instance(Load<GameObject>(GatePath), objectives, new Vector2(26.4f, 1)).name = "Exit Gate";

            Transform enemies = Child(arena, "Enemies", Vector2.zero);
            Transform waypoints = Child(arena, "World Waypoints", Vector2.zero);
            PlaceEnemy<PatrollerEnemy>(patroller, enemies, waypoints, new Vector2(5.1f, 0.50f), 3.9f, 8.7f, player, camera);
            PlaceEnemy<ShooterEnemy>(shooter, enemies, waypoints, new Vector2(15.6f, 0.56f), 14.0f, 17.4f, player, camera);
            PlaceEnemy<ChargerEnemy>(charger, enemies, waypoints, new Vector2(21.3f, 0.57f), 19.0f, 25.2f, player, camera);

            player.transform.position = new Vector3(2, 0.83f, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            definition.SpawnPoint.position = player.transform.position;
            Set(definition, "levelNumber", 5);
            Set(definition, "levelName", "Enemy Arena");
            Set(definition, "requiredCoins", 3);
            Set(definition, "nextScene", "");
            Set(definition, "generatedLayoutScene", ArenaName);
            Set(definition, "objective", "Collect all three coins and reach the exit. Use the cover wall to break the Shooter's aim, and jump onto the platforms to dodge melee attacks.");
            camera.backgroundColor = new Color32(19, 24, 43, 255);
            follow.transform.position = new Vector3(8, 2.7f, -10);
            Set(follow, "minimum", new Vector2(0, 2.7f));
            Set(follow, "maximum", new Vector2(28, 3.1f));
            Set(follow, "horizontalWorldBounds", new Vector2(0, 28));

            var menu = U.All<GameMenuController>(scene).Single();
            Set(menu, "replayScene", ArenaName);
            menu.transform.Find("Safe Area/Victory/Title").GetComponent<Text>().text = "ARENA CLEARED!";
            menu.transform.Find("Safe Area/How To Play/Goals").GetComponent<Text>().text =
                "1. Collect three coins to unlock the exit.\n\n2. Use cover to block the Shooter's aim.\n\n3. Jump over the Charger or take the raised route.";
            var bridge = U.All<GameplayAudioBridge>(scene).Single();
            U.References(bridge, "coins", U.All<CoinCollectible>(scene));
        }

        private static GameObject CreateEnemy<T>(string name, string art, float scale, Vector2 hitbox, Color color,
            int walls, GameObject projectile = null) where T : MonoBehaviour
        {
            var root = new GameObject(name, typeof(Rigidbody2D), typeof(BoxCollider2D));
            try
            {
                var body = root.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.useFullKinematicContacts = true;
                root.GetComponent<BoxCollider2D>().size = hitbox;
                var visual = Sprite(root.transform, "Pixel Art", art, Vector2.zero, 5);
                visual.transform.localScale = Vector3.one * scale;
                visual.color = color;
                var controller = root.AddComponent<T>();
                var perception = root.GetComponent<EnemyPerception2D>();
                U.Reference(perception, "visual", visual);
                U.Reference(perception, "sightOrigin", Child(root.transform, "Sight Origin", new Vector2(0, 0.24f)));
                Set(perception, "walls", 1 << walls);
                Set(root.GetComponent<EnemyHealth>(), "maximumHealth", name == "Charger" ? 3 : 2);

                if (controller is ShooterEnemy)
                {
                    var line = WarningLine(root.transform, "Aim Warning", new Color32(255, 214, 100, 255), 0.065f, true);
                    U.Reference(controller, "aimWarning", line);
                    U.Reference(controller, "projectilePrefab", projectile.GetComponent<EnemyProjectile>());
                }
                else
                {
                    Set(controller, "warningColor", (Color)new Color32(255, 55, 55, 255));
                    if (controller is ChargerEnemy)
                    {
                        var arrow = WarningLine(root.transform, "Charge Direction", new Color32(255, 116, 75, 255), 0.10f, false);
                        arrow.positionCount = 3;
                        arrow.SetPositions(new[] { new Vector3(0.62f, 0.12f), new Vector3(0.94f, 0.32f), new Vector3(0.62f, 0.52f) });
                        U.Reference(controller, "dashDirectionIndicator", arrow);
                    }
                }
                return SavePrefab(root, name);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateProjectile()
        {
            var visual = Sprite(null, "EnemyProjectile", EnemyArenaPresentationSetup.PulseArt, Vector2.zero, 6);
            try
            {
                visual.transform.localScale = Vector3.one * 0.6f;
                visual.gameObject.AddComponent<EnemyProjectile>();
                return SavePrefab(visual.gameObject, "EnemyProjectile");
            }
            finally { Object.DestroyImmediate(visual.gameObject); }
        }

        private static LineRenderer WarningLine(Transform parent, string name, Color color, float width, bool worldSpace)
        {
            var line = Child(parent, name, Vector2.zero).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = PixelMaterial;
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
            line.sortingOrder = 8;
            line.useWorldSpace = worldSpace;
            line.enabled = false;
            return line;
        }

        private static void PlaceEnemy<T>(GameObject prefab, Transform parent, Transform waypoints, Vector2 position,
            float left, float right, PlayerFoundation player, Camera camera) where T : MonoBehaviour
        {
            GameObject enemy = Instance(prefab, parent, position);
            var perception = enemy.GetComponent<EnemyPerception2D>();
            U.Reference(perception, "player", player.GetComponent<PlayerHealth>());
            U.Reference(perception, "gameplayCamera", camera);
            var route = enemy.GetComponent<T>();
            U.Reference(route, "waypointA", Child(waypoints, enemy.name + " A", new Vector2(left, position.y)));
            U.Reference(route, "waypointB", Child(waypoints, enemy.name + " B", new Vector2(right, position.y)));
        }

        private static void ValidateTemplate(Scene scene)
        {
            var player = U.All<PlayerFoundation>(scene).Single();
            var controls = U.All<MultiDeviceControlsPanel>(scene).Single();
            var menu = U.All<GameMenuController>(scene).Single();
            var menuData = new SerializedObject(menu);
            var controlsData = new SerializedObject(controls);
            var input = player.GetComponent<PlayerInput>();
            var canvas = menu.GetComponent<Canvas>();
            Require(player.GetComponent<PlayerInput>() != null && player.GetComponent<PlayerHealth>() != null &&
                U.All<CameraFollow2D>(scene).Length == 1 && U.All<FoundationHud>(scene).Length == 1 &&
                U.All<AudioManager>(scene).Length == 1 && U.All<GameplayAudioBridge>(scene).Length == 1 &&
                U.All<InputSystemUIInputModule>(scene).Length == 1 && canvas != null &&
                canvas.renderMode == RenderMode.ScreenSpaceOverlay && menu.GetComponent<CanvasScaler>() != null &&
                menu.GetComponentInChildren<MenuSafeArea>(true) != null &&
                menuData.FindProperty("playerInput").objectReferenceValue == input &&
                menuData.FindProperty("pausePanel").objectReferenceValue != null &&
                menuData.FindProperty("gameOverPanel").objectReferenceValue != null &&
                menuData.FindProperty("victoryPanel").objectReferenceValue != null &&
                controlsData.FindProperty("stick").objectReferenceValue != null &&
                controlsData.FindProperty("jump").objectReferenceValue != null &&
                controlsData.FindProperty("fire").objectReferenceValue != null,
                "Expected the template player, camera, HUD, Input System, audio and touch controls.");
        }

        private static void ValidateArena(Scene scene)
        {
            ValidateTemplate(scene);
            var definition = U.All<LevelDefinition>(scene).Single();
            var player = U.All<PlayerHealth>(scene).Single();
            var camera = U.All<CameraFollow2D>(scene).Single().GetComponent<Camera>();
            var menu = U.All<GameMenuController>(scene).Single();
            int walls = LayerMask.NameToLayer("Walls");
            Require(definition.GeneratedLayoutScene == ArenaName && definition.SpawnPoint != null && definition.RequiredCoins == 3 &&
                definition.NextScene == "" && !definition.IsBossLevel && U.All<CoinCollectible>(scene).Length == 3 &&
                U.All<ExitGate>(scene).Length == 1 && new SerializedObject(menu).FindProperty("replayScene").stringValue == ArenaName,
                "EnemyAI requires its own spawn, three coins, exit gate, and replay route.");
            Require(walls >= 8 && U.All<BoxCollider2D>(scene).Any(box => box.name == "Shooter Cover Wall" &&
                box.gameObject.layer == walls && !box.isTrigger), "Shooter cover must be a solid Walls collider.");
            ValidateEnemy(U.All<PatrollerEnemy>(scene).Single(), player, camera, walls);
            var shooter = U.All<ShooterEnemy>(scene).Single();
            ValidateEnemy(shooter, player, camera, walls);
            var shooterData = new SerializedObject(shooter);
            var aimWarning = (LineRenderer)shooterData.FindProperty("aimWarning").objectReferenceValue;
            var projectile = (EnemyProjectile)shooterData.FindProperty("projectilePrefab").objectReferenceValue;
            Require(aimWarning != null && aimWarning.sharedMaterial != null && aimWarning.widthMultiplier > 0 &&
                projectile != null && projectile.GetComponent<SpriteRenderer>().sprite != null &&
                projectile.GetComponent<Collider2D>() == null, "The Shooter needs a visible aim line and a visual-only swept projectile.");
            EnemyArenaPresentationSetup.ValidateShooterArt(shooter, projectile);
            var charger = U.All<ChargerEnemy>(scene).Single();
            ValidateEnemy(charger, player, camera, walls);
            Require(new SerializedObject(charger).FindProperty("dashDirectionIndicator").objectReferenceValue != null,
                "The Charger requires its dash direction indicator.");
        }

        private static void ValidateEnemy(MonoBehaviour enemy, PlayerHealth player, Camera camera, int walls)
        {
            var perception = new SerializedObject(enemy.GetComponent<EnemyPerception2D>());
            var body = enemy.GetComponent<Rigidbody2D>();
            var data = new SerializedObject(enemy);
            var first = (Transform)data.FindProperty("waypointA").objectReferenceValue;
            var second = (Transform)data.FindProperty("waypointB").objectReferenceValue;
            Require(PrefabUtility.IsPartOfPrefabInstance(enemy) && enemy.GetComponent<EnemyHealth>() != null &&
                body.bodyType == RigidbodyType2D.Kinematic && !enemy.GetComponent<BoxCollider2D>().isTrigger &&
                perception.FindProperty("player").objectReferenceValue == player &&
                perception.FindProperty("gameplayCamera").objectReferenceValue == camera &&
                perception.FindProperty("visual").objectReferenceValue != null &&
                perception.FindProperty("walls").intValue == (1 << walls) &&
                first != null && second != null && first != second &&
                !first.IsChildOf(enemy.transform) && !second.IsChildOf(enemy.transform),
                "Invalid prefab, perception, or world waypoints on " + enemy.name);
        }

        private static RectTransform MainPanel(GameMenuController controller) =>
            (RectTransform)controller.transform.Find("Safe Area/Main");

        private static int EnsureWallsLayer()
        {
            int existing = LayerMask.NameToLayer("Walls");
            if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").Single());
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = "Walls";
                tags.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(tags.targetObject);
                return i;
            }
            throw new InvalidOperationException("No free user layer is available for Walls.");
        }

        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var entry = scenes.Find(item => item.path == ScenePath);
            if (entry != null) entry.enabled = true;
            else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ResizeBoundary(Scene scene, string name, float x, int walls)
        {
            var boundary = scene.GetRootGameObjects().Single(root => root.name == name);
            boundary.layer = walls;
            boundary.transform.position = new Vector3(x, 3.5f, 0);
            var box = boundary.GetComponent<BoxCollider2D>();
            box.offset = Vector2.zero;
            box.size = new Vector2(1, 9);
        }

        private static void Solid(Transform parent, string name, Vector2 position, Vector2 size, Color color, int walls)
        {
            var renderer = Sprite(parent, name, "ShadowKing/Stone", position, 0);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
            renderer.color = color;
            renderer.gameObject.layer = walls;
            renderer.gameObject.AddComponent<BoxCollider2D>().size = size;
        }

        private static SpriteRenderer Sprite(Transform parent, string name, string art, Vector2 position, int order)
        {
            var sprite = Child(parent, name, position).gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = Load<Sprite>(Sprites + art + ".png");
            sprite.sharedMaterial = PixelMaterial;
            sprite.sortingOrder = order;
            return sprite;
        }

        private static Transform Child(Transform parent, string name, Vector2 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.position = position;
            return child;
        }

        private static GameObject Instance(GameObject prefab, Transform parent, Vector2 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }

        private static string PrefabPath(string name) => Prefabs + "/" + name + ".prefab";

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = PrefabPath(name);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Require(success && prefab != null, "Could not save " + path);
            return prefab;
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "Required asset missing: " + path);
            return asset;
        }

        private static void Set(Object target, string field, object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(field);
            if (value is int number) property.intValue = number;
            else if (value is string text) property.stringValue = text;
            else if (value is Vector2 vector) property.vector2Value = vector;
            else if (value is Color color) property.colorValue = color;
            else throw new ArgumentException(field);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
