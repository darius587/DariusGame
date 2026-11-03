using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zhuzbai.Platformer;

namespace Zhuzbai.Platformer.Editor
{
    public static class PlatformerFoundationSetup
    {
        private const string ScenePath = "Assets/Scenes/MainLevel.unity";
        private const string Art = "Assets/Sprites/Foundation/";
        private const string Prefabs = "Assets/Prefabs/Foundation/";
        private const string Animations = "Assets/Animations/Foundation/";

        [MenuItem("Tools/Zhuzbai/Setup Platformer Environment")]
        public static void GenerateAndOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Generate();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Also callable with Unity -batchmode -executeMethod
        // Zhuzbai.Platformer.Editor.PlatformerFoundationSetup.Generate
        public static void Generate()
        {
            foreach (string folder in new[] { "Assets/Scripts/Runtime", "Assets/Scripts/Editor",
                "Assets/Sprites/Foundation", "Assets/Prefabs/Foundation", "Assets/Animations/Foundation",
                "Assets/Scenes", "Assets/UI" }) EnsureFolder(folder);

            // Idempotent: never replace an existing scene, sprite, animation, or prefab.
            if (File.Exists(ScenePath))
            {
                Debug.Log("MainLevel already exists; preserved without changes. Open Assets/Scenes/MainLevel.unity.");
                return;
            }

            Sprite grass = MakeSprite("Grass", 16, 16, GrassPixel);
            Sprite dirt = MakeSprite("Dirt", 16, 16, (x, y) => DirtPixel(x, y));
            Sprite brick = MakeSprite("Brick", 16, 16, BrickPixel);
            Sprite player = MakeSprite("PlayerIdle", 16, 24, (x, y) => PlayerPixel(x, y, 0));
            Sprite walk = MakeSprite("PlayerWalk", 16, 24, (x, y) => PlayerPixel(x, y, 1));
            Sprite jump = MakeSprite("PlayerJump", 16, 24, (x, y) => PlayerPixel(x, y, 2));
            Sprite coin = MakeSprite("Coin", 8, 8, CoinPixel);
            Sprite cloud = MakeSprite("Cloud", 32, 12, CloudPixel);
            Sprite mountain = MakeSprite("Mountain", 32, 24, MountainPixel);
            Sprite gate = MakeSprite("ExitGate", 16, 32, GatePixel);
            RuntimeAnimatorController controller = MakeAnimator(player, walk, jump);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = new GameObject("MainLevel Foundation");
                var session = root.AddComponent<LevelSession>();
                Transform terrain = Child(root.transform, "Terrain");
                Transform platforms = Child(root.transform, "Brick Platforms");
                Transform backdrop = Child(root.transform, "Background");
                Transform coins = Child(root.transform, "Coins");

                GameObject grassPrefab = TilePrefab("GrassTile", grass);
                GameObject dirtPrefab = TilePrefab("DirtTile", dirt);
                GameObject brickPrefab = TilePrefab("BrickTile", brick);
                for (int x = -1; x <= 40; x++)
                {
                    if (x >= 22 && x <= 25) continue; // A clearly visible falling gap.
                    Spawn(grassPrefab, terrain, new Vector2(x, -0.5f));
                    for (int row = 1; row <= 2; row++)
                        Spawn(dirtPrefab, terrain, new Vector2(x, -0.5f - row));
                }
                AddPlatform(brickPrefab, platforms, 6, 2, 4);
                AddPlatform(brickPrefab, platforms, 12, 3.5f, 4);
                AddPlatform(brickPrefab, platforms, 18, 2, 3);
                AddPlatform(brickPrefab, platforms, 23, 1.5f, 3);
                AddPlatform(brickPrefab, platforms, 28, 2.5f, 4);
                AddPlatform(brickPrefab, platforms, 34, 4, 3);

                var playerObject = Spawn(PlayerPrefab(player, controller), root.transform, new Vector2(2, 1.2f));
                playerObject.name = "Player";
                GameObject coinPrefab = CoinPrefab(coin);
                foreach (Vector2 position in new[] { new Vector2(4, 1), new Vector2(7, 3),
                    new Vector2(13, 4.5f), new Vector2(19, 3), new Vector2(24, 2.5f),
                    new Vector2(29, 3.5f), new Vector2(35, 5), new Vector2(38, 1) })
                    Spawn(coinPrefab, coins, position);

                for (int i = 0; i < 7; i++)
                {
                    var hill = Visual("Distant Mountain", mountain, backdrop, new Vector2(i * 7 - 2, 1.5f), -20);
                    hill.transform.localScale = new Vector3(5, 4, 1);
                    var puff = Visual("Cloud", cloud, backdrop, new Vector2(i * 6 + 1, 5.5f + (i % 2)), -10);
                    puff.transform.localScale = Vector3.one * 1.5f;
                }

                Spawn(GatePrefab(gate), root.transform, new Vector2(39, 1));
                var fallZone = new GameObject("Fall Zone - Respawn");
                fallZone.transform.SetParent(root.transform);
                fallZone.transform.position = new Vector3(19.5f, -6, 0);
                var fallCollider = fallZone.AddComponent<BoxCollider2D>();
                fallCollider.size = new Vector2(48, 2);
                fallCollider.isTrigger = true;
                fallZone.AddComponent<FallHazard>();

                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow2D));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = new Vector3(8, 2.5f, -10);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 4.5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(103, 184, 240, 255);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100;
                SetReference(cameraObject.GetComponent<CameraFollow2D>(), "target", playerObject.transform);
                MakeHud(root.transform, session, playerObject.GetComponent<PlayerHealth>());
                DariusManSetup.ApplyPrefabsIfInstalled();
                DariusManSetup.ApplySceneIfInstalled(scene);
                MapBoundarySetup.ApplyScene(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Unity could not save " + ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("Platformer foundation generated: " + ScenePath + ". Controls are intentionally deferred.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Visual(string name, Sprite sprite, Transform parent, Vector2 position, int order = 0)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            // Unlit sprites are visible in both URP 2D and the built-in renderer; no lighting settings change.
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            string path = Art + "PixelUnlit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (File.Exists(path)) throw new IOException("Existing material path is occupied: " + path);
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            renderer.sharedMaterial = material;
            return go;
        }

        private static GameObject SavePrefab(string name, Func<GameObject> create)
        {
            string path = Prefabs + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Existing prefab path is occupied: " + path);
            GameObject temporary = create();
            try { return PrefabUtility.SaveAsPrefabAsset(temporary, path); }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        private static GameObject TilePrefab(string name, Sprite sprite) => SavePrefab(name, () =>
        {
            var go = Visual(name, sprite, null, Vector2.zero);
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            go.isStatic = true;
            return go;
        });

        private static GameObject PlayerPrefab(Sprite sprite, RuntimeAnimatorController controller) => SavePrefab("Player", () =>
        {
            var go = new GameObject("Player");
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.size = new Vector2(0.65f, 1.4f);
            string physicsPath = Prefabs + "PlayerFriction.physicsMaterial2D";
            var physics = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(physicsPath);
            if (physics == null)
            {
                if (File.Exists(physicsPath)) throw new IOException("Existing physics material path is occupied.");
                physics = new PhysicsMaterial2D("PlayerFriction") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(physics, physicsPath);
            }
            collider.sharedMaterial = physics;
            go.AddComponent<PlayerHealth>();
            var foundation = go.AddComponent<PlayerFoundation>();
            var visual = Visual("Visual", sprite, go.transform, Vector2.zero, 10);
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var ground = Child(go.transform, "GroundCheck");
            ground.localPosition = new Vector3(0, -0.7f, 0);
            var muzzle = Child(go.transform, "ProjectileOrigin");
            muzzle.localPosition = new Vector3(0.55f, 0.05f, 0);
            SetReference(foundation, "groundCheck", ground);
            SetReference(foundation, "projectileOrigin", muzzle);
            SetReference(foundation, "animator", animator);
            return go;
        });

        private static GameObject CoinPrefab(Sprite sprite) => SavePrefab("Coin", () =>
        {
            var go = Visual("Coin", sprite, null, Vector2.zero, 5);
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.23f;
            collider.isTrigger = true;
            go.AddComponent<CoinCollectible>();
            return go;
        });

        private static GameObject GatePrefab(Sprite sprite) => SavePrefab("ExitGate", () =>
        {
            var go = Visual("ExitGate", sprite, null, Vector2.zero, 3);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.7f, 1.8f);
            collider.isTrigger = true;
            go.AddComponent<ExitGate>();
            return go;
        });

        private static GameObject Spawn(GameObject prefab, Transform parent, Vector2 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = position;
            return go;
        }

        private static void AddPlatform(GameObject prefab, Transform parent, int x, float y, int width)
        {
            for (int i = 0; i < width; i++) Spawn(prefab, parent, new Vector2(x + i, y));
        }

        private static void SetReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MakeHud(Transform parent, LevelSession session, PlayerHealth health)
        {
            var canvas = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(parent, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            Text status = HudText(canvas.transform, "Status", new Vector2(24, -20), 24,
                "COINS  00 / 08     HEALTH  3 / 3");
            HudText(canvas.transform, "Phase Notice", new Vector2(24, -55), 18,
                "FOUNDATION BUILD  |  Player controls arrive in the next phase");
            var hud = canvas.AddComponent<FoundationHud>();
            SetReference(hud, "status", status);
            SetReference(hud, "session", session);
            SetReference(hud, "health", health);
        }

        private static Text HudText(Transform parent, string name, Vector2 position, int size, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(1200, 40);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = content;
            go.GetComponent<Shadow>().effectColor = new Color(0.08f, 0.15f, 0.25f, 0.9f);
            return text;
        }

        private static RuntimeAnimatorController MakeAnimator(Sprite idleSprite, Sprite walkSprite, Sprite jumpSprite)
        {
            string path = Animations + "Player.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Existing controller path is occupied.");
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle");
            var walk = machine.AddState("Walk");
            var jump = machine.AddState("Jump");
            idle.motion = MakeClip("Idle", idleSprite, idleSprite);
            walk.motion = MakeClip("Walk", idleSprite, walkSprite);
            jump.motion = MakeClip("Jump", jumpSprite, jumpSprite);
            machine.defaultState = idle;
            Transition(idle, walk, AnimatorConditionMode.Greater, 0.1f, "Speed");
            Transition(walk, idle, AnimatorConditionMode.Less, 0.1f, "Speed");
            Transition(idle, jump, AnimatorConditionMode.IfNot, 0, "Grounded");
            Transition(walk, jump, AnimatorConditionMode.IfNot, 0, "Grounded");
            Transition(jump, idle, AnimatorConditionMode.If, 0, "Grounded");
            return controller;
        }

        private static void Transition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.AddCondition(mode, threshold, parameter);
        }

        private static AnimationClip MakeClip(string name, Sprite first, Sprite second)
        {
            string path = Animations + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Existing clip path is occupied.");
            var clip = new AnimationClip { name = name, frameRate = 8 };
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] {
                new ObjectReferenceKeyframe { time = 0, value = first },
                new ObjectReferenceKeyframe { time = 0.125f, value = second },
                new ObjectReferenceKeyframe { time = 0.25f, value = first } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static Sprite MakeSprite(string name, int width, int height, Func<int, int, Color32> pixel)
        {
            string path = Art + name + ".png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new IOException("Existing sprite path is occupied: " + path);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
                texture.SetPixels32(pixels);
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

        private static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);
        private static Color32 DirtPixel(int x, int y) => (x * 7 + y * 11) % 19 < 3 ? C(107, 65, 42) :
            (x * 3 + y * 5) % 13 < 2 ? C(177, 118, 64) : C(143, 89, 49);
        private static Color32 GrassPixel(int x, int y) => y >= 14 ? C(108, 206, 66) :
            y >= 12 ? C(55, 148, 54) : y == 11 && x % 4 < 2 ? C(41, 112, 45) : DirtPixel(x, y);
        private static Color32 BrickPixel(int x, int y)
        {
            int shift = y < 8 ? 0 : 8;
            if (y % 8 == 0 || (x + shift) % 16 == 0) return C(88, 48, 49);
            if (y % 8 == 7) return C(237, 148, 94);
            return (x + shift) % 16 == 15 ? C(143, 65, 53) : C(197, 95, 65);
        }
        private static Color32 PlayerPixel(int x, int y, int pose)
        {
            if (y >= 19 && y <= 22 && x >= 3 && x <= 12) return C(209, 56, 62);
            if (y == 18 && x >= 2 && x <= 14) return C(150, 39, 51);
            if (y >= 13 && y <= 17 && x >= 4 && x <= 11)
                return x == 10 && y >= 15 ? C(27, 36, 52) : C(255, 201, 144);
            if (y >= 6 && y <= 12 && x >= 4 && x <= 11)
                return x == 6 || x == 9 ? C(245, 188, 65) : C(40, 84, 157);
            if (y >= 7 && y <= 11 && (x == 3 || x == 12)) return C(255, 201, 144);
            int left = pose == 1 ? 3 : 4;
            if (y >= 2 && y <= 5 && ((x >= left && x <= left + 2) || (x >= 9 && x <= 11))) return C(32, 54, 100);
            if (y == 1 && ((x >= left - 1 && x <= left + 2) || (x >= 9 && x <= 12))) return C(64, 42, 46);
            if (pose == 2 && y >= 12 && y <= 15 && (x == 2 || x == 13)) return C(255, 201, 144);
            return new Color32(0, 0, 0, 0);
        }
        private static Color32 CoinPixel(int x, int y)
        {
            int dx = 2 * x - 7, dy = 2 * y - 7;
            if (dx * dx + dy * dy > 48) return new Color32(0, 0, 0, 0);
            if (dx * dx + dy * dy > 28) return C(194, 119, 25);
            return x == 3 || y == 5 ? C(255, 244, 163) : C(255, 201, 48);
        }
        private static Color32 CloudPixel(int x, int y)
        {
            bool inside = (x - 9) * (x - 9) + (y - 5) * (y - 5) < 35 ||
                (x - 17) * (x - 17) + (y - 6) * (y - 6) < 36 ||
                (x - 24) * (x - 24) + (y - 4) * (y - 4) < 22;
            return !inside ? new Color32(0, 0, 0, 0) : y < 4 ? C(204, 231, 244) : C(245, 251, 255);
        }
        private static Color32 MountainPixel(int x, int y)
        {
            if (y > 23 - Mathf.Abs(x - 16) * 1.2f) return new Color32(0, 0, 0, 0);
            return x < 16 ? C(108, 171, 188) : C(88, 151, 175);
        }
        private static Color32 GatePixel(int x, int y)
        {
            if (x <= 2 || x >= 13 || y >= 28) return C(67, 70, 97);
            if (x == 3 || x == 12 || y == 27) return C(154, 161, 188);
            if (y >= 2 && y <= 26) return (x + y) % 6 < 2 ? C(91, 217, 204) : C(48, 133, 160);
            return C(67, 70, 97);
        }
    }
}
