using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class DariusManSetup
    {
        private const string Art = "Assets/Sprites/DariusMan/";
        private const string Animations = "Assets/Animations/DariusMan/";
        private const string ControllerPath = Animations + "DariusMan.controller";
        private const string PlayerPath = "Assets/Prefabs/Foundation/Player.prefab";
        private const string BulletPath = "Assets/Prefabs/Foundation/Bullet.prefab";
        private static readonly string[] States = { "Idle", "Run", "Jump", "Fall", "Shoot" };
        private static readonly int[] Counts = { 2, 4, 2, 2, 2 };
        private static string[] Scenes => new[] { U.Level, "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity" }
            .Concat(File.Exists("Assets/Scenes/Level4.unity") ? new[] { "Assets/Scenes/Level4.unity" } : Array.Empty<string>()).ToArray();
        public static bool IsInstalled => File.Exists(ControllerPath);

        [MenuItem("Tools/Zhuzbai/Setup Player Appearance")]
        public static void Apply()
        {
            U.RequireEditMode();
            foreach (string scene in Scenes)
            {
                if (!File.Exists(scene)) throw new FileNotFoundException("Expected existing level; this command never creates levels.", scene);
                U.EditScene(scene, s => RequireSinglePlayer(s), false);
            }
            PrepareAssets();
            ApplyPrefabs();
            foreach (string scene in Scenes) U.EditScene(scene, ApplyScene);
            AssetDatabase.SaveAssets(); Validate();
            Debug.Log("DariusMan applied to the existing player and energy projectile in all three levels. Physics, input, tuning and layouts are preserved.");
        }

        private static void PrepareAssets()
        {
            U.Folder(Animations.TrimEnd('/'));
            for (int state = 0; state < States.Length; state++)
                for (int frame = 0; frame < Counts[state]; frame++) Import(FramePath(States[state], frame), 20);
            Import(Art + "DariusMan_Energy.png", 16);
            for (int state = 0; state < States.Length; state++) EnsureClip(States[state], Counts[state], state == 4 ? 16 : state == 1 ? 10 : 6, state != 4);
            EnsureController();
        }
        private static string FramePath(string state, int frame) => Art + "DariusMan_" + state + "_" + frame + ".png";
        private static Sprite Frame(string state, int frame) => AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(state, frame));
        private static void Import(string path, float pixelsPerUnit)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing transparent sprite. Restore the supplied DariusMan assets or run Tools/generate_dariusman_art.py.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.SaveAndReimport();
        }
        private static void EnsureClip(string name, int count, float frameRate, bool loop)
        {
            string path = Animations + name + ".anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) return;
            if (File.Exists(path)) throw new IOException("Animation path occupied: " + path);
            var clip = new AnimationClip { name = "DariusMan " + name, frameRate = frameRate };
            var frames = new ObjectReferenceKeyframe[count + 1];
            for (int i = 0; i <= count; i++) frames[i] = new ObjectReferenceKeyframe { time = i / frameRate, value = Frame(name, i == count ? (loop ? 0 : count - 1) : i) };
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), frames);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
        }
        private static void EnsureController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
            if (File.Exists(ControllerPath)) throw new IOException("Animator path occupied: " + ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var states = States.Select((name, i) => {
                var state = machine.AddState(name, new Vector3(250 + i % 3 * 240, 80 + i / 3 * 160));
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Animations + name + ".anim");
                state.writeDefaultValues = false; return state;
            }).ToArray();
            machine.defaultState = states[0];
            // Explicit paths avoid an AnyState locomotion transition interrupting Shoot.
            for (int from = 0; from < states.Length; from++)
            for (int to = 0; to < 4; to++)
            {
                if (from == to) continue;
                var transition = states[from].AddTransition(states[to]);
                transition.duration = 0; transition.hasExitTime = from == 4; transition.exitTime = 1;
                transition.AddCondition(to < 2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "Grounded");
                if (to < 2) transition.AddCondition(to == 0 ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, 0.1f, "Speed");
                else transition.AddCondition(to == 2 ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, 0.01f, "VerticalSpeed");
            }
            var shoot = machine.AddAnyStateTransition(states[4]);
            shoot.duration = 0; shoot.hasExitTime = false; shoot.canTransitionToSelf = false;
            shoot.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            EditorUtility.SetDirty(controller);
        }

        internal static void ApplyPrefabsIfInstalled()
        {
            if (IsInstalled) ApplyPrefabs();
        }
        private static void ApplyPrefabs()
        {
            EditPrefab(PlayerPath, root => ConfigurePlayer(root.GetComponent<PlayerFoundation>()));
            EditPrefab(BulletPath, root => {
                if (root.GetComponent<BulletProjectile>() == null) throw new InvalidOperationException("Expected existing BulletProjectile.");
                ConfigureBullet(root.GetComponent<SpriteRenderer>());
            });
        }
        private static void EditPrefab(string path, Action<GameObject> edit)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Required existing prefab", path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); if (!saved) throw new IOException("Could not save " + path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static PlayerFoundation RequireSinglePlayer(Scene scene)
        {
            var players = U.All<PlayerFoundation>(scene);
            if (players.Length != 1) throw new InvalidOperationException("Expected one existing player in " + scene.name + "; none will be created or removed.");
            return players[0];
        }
        internal static void ApplySceneIfInstalled(Scene scene)
        {
            if (IsInstalled) ApplyScene(scene);
        }
        private static void ApplyScene(Scene scene)
        {
            ConfigurePlayer(RequireSinglePlayer(scene));
            foreach (var bullet in U.All<BulletProjectile>(scene)) ConfigureBullet(bullet.GetComponent<SpriteRenderer>());
        }
        private static void ConfigurePlayer(PlayerFoundation player)
        {
            if (player == null) throw new InvalidOperationException("Existing player controller is missing.");
            var data = new SerializedObject(player);
            var animator = data.FindProperty("animator").objectReferenceValue as Animator;
            if (animator == null || animator.transform == player.transform) throw new InvalidOperationException("Expected existing Animator on the separate Visual child.");
            var renderer = animator.GetComponent<SpriteRenderer>();
            if (renderer == null) throw new InvalidOperationException("Existing visual SpriteRenderer is missing.");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var idle = Frame("Idle", 0);
            if (controller == null || idle == null) throw new InvalidOperationException("Run Apply DariusMan Visuals to prepare the assets first.");
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            renderer.sprite = idle; renderer.color = Color.white;
            if (animator.GetComponent<DariusManAnimator>() == null) animator.gameObject.AddComponent<DariusManAnimator>();
            U.Reference(player, "spriteRenderer", renderer);
            Dirty(animator); Dirty(renderer);
        }
        private static void ConfigureBullet(SpriteRenderer renderer)
        {
            if (renderer == null) throw new InvalidOperationException("Existing bullet SpriteRenderer is missing.");
            var energy = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "DariusMan_Energy.png");
            if (energy == null) throw new InvalidOperationException("Green energy sprite is missing. Run Apply DariusMan Visuals.");
            renderer.sprite = energy; renderer.color = Color.white; Dirty(renderer);
        }
        private static void Dirty(Object value) { EditorUtility.SetDirty(value); if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value); }

        [MenuItem("Tools/Zhuzbai/Validate Player Appearance")]
        public static void Validate()
        {
            U.RequireEditMode();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null || !States.All(name => controller.layers[0].stateMachine.states.Any(s => s.state.name == name && s.state.motion != null)))
                throw new InvalidOperationException("Missing DariusMan Animator states/clips.");
            for (int state = 0; state < States.Length; state++) for (int frame = 0; frame < Counts[state]; frame++)
            {
                var sprite = Frame(States[state], frame);
                if (sprite == null || sprite.pixelsPerUnit != 20 || sprite.texture.filterMode != FilterMode.Point)
                    throw new InvalidOperationException("Missing or incorrectly imported DariusMan frame.");
            }
            foreach (string scene in Scenes) U.EditScene(scene, s => {
                var player = RequireSinglePlayer(s); var data = new SerializedObject(player);
                var animator = data.FindProperty("animator").objectReferenceValue as Animator;
                if (animator == null || animator.runtimeAnimatorController != controller || animator.GetComponents<DariusManAnimator>().Length != 1 ||
                    data.FindProperty("spriteRenderer").objectReferenceValue != animator.GetComponent<SpriteRenderer>() || player.GetComponent<Rigidbody2D>() == null || player.GetComponent<Collider2D>() == null)
                    throw new InvalidOperationException("DariusMan visual/player wiring invalid in " + s.name);
                var bullet = data.FindProperty("bulletPrefab").objectReferenceValue as BulletProjectile;
                if (bullet == null || bullet.GetComponent<SpriteRenderer>().sprite != AssetDatabase.LoadAssetAtPath<Sprite>(Art + "DariusMan_Energy.png"))
                    throw new InvalidOperationException("Green energy prefab reference missing in " + s.name);
            }, false);
            Debug.Log("DariusMan visuals validated in MainLevel, Level2 and Level3. Play Mode animation/foot alignment checks still required.");
        }
    }
}
