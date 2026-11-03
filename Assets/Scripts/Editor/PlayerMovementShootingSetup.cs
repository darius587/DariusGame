using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zhuzbai.Platformer.Editor
{
    public static class PlayerMovementShootingSetup
    {
        private const string ScenePath = "Assets/Scenes/MainLevel.unity";
        private const string PlayerPath = "Assets/Prefabs/Foundation/Player.prefab";
        private const string ActionsPath = "Assets/Settings/PlayerControls.inputactions";
        private const string BulletPath = "Assets/Prefabs/Foundation/Bullet.prefab";
        private const string SpritePath = "Assets/Sprites/Foundation/Bullet.png";
        private const string Notice = "MOVE  A/D or arrows    JUMP  Space    FIRE  X / left click   |   Gamepad supported";

        [MenuItem("Tools/Zhuzbai/Setup Player Movement and Shooting")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before applying Phase 2.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab Mode before applying Phase 2.");
            if (!File.Exists(ScenePath) || !File.Exists(PlayerPath))
                throw new InvalidOperationException("MainLevel and the foundation Player prefab must already exist. Phase 2 never regenerates the level.");

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            FindPlayer(scene); // Preflight before changing any assets.
            AssetDatabase.ImportAsset(ActionsPath, ImportAssetOptions.ForceSynchronousImport);
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            ValidateActions(actions);
            var bullet = EnsureBullet();

            // Edit only the existing prefab contents; transforms, physics tuning, health, and animation remain intact.
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                ConfigurePlayer(contents.GetComponent<PlayerFoundation>(), actions, bullet, false);
                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPath, out bool success);
                if (!success) throw new IOException("Could not save the updated Player prefab.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            DariusManSetup.ApplyPrefabsIfInstalled();
            MultiDeviceControlsSetup.ApplyPrefabIfInstalled();

            // Reacquire after prefab propagation. Respect per-instance transforms and unrelated overrides.
            var player = FindPlayer(scene);
            ConfigurePlayer(player, actions, bullet, true);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.name != "Phase Notice" ||
                    (text.text != "FOUNDATION BUILD  |  Player controls arrive in the next phase" && text.text != Notice)) continue;
                Undo.RecordObject(text, "Update Phase 2 controls notice");
                text.text = Notice;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            DariusManSetup.ApplySceneIfInstalled(scene);
            MultiDeviceControlsSetup.ApplySceneIfInstalled(scene);
            ValidatePlayer(player, actions);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save MainLevel.");
            AssetDatabase.SaveAssets();
            SceneManager.SetActiveScene(scene);
            Selection.activeGameObject = player.gameObject;
            Debug.Log("Phase 2 applied and saved to MainLevel and Player.prefab. Level objects and existing tuning were preserved. Select the Game view and press Play.");
        }

        [MenuItem("Tools/Zhuzbai/Validate Player Movement and Shooting")]
        public static void Validate()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open MainLevel before validating Phase 2.");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            ValidateActions(actions);
            ValidatePlayer(FindPlayer(scene), actions);
            Debug.Log("Phase 2 configuration validated: PlayerInput, action bindings, physics, Animator, and bullet references are connected.");
        }

        private static PlayerFoundation FindPlayer(Scene scene)
        {
            var players = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerFoundation>(true)).ToArray();
            if (players.Length != 1)
                throw new InvalidOperationException($"Expected exactly one existing PlayerFoundation in MainLevel; found {players.Length}. No player will be created or replaced.");
            return players[0];
        }

        private static void ConfigurePlayer(PlayerFoundation player, InputActionAsset actions, BulletProjectile bullet, bool undo)
        {
            if (player == null) throw new InvalidOperationException("Existing Player prefab is missing PlayerFoundation.");
            if (player.GetComponents<PlayerInput>().Length > 1)
                throw new InvalidOperationException("Remove duplicate PlayerInput components before continuing.");
            if (undo) Undo.RecordObject(player, "Connect Phase 2 player");
            var serialized = new SerializedObject(player);
            // Existing ground check, socket, and Animator references are intentionally preserved.
            if (serialized.FindProperty("groundCheck").objectReferenceValue == null ||
                serialized.FindProperty("projectileOrigin").objectReferenceValue == null ||
                serialized.FindProperty("animator").objectReferenceValue == null)
                throw new InvalidOperationException("Player's foundation references are missing. Restore GroundCheck, ProjectileOrigin and Animator before applying Phase 2.");
            if (serialized.FindProperty("spriteRenderer").objectReferenceValue == null)
                serialized.FindProperty("spriteRenderer").objectReferenceValue = player.GetComponentInChildren<SpriteRenderer>(true);
            serialized.FindProperty("bulletPrefab").objectReferenceValue = bullet;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(player);

            var input = player.GetComponent<PlayerInput>();
            if (input == null) input = undo ? Undo.AddComponent<PlayerInput>(player.gameObject) : player.gameObject.AddComponent<PlayerInput>();
            if (undo) Undo.RecordObject(input, "Configure Phase 2 PlayerInput");
            input.actions = actions;
            input.notificationBehavior = PlayerNotifications.SendMessages;
            input.defaultActionMap = "Player";
            input.defaultControlScheme = "";
            input.neverAutoSwitchControlSchemes = false;
            input.enabled = true;
            EditorUtility.SetDirty(input);
            if (PrefabUtility.IsPartOfPrefabInstance(player))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            }
        }

        private static void ValidateActions(InputActionAsset actions)
        {
            if (actions == null) throw new InvalidOperationException("PlayerControls.inputactions did not import. Check Input System 1.20.0 and Console errors.");
            var map = actions.FindActionMap("Player", true);
            foreach (string name in new[] { "Move", "Jump", "Fire" }) map.FindAction(name, true);
            if (map.FindAction("Move").type != InputActionType.Value || map.FindAction("Move").expectedControlType != "Vector2" ||
                map.FindAction("Jump").type != InputActionType.Button || map.FindAction("Fire").type != InputActionType.Button)
                throw new InvalidOperationException("Player actions must be Move Value/Vector2, Jump Button, and Fire Button.");
            RequireBindings(map.FindAction("Move"), "<Keyboard>/w", "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d",
                "<Keyboard>/upArrow", "<Keyboard>/downArrow", "<Keyboard>/leftArrow", "<Keyboard>/rightArrow", "<Gamepad>/leftStick");
            RequireBindings(map.FindAction("Jump"), "<Keyboard>/space", "<Gamepad>/buttonSouth");
            RequireBindings(map.FindAction("Fire"), "<Keyboard>/x", "<Mouse>/leftButton", "<Gamepad>/buttonWest");
        }

        private static void RequireBindings(InputAction action, params string[] paths)
        {
            foreach (string path in paths)
                if (!action.bindings.Any(binding => string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Missing {action.name} binding: {path}");
        }

        private static void ValidatePlayer(PlayerFoundation player, InputActionAsset actions)
        {
            var input = player.GetComponent<PlayerInput>();
            if (input == null || input.actions != actions || input.notificationBehavior != PlayerNotifications.SendMessages ||
                input.defaultActionMap != "Player" || !input.enabled)
                throw new InvalidOperationException("PlayerInput is not correctly configured. Run Apply Phase 2.");
            var body = player.GetComponent<Rigidbody2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated ||
                player.GetComponent<Collider2D>() == null || player.GetComponent<Collider2D>().isTrigger)
                throw new InvalidOperationException("Player needs a simulated dynamic Rigidbody2D and solid Collider2D.");
            var serialized = new SerializedObject(player);
            foreach (string property in new[] { "groundCheck", "projectileOrigin", "animator", "spriteRenderer", "bulletPrefab" })
                if (serialized.FindProperty(property).objectReferenceValue == null)
                    throw new InvalidOperationException("Missing player reference: " + property);
            var animator = (Animator)serialized.FindProperty("animator").objectReferenceValue;
            if (animator.runtimeAnimatorController == null || !animator.parameters.Any(p => p.name == "Speed" && p.type == AnimatorControllerParameterType.Float) ||
                !animator.parameters.Any(p => p.name == "Grounded" && p.type == AnimatorControllerParameterType.Bool) ||
                !animator.parameters.Any(p => p.name == "VerticalSpeed" && p.type == AnimatorControllerParameterType.Float))
                throw new InvalidOperationException("Player Animator must retain Speed, Grounded, and VerticalSpeed parameters.");
            var bullet = (BulletProjectile)serialized.FindProperty("bulletPrefab").objectReferenceValue;
            if (bullet.GetComponent<Rigidbody2D>() == null || bullet.GetComponent<Rigidbody2D>().gravityScale != 0 ||
                bullet.GetComponent<CircleCollider2D>() == null || bullet.GetComponent<CircleCollider2D>().isTrigger)
                throw new InvalidOperationException("Bullet needs a zero-gravity Rigidbody2D and solid circle collider.");
        }

        private static BulletProjectile EnsureBullet()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPath);
            if (existing != null)
            {
                var bullet = existing.GetComponent<BulletProjectile>();
                if (bullet == null) throw new InvalidOperationException("Existing Bullet.prefab has no BulletProjectile; it will not be overwritten.");
                return bullet;
            }
            if (File.Exists(BulletPath)) throw new IOException("Bullet prefab path is already occupied.");
            Sprite sprite = EnsureBulletSprite();
            var temporary = new GameObject("Bullet", typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(BulletProjectile));
            try
            {
                var renderer = temporary.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 12;
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Sprites/Foundation/PixelUnlit.mat");
                var body = temporary.GetComponent<Rigidbody2D>();
                body.gravityScale = 0;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                temporary.GetComponent<CircleCollider2D>().radius = 0.1f;
                var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, BulletPath, out bool success);
                if (!success) throw new IOException("Could not save Bullet.prefab.");
                return prefab.GetComponent<BulletProjectile>();
            }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static Sprite EnsureBulletSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return existing;
            if (File.Exists(SpritePath)) throw new IOException("Bullet sprite path is already occupied.");
            var texture = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[32];
                for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++)
                    pixels[y * 8 + x] = (x == 0 || x == 7) && (y == 0 || y == 3) ? new Color32(0, 0, 0, 0) :
                        y == 1 || y == 2 ? new Color32(255, 244, 174, 255) : new Color32(247, 149, 38, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }
    }
}
