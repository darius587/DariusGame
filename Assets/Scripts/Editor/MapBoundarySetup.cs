using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    /// <summary>Only adds exterior vertical walls; never fills internal gaps.</summary>
    public static class MapBoundarySetup
    {
        private static readonly string[] Paths = { "Assets/Scenes/MainLevel.unity", "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity", "Assets/Scenes/Level4.unity" };
        private sealed class Layout
        {
            public float left, right, bottom, top, thickness;
            public int layer;
        }

        [MenuItem("Tools/Zhuzbai/Setup Map Boundaries")]
        public static void ApplyAll()
        {
            U.RequireEditMode();
            // Preflight every scene before saving any changes. Loaded, unsaved boundaries are included.
            foreach (string path in Paths) U.EditScene(path, scene => Measure(scene), false);
            foreach (string path in Paths) U.EditScene(path, ApplyScene);
            ValidateAll();
        }

        [MenuItem("Tools/Zhuzbai/Validate Map Boundaries")]
        public static void ValidateAll()
        {
            U.RequireEditMode();
            foreach (string path in Paths) U.EditScene(path, ValidateScene, false);
            Debug.Log("Map boundaries validated in all four gameplay scenes. Play Mode testing is still required.");
        }

        private static string Key(string name) => new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        private static bool IsBoundary(Transform t)
        {
            for (; t != null; t = t.parent)
                if (Key(t.name) == "leftboundary" || Key(t.name) == "rightboundary") return true;
            return false;
        }
        private static Transform Existing(Scene scene, string name)
        {
            var matches = U.All<Transform>(scene).Where(t => Key(t.name) == Key(name)).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException(scene.name + ": duplicate " + name + " objects; resolve these before applying.");
            var found = matches.SingleOrDefault();
            if (found == null) return null;
            if (found.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 || found.GetComponentsInChildren<Collider2D>(true).Any(c => c.transform != found || !(c is BoxCollider2D)) || found.GetComponents<BoxCollider2D>().Length > 1)
                throw new InvalidOperationException(scene.name + ": " + name + " contains gameplay or extra colliders; inspect it before repair.");
            if (found.parent != null && (found.parent.GetComponentInParent<Rigidbody2D>() != null || found.parent.lossyScale != Vector3.one || found.parent.rotation != Quaternion.identity || !found.parent.gameObject.activeInHierarchy))
                throw new InvalidOperationException(scene.name + ": boundary parent must be active, unscaled, unrotated and static.");
            return found;
        }
        private static Layout Measure(Scene scene)
        {
            Existing(scene, "LeftBoundary"); Existing(scene, "RightBoundary");
            Physics2D.SyncTransforms();
            var player = U.All<PlayerFoundation>(scene).Single();
            var body = player.GetComponent<Rigidbody2D>();
            var playerBox = player.GetComponent<Collider2D>().bounds;
            var colliders = U.All<Collider2D>(scene).Where(c => c.enabled && c.gameObject.activeInHierarchy && !IsBoundary(c.transform)).ToArray();
            var ground = colliders.Where(c => !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static) &&
                (scene.name == "MainLevel" ? HasAncestor(c.transform, "Terrain") : scene.name == "Level4" ? c.name == "Arena Floor" : c.name == "Ground")).ToArray();
            if (ground.Length == 0) throw new InvalidOperationException(scene.name + ": no recognized playable ground found; no boundaries changed.");
            float highest = colliders.Where(c => !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static)).Max(c => c.bounds.max.y);
            float verticalSpeed = 0;
            var layout = new Layout { left = ground.Min(c => c.bounds.min.x), right = ground.Max(c => c.bounds.max.x), bottom = ground.Min(c => c.bounds.min.y), layer = ground[0].gameObject.layer, thickness = Mathf.Max(.5f, playerBox.size.x) };
            foreach (var support in colliders.Where(c => !c.isTrigger && (c.attachedRigidbody == null || c.attachedRigidbody.bodyType == RigidbodyType2D.Static)))
            {
                // Level4's architectural walls already straddle the arena edges; retain them.
                if (scene.name == "Level4" && (support.name == "West Wall" || support.name == "East Wall")) continue;
                RequireInside(scene, layout, support.bounds, support.name);
            }
            foreach (var boss in U.All<ShadowKingBoss>(scene)) RequireInside(scene, layout, boss.GetComponent<Collider2D>().bounds, boss.name);
            foreach (var platform in U.All<MovingPlatform>(scene))
            {
                var data = new SerializedObject(platform);
                Vector2 start = data.FindProperty("start").vector2Value, end = data.FindProperty("end").vector2Value;
                float speed = data.FindProperty("speed").floatValue;
                verticalSpeed = Mathf.Max(verticalSpeed, Mathf.Abs((end - start).normalized.y) * speed);
                Bounds bounds = platform.GetComponent<Collider2D>().bounds;
                foreach (var point in new[] { start, end })
                {
                    Bounds endpoint = bounds; endpoint.center += (Vector3)(point - (Vector2)platform.transform.position);
                    RequireInside(scene, layout, endpoint, platform.name);
                    highest = Mathf.Max(highest, endpoint.max.y);
                }
            }
            foreach (var hazard in U.All<FallHazard>(scene))
                foreach (var collider in hazard.GetComponents<Collider2D>()) layout.bottom = Mathf.Min(layout.bottom, collider.bounds.min.y);
            float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
            if (gravity < .01f) throw new InvalidOperationException("Cannot derive boundary height without downward player gravity.");
            float jump = new SerializedObject(player).FindProperty("jumpSpeed").floatValue + verticalSpeed;
            layout.top = highest + jump * jump / (2 * gravity) + playerBox.size.y + .5f;
            layout.bottom -= playerBox.size.y + .5f;
            if (Physics2D.GetIgnoreLayerCollision(layout.layer, player.gameObject.layer)) throw new InvalidOperationException(scene.name + ": ground layer does not collide with the player.");
            foreach (var coin in U.All<CoinCollectible>(scene)) RequireInside(scene, layout, coin.GetComponent<Collider2D>().bounds, coin.name);
            foreach (var gate in U.All<ExitGate>(scene)) RequireInside(scene, layout, gate.GetComponent<Collider2D>().bounds, gate.name);
            return layout;
        }
        private static bool HasAncestor(Transform t, string name)
        { for (; t != null; t = t.parent) if (t.name == name) return true; return false; }
        private static void RequireInside(Scene scene, Layout layout, Bounds bounds, string name)
        {
            if (bounds.min.x < layout.left - .01f || bounds.max.x > layout.right + .01f)
                throw new InvalidOperationException(scene.name + ": " + name + " extends outside the measured floor. Inspect geometry before applying walls.");
        }
        public static void ApplyScene(Scene scene)
        {
            var layout = Measure(scene);
            Configure(scene, "LeftBoundary", layout.left - layout.thickness / 2, layout);
            Configure(scene, "RightBoundary", layout.right + layout.thickness / 2, layout);
            ValidateScene(scene);
        }
        private static void Configure(Scene scene, string name, float x, Layout layout)
        {
            var t = Existing(scene, name);
            if (t == null) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); t = go.transform; }
            t.name = name; t.gameObject.SetActive(true); t.gameObject.layer = layout.layer;
            t.rotation = Quaternion.identity; t.localScale = Vector3.one;
            t.position = new Vector3(x, (layout.top + layout.bottom) / 2, 0);
            foreach (var renderer in t.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var body = t.GetComponent<Rigidbody2D>();
            if (body != null) { body.bodyType = RigidbodyType2D.Static; body.simulated = true; }
            var box = t.GetComponent<BoxCollider2D>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider2D>();
            box.enabled = true; box.isTrigger = false; box.offset = Vector2.zero;
            box.compositeOperation = Collider2D.CompositeOperation.None;
            box.size = new Vector2(layout.thickness, layout.top - layout.bottom);
            box.edgeRadius = 0;
            box.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Prefabs/Foundation/PlayerFriction.physicsMaterial2D");
            // Reused manual objects may have collider-specific layer overrides.
            box.includeLayers = 0; box.excludeLayers = 0;
            EditorUtility.SetDirty(t.gameObject); EditorUtility.SetDirty(box);
        }
        private static void ValidateScene(Scene scene)
        {
            var layout = Measure(scene); Physics2D.SyncTransforms();
            if (scene.name == "Level3" && U.All<LevelDefinition>(scene).Single().NextScene != "Level4")
                throw new InvalidOperationException("Level3 must still transition to Level4; no progression settings are changed by this tool.");
            if (scene.name == "Level4" && (U.All<ShadowKingBoss>(scene).Length != 1 || U.All<ShadowKingHealth>(scene).Length != 1))
                throw new InvalidOperationException("Level4 must retain its single Shadow King boss and health component.");
            foreach (string name in new[] { "LeftBoundary", "RightBoundary" })
            {
                var t = Existing(scene, name);
                var box = t != null ? t.GetComponent<BoxCollider2D>() : null;
                if (box == null || !box.enabled || box.isTrigger || !t.gameObject.activeInHierarchy || (box.attachedRigidbody != null && (!box.attachedRigidbody.simulated || box.attachedRigidbody.bodyType != RigidbodyType2D.Static)) || t.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled))
                    throw new InvalidOperationException(scene.name + ": invalid solid invisible " + name);
                float face = name == "LeftBoundary" ? box.bounds.max.x : box.bounds.min.x;
                float expected = name == "LeftBoundary" ? layout.left : layout.right;
                if (Mathf.Abs(face - expected) > .01f || box.bounds.max.y < layout.top - .01f || box.bounds.min.y > layout.bottom + .01f || box.gameObject.layer != layout.layer)
                    throw new InvalidOperationException(scene.name + ": boundary dimensions/layer need repair.");
            }
            Debug.Log(scene.name + ": solid exterior boundaries validated at x=" + layout.left + " and " + layout.right + "; internal pits unchanged.");
        }
    }
}
