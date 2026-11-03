using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;
using UI = Zhuzbai.Platformer.Editor.GameMenuSetup;

namespace Zhuzbai.Platformer.Editor
{
    public static class ShadowKingSetup
    {
        public const string ScenePath = "Assets/Scenes/Level4.unity";
        private const string Source = "Assets/Scenes/Level3.unity";
        private const string Art = "Assets/Sprites/ShadowKing/";
        private const string AnimationFolder = "Assets/Animations/ShadowKing/";
        private const string BossPrefab = "Assets/Prefabs/Boss/ShadowKing.prefab";
        private const string OrbPrefab = "Assets/Prefabs/Boss/ShadowOrb.prefab";
        private const string Victory = "Assets/Audio/SFX/ShadowKingVictory.wav";
        private static readonly string[] BuildOrder = { U.Menu, U.Level, "Assets/Scenes/Level2.unity", Source, ScenePath };
        public static bool IsInstalled => File.Exists(ScenePath);
        private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Sprites/Foundation/PixelUnlit.mat");
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");

        [MenuItem("Tools/Zhuzbai/Setup Boss Level")]
        public static void Apply()
        {
            U.RequireEditMode();
            U.EditScene(Source, scene => {
                Require(U.All<PlayerFoundation>(scene).Length == 1 && U.All<MultiDeviceControlsPanel>(scene).Length == 1 && U.All<GameMenuController>(scene).Length == 1, "Existing Level3 player, menus and mobile controls are required.");
            }, false);
            PrepareAssets();
            bool created = !File.Exists(ScenePath);
            if (created) U.EditScene(Source, scene => Require(EditorSceneManager.SaveScene(scene, ScenePath, true), "Could not copy Level3 to Level4."), false);
            U.EditScene(ScenePath, scene => {
                var definition = U.All<LevelDefinition>(scene).Single();
                Require(created || (definition.IsBossLevel && definition.GeneratedLayoutScene == "Level4"), "Existing Level4 is not owned by this setup; it will not be overwritten.");
                if (created) BuildArena(scene);
                ConfigureScene(scene);
            });
            // Only progression metadata changes in the original Level3. Its layout is retained.
            U.EditScene(Source, scene => Set(U.All<LevelDefinition>(scene).Single(), "nextScene", "Level4"));
            foreach (string path in BuildOrder.Take(4).Skip(1)) U.EditScene(path, scene => {
                foreach (var panel in U.All<HowToPlayPanel>(scene))
                {
                    var goals = panel.transform.Find("Goals");
                    if (goals != null) { var text = goals.GetComponent<Text>(); text.text = text.text.Replace("all three levels", "all four levels"); EditorUtility.SetDirty(text); }
                }
            });
            RegisterScenes(); AssetDatabase.SaveAssets(); Validate();
            Debug.Log("Level4 — Shadow King's Throne installed. Original levels preserved; Level3 now leads to Level4. Open MainMenu alone and follow LEVEL4_SHADOW_KING_SETUP.md.");
        }
        private static void PrepareAssets()
        {
            U.Folder(AnimationFolder.TrimEnd('/')); U.Folder("Assets/Prefabs/Boss");
            foreach (string path in Directory.GetFiles(Art, "*.png"))
            {
                string normalized = path.Replace('\\', '/');
                AssetDatabase.ImportAsset(normalized, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(normalized);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = normalized.Contains("ShadowKing_") || normalized.EndsWith("Orb.png") ? 20 : 16;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Center; settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
            }
            Require(File.Exists(Victory), "Missing supplied victory WAV. Run Tools/generate_shadow_king_art.py.");
            AssetDatabase.ImportAsset(Victory, ImportAssetOptions.ForceSynchronousImport);
            var audioImporter = (AudioImporter)AssetImporter.GetAtPath(Victory);
            var audio = audioImporter.defaultSampleSettings; audio.loadType = AudioClipLoadType.DecompressOnLoad; audio.compressionFormat = AudioCompressionFormat.PCM; audioImporter.defaultSampleSettings = audio; audioImporter.SaveAndReimport();
            EnsureAnimations(); EnsurePrefabs();
        }
        private static void EnsureAnimations()
        {
            string[] states = { "Idle", "Attack", "Damage", "Death" }; int[] counts = { 2, 2, 1, 4 };
            for (int state = 0; state < states.Length; state++)
            {
                string path = AnimationFolder + states[state] + ".anim";
                if (File.Exists(path)) continue;
                var clip = new AnimationClip { name = "Shadow King " + states[state], frameRate = 6 };
                var keys = new ObjectReferenceKeyframe[counts[state] + 1];
                for (int i = 0; i < keys.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / 6f, value = Sprite("ShadowKing_" + states[state] + "_" + Mathf.Min(i, counts[state] - 1)) };
                Require(keys.All(k => k.value != null), "Missing boss animation frames.");
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = state == 0; AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, path);
            }
            string controllerPath = AnimationFolder + "ShadowKing.controller";
            if (File.Exists(controllerPath)) return;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (string trigger in states.Skip(1)) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var nodes = states.Select((name, i) => { var node = machine.AddState(name, new Vector3(240, i * 100)); node.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + name + ".anim"); node.writeDefaultValues = false; return node; }).ToArray();
            machine.defaultState = nodes[0];
            foreach (int i in new[] { 3, 2, 1 })
            {
                var enter = machine.AddAnyStateTransition(nodes[i]); enter.hasExitTime = false; enter.duration = 0; enter.canTransitionToSelf = false; enter.AddCondition(AnimatorConditionMode.If, 0, states[i]);
                if (i == 3) continue;
                var exit = nodes[i].AddTransition(nodes[0]); exit.hasExitTime = true; exit.exitTime = 1; exit.duration = 0;
            }
            EditorUtility.SetDirty(controller);
        }
        private static void EnsurePrefabs()
        {
            if (File.Exists(OrbPrefab)) Require(AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefab).GetComponent<ShadowKingProjectile>() != null, "Existing ShadowOrb prefab belongs to a different system.");
            if (File.Exists(BossPrefab)) Require(AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab).GetComponent<ShadowKingHealth>() != null, "Existing ShadowKing prefab belongs to a different system.");
            if (!File.Exists(OrbPrefab))
            {
                var root = Visual(null, "Shadow Orb", Vector2.zero, "Orb", 12);
                try
                {
                    var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.freezeRotation = true; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    root.AddComponent<CircleCollider2D>().radius = 0.22f; root.AddComponent<ShadowKingProjectile>();
                    PrefabUtility.SaveAsPrefabAsset(root, OrbPrefab);
                }
                finally { Object.DestroyImmediate(root); }
            }
            if (File.Exists(BossPrefab)) return;
            var boss = new GameObject("SHADOW KING", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(ShadowKingHealth), typeof(ShadowKingBoss));
            try
            {
                boss.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                boss.GetComponent<BoxCollider2D>().size = new Vector2(1.5f, 3);
                var visual = Visual(boss.transform, "Visual", Vector2.zero, "ShadowKing_Idle_0", 10);
                var animator = visual.AddComponent<Animator>(); animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimationFolder + "ShadowKing.controller");
                var muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(boss.transform); muzzle.localPosition = new Vector3(-1.15f, 0, 0);
                var warning = Visual(boss.transform, "Attack Warning", Vector2.zero, "Glow", 13); warning.transform.localPosition = muzzle.localPosition; warning.SetActive(false);
                var controller = boss.GetComponent<ShadowKingBoss>();
                U.Reference(controller, "visual", visual.GetComponent<SpriteRenderer>()); U.Reference(controller, "animator", animator); U.Reference(controller, "muzzle", muzzle);
                U.Reference(controller, "warning", warning.GetComponent<SpriteRenderer>());
                U.Reference(controller, "projectilePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefab).GetComponent<ShadowKingProjectile>());
                U.Reference(controller, "victorySound", AssetDatabase.LoadAssetAtPath<AudioClip>(Victory));
                PrefabUtility.SaveAsPrefabAsset(boss, BossPrefab);
            }
            finally { Object.DestroyImmediate(boss); }
        }
        private static void BuildArena(Scene scene)
        {
            var session = U.All<LevelSession>(scene).Single();
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "Phase5 Environment") Object.DestroyImmediate(root);
            foreach (string name in new[] { "Terrain", "Brick Platforms", "Background", "Coins" })
            { var child = session.transform.Find(name); if (child != null) Object.DestroyImmediate(child.gameObject); }
            // This operates only on the newly copied Level4, never on its source Level3.
            foreach (var coin in U.All<CoinCollectible>(scene)) Object.DestroyImmediate(coin.gameObject);
            foreach (var gate in U.All<ExitGate>(scene)) Object.DestroyImmediate(gate.gameObject);
            foreach (var zone in U.All<FallHazard>(scene)) Object.DestroyImmediate(zone.gameObject);
            Require(U.All<PatrolEnemy>(scene).Length == 0 && U.All<HazardSpawner>(scene).Length == 0 && U.All<MovingPlatform>(scene).Length == 0, "Unexpected copied gameplay objects outside the known environment. Review Level4 before proceeding.");
            var room = new GameObject("Level4 Throne Room").transform;
            Solid(room, "Arena Floor", new Vector2(12.5f, -1.5f), new Vector2(28, 3));
            Solid(room, "West Wall", new Vector2(-1.5f, 4), new Vector2(1, 8));
            Solid(room, "East Wall", new Vector2(26.5f, 4), new Vector2(1, 8));
            foreach (float x in new[] { 6, 11, 16 }) Solid(room, "Dodge Platform", new Vector2(x, 2), new Vector2(2.5f, 0.5f));
            // Decorations never carry gameplay colliders.
            var back = Visual(room, "Dark Stone Backdrop", new Vector2(12.5f, 4), "Stone", -30);
            var backdrop = back.GetComponent<SpriteRenderer>(); backdrop.drawMode = SpriteDrawMode.Tiled; backdrop.size = new Vector2(28, 10); backdrop.color = new Color(0.55f, 0.45f, 0.65f);
            var throne = Visual(room, "Throne", new Vector2(22, 2.2f), "Throne", -12); throne.transform.localScale = Vector3.one * 1.5f;
            for (int i = 0; i < 5; i++)
            {
                float x = 1 + i * 6;
                var pillar = Visual(room, "Stone Pillar", new Vector2(x, 2.5f), "Pillar", -15); pillar.transform.localScale = new Vector3(1.5f, 2, 1);
                Visual(room, "Green Torch", new Vector2(x, 3), "Torch", -10);
            }
            var player = U.All<PlayerFoundation>(scene).Single(); player.transform.position = new Vector3(3, 1.2f, 0);
            var definition = session.GetComponent<LevelDefinition>(); definition.SpawnPoint.position = player.transform.position;
            var camera = U.All<CameraFollow2D>(scene).Single(); camera.GetComponent<Camera>().backgroundColor = new Color32(19, 14, 30, 255);
            camera.GetComponent<Camera>().orthographicSize = 7.6f; // Whole arena fits a 16:9 view; existing follow/bounds remain.
            Set(camera, "minimum", new Vector2(7, 3)); Set(camera, "maximum", new Vector2(19, 3.8f)); Set(camera, "horizontalWorldBounds", new Vector2(-1, 26)); camera.transform.position = new Vector3(8, 3, -10);
            var fall = new GameObject("Arena Safety Fall Zone", typeof(BoxCollider2D), typeof(FallHazard)); fall.transform.SetParent(room); fall.transform.position = new Vector3(12.5f, -6, 0); fall.GetComponent<BoxCollider2D>().size = new Vector2(35, 3); fall.GetComponent<BoxCollider2D>().isTrigger = true;
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefab), room); boss.transform.position = new Vector3(22, 1.5f, 0);
        }
        private static GameObject Visual(Transform parent, string name, Vector2 position, string sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent); go.transform.position = position;
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = Sprite(sprite); Require(renderer.sprite != null, "Missing art " + sprite); renderer.sharedMaterial = Material; renderer.sortingOrder = order; return go;
        }
        private static void Solid(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = Visual(parent, name, position, "Stone", 0); var renderer = go.GetComponent<SpriteRenderer>(); renderer.drawMode = SpriteDrawMode.Tiled; renderer.size = size; go.AddComponent<BoxCollider2D>().size = size;
        }
        private static void ConfigureScene(Scene scene)
        {
            var session = U.All<LevelSession>(scene).Single(); var definition = session.GetComponent<LevelDefinition>();
            Set(definition, "levelNumber", 4); Set(definition, "levelName", "Shadow King's Throne"); Set(definition, "requiredCoins", 0); Set(definition, "nextScene", "");
            Set(definition, "bossLevel", true); Set(definition, "generatedLayoutScene", "Level4"); Set(definition, "objective", "Defeat SHADOW KING: land 15 energy blasts. Dodge warned shots and jump over Phase 2 waves.");
            var player = U.All<PlayerFoundation>(scene).Single(); Set(player.GetComponent<PlayerHealth>(), "maximumHealth", 3);
            var boss = U.All<ShadowKingBoss>(scene).Single(); U.Reference(boss, "player", player); U.Reference(boss, "session", session);
            var menu = U.All<GameMenuController>(scene).Single();
            var safe = (RectTransform)menu.transform.Find("Safe Area");
            var bar = UI.Rect(safe, "Shadow King Health"); UI.Place(bar, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(640, 120));
            var background = Get<Image>(bar.gameObject); background.color = new Color(0.02f, 0.01f, 0.05f, 0.8f); background.raycastTarget = false;
            UI.Label(bar, "Name", "SHADOW KING", new Vector2(-60, -5), new Vector2(400, 32), 27, new Color32(250, 211, 108, 255));
            var hp = UI.Label(bar, "HP", "15 / 15 HP", new Vector2(220, -5), new Vector2(180, 32), 24, Color.white);
            var track = UI.Rect(bar, "Track"); UI.Place(track, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -43), new Vector2(600, 22)); Get<Image>(track.gameObject).color = new Color32(45, 29, 57, 255); track.GetComponent<Image>().raycastTarget = false;
            var fillRect = UI.Rect(track, "Fill"); Stretch(fillRect); var fill = Get<Image>(fillRect.gameObject); fill.sprite = Sprite("Stone"); fill.color = new Color32(156, 73, 204, 255); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1; fill.raycastTarget = false;
            var warning = UI.Label(bar, "Warning", "", new Vector2(0, -77), new Vector2(600, 30), 23, new Color32(126, 255, 176, 255));
            var hud = Get<ShadowKingHud>(bar.gameObject); U.Reference(hud, "health", boss.GetComponent<ShadowKingHealth>()); U.Reference(hud, "boss", boss); U.Reference(hud, "fill", fill); U.Reference(hud, "hitPoints", hp); U.Reference(hud, "warning", warning);
            var foundationHud = U.All<FoundationHud>(scene).Single(); var coinCounter = (PixelNumberDisplay)new SerializedObject(foundationHud).FindProperty("coinCounter").objectReferenceValue;
            coinCounter.transform.parent.gameObject.SetActive(false);
            var levelBadge = foundationHud.GetComponentInChildren<PixelHudSafeArea>(true).transform.Find("Level Indicator");
            if (levelBadge != null) { var text = levelBadge.GetComponent<Text>(); text.text = "LEVEL 4"; text.enabled = true; var badge = levelBadge.Find("Pixel Badge"); if (badge != null) badge.GetComponent<Image>().enabled = false; }
            GameAudioSetup.ConnectScene(scene, AssetDatabase.LoadAssetAtPath<GameObject>(GameAudioSetup.PrefabPath), true);
            UiInputActionSetup.Configure(U.All<EventSystem>(scene).Single().GetComponent<InputSystemUIInputModule>());
            MapBoundarySetup.ApplyScene(scene);
        }
        private static T Get<T>(GameObject go) where T : Component { var item = go.GetComponent<T>(); return item != null ? item : go.AddComponent<T>(); }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Set(Object target, string field, object value)
        {
            var data = new SerializedObject(target); var property = data.FindProperty(field);
            if (value is int i) property.intValue = i; else if (value is bool b) property.boolValue = b; else if (value is string s) property.stringValue = s; else if (value is Vector2 v) property.vector2Value = v;
            else throw new ArgumentException(field);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static EditorBuildSettingsScene[] Ordered(EditorBuildSettingsScene[] current) => BuildOrder.Select(p => new EditorBuildSettingsScene(p, true)).Concat(current.Where(s => !BuildOrder.Contains(s.path))).ToArray();
        private static void RegisterScenes()
        {
            EditorBuildSettings.scenes = Ordered(EditorBuildSettings.scenes);
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile"))
            { var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid)); if (profile != null && profile.overrideGlobalScenes) { profile.scenes = Ordered(profile.scenes); EditorUtility.SetDirty(profile); } }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [MenuItem("Tools/Zhuzbai/Validate Boss Level")]
        public static void Validate()
        {
            U.RequireEditMode();
            U.EditScene(ScenePath, scene => {
                var definition = U.All<LevelDefinition>(scene).Single(); Require(definition.IsBossLevel && definition.LevelNumber == 4 && definition.RequiredCoins == 0 && definition.NextScene == "" && definition.SpawnPoint != null, "Invalid boss metadata/spawn.");
                Require(U.All<CoinCollectible>(scene).Length == 0 && U.All<ExitGate>(scene).Length == 0 && U.All<HazardSpawner>(scene).Length == 0 && U.All<PatrolEnemy>(scene).Length == 0, "Copied objectives/hazards remain in the arena.");
                var player = U.All<PlayerFoundation>(scene).Single(); Require(player.GetComponent<PlayerHealth>().MaximumHealth == 3 && player.GetComponents<PlayerInput>().Length == 1 && player.GetComponent<MultiDeviceInput>() != null, "Player health/input configuration invalid.");
                var playerInput = player.GetComponent<PlayerInput>(); Require(playerInput.actions != null && playerInput.actions.name == "PlayerControls" && playerInput.neverAutoSwitchControlSchemes && playerInput.defaultActionMap == "Player", "Boss scene input changed.");
                Require(playerInput.notificationBehavior == PlayerNotifications.SendMessages && playerInput.enabled && player.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Dynamic, "Player input/physics mode changed.");
                Require(U.All<ShadowKingHealth>(scene).Length == 1 && U.All<ShadowKingBoss>(scene).Length == 1 && U.All<ShadowKingHud>(scene).Length == 1 && ShadowKingHealth.FullHealth == 15, "Missing/duplicate boss or health bar.");
                var boss = U.All<ShadowKingBoss>(scene).Single(); var data = new SerializedObject(boss);
                foreach (string field in new[] { "animator", "visual", "warning", "muzzle", "projectilePrefab", "victorySound", "player", "session" }) Require(data.FindProperty(field).objectReferenceValue != null, "Missing boss reference: " + field);
                Require(boss.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Kinematic && !boss.GetComponent<Collider2D>().isTrigger, "Boss body/collider invalid.");
                var orb = (ShadowKingProjectile)data.FindProperty("projectilePrefab").objectReferenceValue; Require(EditorUtility.IsPersistent(orb) && orb.GetComponent<Rigidbody2D>().gravityScale == 0 && !orb.GetComponent<Collider2D>().isTrigger, "Orb physics/prefab invalid.");
                var controller = ((Animator)data.FindProperty("animator").objectReferenceValue).runtimeAnimatorController as AnimatorController;
                Require(controller != null && new[] { "Idle", "Attack", "Damage", "Death" }.All(name => controller.layers[0].stateMachine.states.Any(s => s.state.name == name && s.state.motion != null)), "Boss animation states missing.");
                Require(U.All<GameMenuController>(scene).Length == 1 && U.All<AudioManager>(scene).Length == 1 && U.All<MultiDeviceControlsPanel>(scene).Length == 1 && U.All<EventSystem>(scene).Length == 1, "Missing/duplicate shared systems.");
                Require(U.All<AudioManager>(scene).Single().GetComponentsInChildren<AudioSource>(true).Length == 4 && U.All<GameplayAudioBridge>(scene).Length == 1 && U.All<FoundationHud>(scene).Length == 1 && U.All<CameraFollow2D>(scene).Length == 1, "Missing/duplicate HUD, camera or audio channels/bridge.");
                var controls = U.All<MultiDeviceControlsPanel>(scene).Single(); var controlData = new SerializedObject(controls);
                var stick = controlData.FindProperty("stick").objectReferenceValue as SafeOnScreenStick; var jump = controlData.FindProperty("jump").objectReferenceValue as SafeOnScreenButton; var fire = controlData.FindProperty("fire").objectReferenceValue as SafeOnScreenButton;
                Require(stick != null && stick.controlPath == "<Gamepad>/leftStick" && jump != null && jump.controlPath == "<Gamepad>/buttonSouth" && fire != null && fire.controlPath == "<Gamepad>/buttonWest", "Mobile control paths/references missing.");
                var playerData = new SerializedObject(player); var playerAnimator = playerData.FindProperty("animator").objectReferenceValue as Animator;
                Require(playerAnimator != null && playerAnimator.GetComponent<DariusManAnimator>() != null && playerAnimator.runtimeAnimatorController != null && playerData.FindProperty("bulletPrefab").objectReferenceValue != null, "DariusMan animation/projectile wiring missing.");
                UiInputActionSetup.Validate(U.All<EventSystem>(scene).Single().GetComponent<InputSystemUIInputModule>(), scene.name);
                var menu = new SerializedObject(U.All<GameMenuController>(scene).Single()); Require(menu.FindProperty("victoryPanel").objectReferenceValue != null, "Existing YOU WIN screen missing.");
                var hud = new SerializedObject(U.All<ShadowKingHud>(scene).Single()); foreach (string field in new[] { "health", "boss", "fill", "hitPoints", "warning" }) Require(hud.FindProperty(field).objectReferenceValue != null, "Missing boss HUD reference: " + field);
            }, false);
            U.EditScene(Source, scene => Require(U.All<LevelDefinition>(scene).Single().NextScene == "Level4", "Level3 does not lead to Level4."), false);
            Require(EditorBuildSettings.scenes.Length >= 5 && BuildOrder.Select((p, i) => EditorBuildSettings.scenes[i].path == p && EditorBuildSettings.scenes[i].enabled).All(v => v), "Incorrect global Build Profile scene order.");
            foreach (string guid in AssetDatabase.FindAssets("t:BuildProfile")) { var p = AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid)); if (p != null && p.overrideGlobalScenes) Require(p.scenes.Length >= 5 && BuildOrder.Select((path, i) => p.scenes[i].path == path && p.scenes[i].enabled).All(v => v), "Incorrect custom Build Profile order."); }
            Debug.Log("Level4 structural validation passed: boss, arena references, player, UI, audio, progression and scene registration. Phase timing, victory/death, and controls require Play Mode tests.");
        }
    }
}
