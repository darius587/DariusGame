using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Zhuzbai.Platformer.Editor
{
    public static class GameDataValidator
    {
        [MenuItem("Tools/Zhuzbai/Game Validation/Verify Optional Campaign Coins")]
        public static void VerifyOptionalCampaignCoins()
        {
            GameEditorUtility.RequireEditMode();
            Require(SaveManager.Instance == null, "Run outside Play Mode without a live SaveManager.");
            // Preserve the actual save byte-for-byte; all completion writes use Logs.
            string realPath = Path.Combine(Application.persistentDataPath, "save.json");
            byte[] original = File.Exists(realPath) ? File.ReadAllBytes(realPath) : null;
            string previousPath = SaveSystem.TestSavePath;
            Directory.CreateDirectory("Logs/OptionalCoinTests");
            SaveSystem.TestSavePath = Path.GetFullPath("Logs/OptionalCoinTests/save.json");
            var testScene = SceneManager.GetActiveScene();
            var owner = new GameObject("Test SaveManager");
            SceneManager.MoveGameObjectToScene(owner, testScene);
            var manager = owner.AddComponent<SaveManager>();
            var instance = typeof(SaveManager).GetProperty("Instance");
            instance.SetValue(null, manager);
            try
            {
                foreach (string scene in new[] { "MainLevel", "Level2", "Level3", "Level4", "EnemyAI", "MainMenu" })
                    GameEditorUtility.EditScene("Assets/Scenes/" + scene + ".unity", VerifyReferences, false);
                VerifySaves();
                SaveSystem.TestSavePath = Path.GetFullPath("Logs/OptionalCoinTests/save.json");
                Require(SaveSystem.Delete(), "Could not clear isolated test data");
                manager.Load();
                Action<string, string, int, int, int> complete = (sceneName, assetName, coins, best, total) =>
                {
                    var scene = CreateTestScene(sceneName);
                    try
                    {
                        var go = new GameObject("Test Session");
                        SceneManager.MoveGameObjectToScene(go, scene);
                        var definition = go.AddComponent<LevelDefinition>();
                        GameEditorUtility.Reference(definition, "levelData", AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Data/Levels/" + assetName + "_Data.asset"));
                        var session = go.AddComponent<LevelSession>();
                        typeof(LevelSession).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                        Require(!session.Completed && session.GateUnlocked, "Campaign must allow the gate without auto-completing");
                        int available = sceneName == "MainLevel" ? 8 : sceneName == "Level2" ? 10 : 12;
                        Require(session.TotalCoins == available, "HUD available coin count changed");
                        int bankedBefore = manager.Data.totalCoins;
                        int bestBefore = manager.Data.bestScore;
                        for (int i = 0; i < coins; i++) session.CollectCoin();
                        // Quitting/focus/pause save must not bank an unfinished run.
                        manager.Save();
                        var unfinished = SaveSystem.Load();
                        Require(unfinished.totalCoins == bankedBefore && unfinished.bestScore == bestBefore, "Unfinished coins were banked");
                        session.Complete();
                        session.Complete(); // Trigger-stay/repeated completion cannot award twice.
                        var saved = SaveSystem.Load();
                        Require(session.Completed && saved.bestScore == best && saved.totalCoins == total, "Incorrect completed-run rewards");
                        Require(saved.completedLevels.Contains(sceneName) && saved.unlockedLevels.Contains(definition.NextScene), "Progression failed");
                        Debug.Log($"OPTIONAL COINS PASS: {sceneName} {coins}/{available}, best={best}, total={total}");
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                };
                complete("MainLevel", "GreenValley", 0, 0, 0);
                complete("MainLevel", "GreenValley", 5, 5, 5);
                complete("MainLevel", "GreenValley", 8, 8, 13);
                Require(SaveSystem.Delete(), "Could not reset isolated sequence"); manager.Load();
                complete("MainLevel", "GreenValley", 5, 5, 5);
                complete("Level2", "DesertCanyon", 7, 7, 12);
                complete("MainLevel", "GreenValley", 8, 8, 20);
                complete("Level3", "DarkCastle", 6, 8, 26);
                // Existing progress loads and replays without losing prior records/unlocks.
                manager.Data.bestScore = 100; manager.Data.totalCoins = 500;
                manager.Save(); manager.Load();
                complete("MainLevel", "GreenValley", 0, 100, 500);
                foreach (string sceneName in new[] { "EnemyAI", "Level4" })
                {
                    var scene = CreateTestScene(sceneName);
                    try
                    {
                        var go = new GameObject("Special Objective Session");
                        SceneManager.MoveGameObjectToScene(go, scene);
                        var definition = go.AddComponent<LevelDefinition>();
                        GameEditorUtility.Reference(definition, "levelData", AssetDatabase.LoadAssetAtPath<LevelData>("Assets/Data/Levels/" + (sceneName == "EnemyAI" ? "EnemyArena" : "ShadowKing") + "_Data.asset"));
                        var session = go.AddComponent<LevelSession>();
                        typeof(LevelSession).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(session, null);
                        session.Complete();
                        Require(!session.GateUnlocked && !session.Completed, "Special objective bypassed");
                        if (sceneName == "EnemyAI")
                        {
                            for (int i = 0; i < 3; i++) session.CollectCoin();
                            Require(session.GateUnlocked, "Arena three-coin rule broken");
                        }
                        else
                        {
                            var boss = go.AddComponent<ShadowKingHealth>();
                            GameEditorUtility.Reference(boss, "stats", AssetDatabase.LoadAssetAtPath<EnemyStats>("Assets/Data/Balance/Boss_Stats.asset"));
                            boss.ApplyStats(AssetDatabase.LoadAssetAtPath<EnemyStats>("Assets/Data/Balance/Boss_Stats.asset"));
                            session.CompleteBossEncounter(boss);
                            Require(!session.Completed, "Living boss allowed completion");
                            typeof(ShadowKingHealth).GetProperty("CurrentHealth").SetValue(boss, 0);
                            session.CompleteBossEncounter(boss);
                            Require(session.Completed, "Defeated boss no longer completes");
                        }
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
                Require(SaveData.CurrentVersion == 1 && typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).OrderBy(n => n).SequenceEqual(new[] { "bestScore", "completedLevels", "saveVersion", "totalCoins", "unlockedLevels" }), "Save schema changed");
                File.WriteAllText("Logs/optional-coins-verification.txt", "PASS: compilation, scene references, Green Valley 0/8, 5/8, 8/8; Desert Canyon 7/10; Dark Castle 6/12; best/total/replays; duplicate completion; unfinished rewards; unlocks; existing records; boss/alive/dead guards; arena objective; JSON schema and safe loading. Edit-mode logic tests only; gate traversal, visuals/audio and Android require manual testing.\n");
                Debug.Log("OPTIONAL COIN VERIFICATION PASSED");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                instance.SetValue(null, null);
                SaveSystem.TestSavePath = previousPath;
                SaveSystem.Load();
                Require(original == null ? !File.Exists(realPath) : File.Exists(realPath) && original.SequenceEqual(File.ReadAllBytes(realPath)), "Real save changed during isolated tests");
            }
        }

        private static Scene CreateTestScene(string name)
        {
            return EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
        }

        [MenuItem("Tools/Zhuzbai/Game Validation/Configure and Verify Data")]
        public static void Run()
        {
            GameEditorUtility.RequireEditMode();
            foreach (string sceneName in new[] { "MainLevel", "Level2", "Level3", "Level4", "EnemyAI" })
                GameEditorUtility.EditScene("Assets/Scenes/" + sceneName + ".unity", scene =>
                {
                    var definition = GameEditorUtility.All<LevelDefinition>(scene).Single();
                    Require(definition.Data != null, "Missing LevelData in " + sceneName);
                    var enemies = GameEditorUtility.All<MonoBehaviour>(scene)
                        .Where(c => c is PatrolEnemy || c is PatrollerEnemy || c is ShooterEnemy || c is ChargerEnemy || c is ShadowKingBoss).ToArray();
                    definition.Data.enemies = enemies.Select(e => new LevelEnemyConfiguration {
                        objectName = e.name, spawnPosition = e.transform.position,
                        stats = (EnemyStats)new SerializedObject(e).FindProperty("stats").objectReferenceValue
                    }).ToArray();
                    GameEditorUtility.References(definition, "enemyObjects", enemies.Select(e => e.transform).ToArray());
                    EditorUtility.SetDirty(definition.Data);
                    VerifyReferences(scene);
                    var menu = GameEditorUtility.All<GameMenuController>(scene).Single();
                    var menuData = new SerializedObject(menu);
                    Require(menuData.FindProperty("settingsPanel").objectReferenceValue != null, "Missing settings panel");
                    Debug.Log("GAME DATA: " + sceneName + " references valid; " + enemies.Length + " authored enemies configured.");
                });
            GameEditorUtility.EditScene("Assets/Scenes/MainMenu.unity", scene => {
                VerifyReferences(scene);
                var menu = new SerializedObject(GameEditorUtility.All<GameMenuController>(scene).Single());
                Require(menu.FindProperty("stageSelectionPanel").objectReferenceValue != null, "Missing level select parent");
            }, false);
            foreach (string path in AssetDatabase.FindAssets("t:Prefab").Select(AssetDatabase.GUIDToAssetPath).Where(p => p.StartsWith("Assets/Prefabs/")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true)) VerifyComponent(component);
            }
            AssetDatabase.SaveAssets();
            VerifySaves();
            File.WriteAllText("game-data-validation.txt", "PASS: Unity compilation, six scene references, prefab references, authored enemy configuration, JSON round-trip, missing/empty/null/truncated saves, unsupported versions, normalization, deletion.\nPlay Mode and Android device verification remain required.\n");
            Debug.Log("GAME DATA VALIDATION PASSED");
        }

        private static void VerifyReferences(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) VerifyComponent(component);
        }
        private static void VerifyComponent(MonoBehaviour component)
        {
            Require(component != null, "Missing script reference");
            var data = new SerializedObject(component);
            foreach (string field in new[] { "stats", "itemData" })
            {
                var property = data.FindProperty(field);
                if (property != null) Require(property.objectReferenceValue != null, "Missing " + field + " on " + component.name + "/" + component.GetType().Name);
            }
        }

        // Redirect file tests into a disposable project folder; never touch a player's save.
        private static void VerifySaves()
        {
            string folder = Path.GetFullPath("Logs/SaveSystemTests");
            Directory.CreateDirectory(folder);
            SaveSystem.TestSavePath = Path.Combine(folder, "save.json");
            try
            {
                Require(SaveSystem.Delete(), "Test cleanup failed");
                var defaults = SaveSystem.Load();
                Require(defaults.unlockedLevels.SequenceEqual(new[] { "MainLevel" }), "Default unlocks incorrect");
                var data = new SaveData { bestScore = 12, totalCoins = 30 };
                data.completedLevels.Add("MainLevel");
                Require(SaveSystem.Save(data), "Save failed");
                var loaded = SaveSystem.Load();
                Require(loaded.bestScore == 12 && loaded.totalCoins == 30 && loaded.unlockedLevels.Contains("Level2"), "Round-trip failed");
                data.totalCoins = 40;
                bool saved = SaveSystem.Save(data);
                var replacement = SaveSystem.Load();
                Require(saved && replacement.totalCoins == 40, "Atomic replacement failed: saved=" + saved + " coins=" + replacement.totalCoins + " JSON=" + File.ReadAllText(SaveSystem.SavePath));
                foreach (string bad in new[] { "", " ", "null", "{}", "{broken", "{\"saveVersion\":1,\"unlockedLevels\":[", "{\"saveVersion\":999,\"unlockedLevels\":[]}" })
                {
                    File.WriteAllText(SaveSystem.SavePath, bad);
                    var recovered = SaveSystem.Load();
                    Require(recovered != null && recovered.unlockedLevels.Contains("MainLevel"), "Safe loading failed");
                    Require(!SaveSystem.Save(recovered) && File.ReadAllText(SaveSystem.SavePath) == bad, "Corrupted save overwritten");
                }
                Require(SaveSystem.Delete(), "Delete failed");
                Require(!File.Exists(SaveSystem.SavePath), "Save remains after delete");
            }
            finally { SaveSystem.TestSavePath = null; SaveSystem.Load(); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
