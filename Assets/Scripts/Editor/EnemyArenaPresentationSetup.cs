using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class EnemyArenaPresentationSetup
    {
        internal const string RobotArt = "Phase5/ShooterRobot";
        internal const string PulseArt = "Phase5/ShooterPulse";
        private const string SpriteRoot = "Assets/Sprites/";
        private const string ShooterPrefab = "Assets/Prefabs/Enemies/Shooter.prefab";
        private const string ProjectilePrefab = "Assets/Prefabs/Enemies/EnemyProjectile.prefab";

        [MenuItem("Tools/Zhuzbai/Apply Stage Selection and Shooter Art")]
        public static void Apply()
        {
            U.RequireEditMode();
            if (SceneManager.GetSceneByPath(U.Menu).isDirty)
                throw new InvalidOperationException("Save MainMenu before applying the stage selection.");
            foreach (string path in new[] { EnemyArenaSetup.ScenePath, ShooterPrefab, ProjectilePrefab })
                if (!File.Exists(path)) throw new FileNotFoundException("Set up the Enemy Arena first.", path);

            EnsureSprites();
            SetPrefabArt(ShooterPrefab, RobotArt, 1f);
            SetPrefabArt(ProjectilePrefab, PulseArt, 0.6f);
            U.EditScene(U.Menu, scene =>
            {
                GameMenuSetup.BuildStageSelection(U.All<GameMenuController>(scene).Single());
                GameMenuSetup.ValidateStageSelection(scene);
            });
            EnemyArenaSetup.Validate();
        }

        internal static void EnsureSprites()
        {
            EnsureSprite(RobotArt, 24, PaintRobot);
            EnsureSprite(PulseArt, 12, PaintPulse);
        }

        internal static void ValidateShooterArt(ShooterEnemy shooter, EnemyProjectile projectile)
        {
            var sprite = shooter.GetComponentInChildren<SpriteRenderer>().sprite;
            string prefab = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shooter.gameObject);
            if (AssetDatabase.GetAssetPath(sprite) != ArtPath(RobotArt) ||
                AssetDatabase.GetAssetPath(projectile.GetComponent<SpriteRenderer>().sprite) != ArtPath(PulseArt) ||
                AssetDatabase.GetDependencies(prefab).Any(path => path.StartsWith("Assets/Sprites/ShadowKing/", StringComparison.Ordinal)))
                throw new InvalidOperationException("The Shooter and its projectile require their own robot and pulse sprites.");
        }

        private static void SetPrefabArt(string path, string art, float scale)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual = root.GetComponentInChildren<SpriteRenderer>();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath(art));
                if (visual == null || sprite == null) throw new InvalidOperationException("Missing visual for " + path);
                visual.sprite = sprite;
                visual.color = Color.white;
                visual.transform.localScale = Vector3.one * scale;
                if (visual.transform != root.transform) visual.transform.localPosition = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new IOException("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string ArtPath(string art) => SpriteRoot + art + ".png";

        private static void EnsureSprite(string art, int size, Action<Texture2D> paint)
        {
            string path = ArtPath(art);
            if (File.Exists(path)) return;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(new Color32[size * size]);
                paint(texture);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 20f;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void PaintRobot(Texture2D texture)
        {
            Color32 outline = new Color32(25, 14, 48, 255);
            Color32 shade = new Color32(76, 37, 123, 255);
            Color32 purple = new Color32(138, 71, 221, 255);
            Color32 light = new Color32(206, 151, 255, 255);
            Color32 cyan = new Color32(87, 240, 247, 255);

            // Boots, chassis, and helmet. The broad highlights survive a small phone viewport.
            Fill(texture, 3, 2, 7, 5, outline);
            Fill(texture, 11, 2, 7, 5, outline);
            Fill(texture, 4, 3, 5, 3, shade);
            Fill(texture, 12, 3, 5, 3, shade);
            Fill(texture, 4, 4, 4, 1, light);
            Fill(texture, 13, 4, 4, 1, light);
            Fill(texture, 4, 6, 14, 10, outline);
            Fill(texture, 5, 7, 12, 8, purple);
            Fill(texture, 5, 7, 3, 7, shade);
            Fill(texture, 9, 8, 3, 3, light);
            Fill(texture, 5, 13, 13, 8, outline);
            Fill(texture, 6, 14, 11, 6, purple);
            Fill(texture, 7, 19, 8, 1, light);
            Fill(texture, 10, 16, 7, 3, outline);
            Fill(texture, 10, 17, 4, 1, cyan);
            Fill(texture, 7, 20, 2, 3, outline);
            Fill(texture, 7, 22, 2, 1, light);

            // The barrel ends at world (+0.55, +0.20), matching the existing mirrored muzzle.
            Fill(texture, 14, 12, 9, 7, outline);
            Fill(texture, 15, 13, 6, 5, shade);
            Fill(texture, 15, 16, 6, 2, light);
            Fill(texture, 19, 14, 2, 2, purple);
            Fill(texture, 22, 14, 1, 4, cyan);
            Fill(texture, 22, 15, 1, 2, Color.white);
        }

        private static void PaintPulse(Texture2D texture)
        {
            Fill(texture, 2, 4, 8, 4, new Color32(117, 52, 204, 255));
            Fill(texture, 4, 2, 4, 8, new Color32(117, 52, 204, 255));
            Fill(texture, 3, 3, 6, 6, new Color32(190, 122, 255, 255));
            Fill(texture, 4, 4, 4, 4, new Color32(117, 238, 255, 255));
            Fill(texture, 5, 5, 2, 2, Color.white);
        }

        private static void Fill(Texture2D texture, int x, int y, int width, int height, Color32 color)
        {
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++)
                    texture.SetPixel(column, row, color);
        }
    }
}
