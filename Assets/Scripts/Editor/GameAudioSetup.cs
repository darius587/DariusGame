using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;

namespace Zhuzbai.Platformer.Editor
{
    public static class GameAudioSetup
    {
        public const string PrefabPath = "Assets/Prefabs/Phase4/AudioManager.prefab";
        private static readonly string[] Scenes = { U.Menu, U.Level, "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity", "Assets/Scenes/Level4.unity" };
        private static readonly string[] Fields = { "music", "mainMenuMusic", "levelMusic", "bossMusic", "gameOver", "win", "footsteps", "coin", "jump", "fall", "shoot", "damage", "click" };
        private static readonly string[,] ImportedClips = {
            { "music", "Assets/Audio/Music/MainMenuMusic.mp3" },
            { "mainMenuMusic", "Assets/Audio/Music/MainMenuMusic.mp3" },
            { "levelMusic", "Assets/Audio/Music/LevelMusic.mp3" },
            { "bossMusic", "Assets/Audio/Music/BossMusic.mp3" },
            { "coin", "Assets/Audio/CoinSound.mp3" },
            { "jump", "Assets/Audio/JumpSound.mp3" },
            { "shoot", "Assets/Audio/ShootSound.mp3" },
            { "damage", "Assets/Audio/DamageSound.mp3" },
            { "fall", "Assets/Audio/DamageSound.mp3" },
            { "gameOver", "Assets/Audio/GameOverSound.mp3" },
            { "win", "Assets/Audio/WinSound.mp3" }
        };
        [MenuItem("Tools/Zhuzbai/Setup Game Audio")]
        public static void Apply()
        {
            U.RequireEditMode();
            if (!File.Exists(U.Level)) throw new FileNotFoundException("MainLevel is missing. Audio setup never regenerates the level.");
            var prefab = EnsurePrefab();
            foreach (string path in Scenes)
                U.EditScene(path, scene => ConnectScene(scene, prefab, scene.name != "MainMenu"));
            AssetDatabase.SaveAssets();
            Debug.Log("Game audio applied: imported MP3s, scene music, and event subscriptions in all five scenes.");
        }
        public static GameObject EnsurePrefab()
        {
            U.Folder("Assets/Audio/Music"); U.Folder("Assets/Audio/SFX"); U.Folder("Assets/Prefabs/Phase4");
            bool existing = File.Exists(PrefabPath);
            var root = existing ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("AudioManager");
            try
            {
                var manager = root.GetComponent<AudioManager>();
                if (existing && manager == null) throw new InvalidOperationException("Existing AudioManager prefab belongs to another system; it will not be overwritten.");
                if (manager == null) manager = root.AddComponent<AudioManager>();
                // Optional click/footsteps remain unchanged when assigned; clear stale references.
                foreach (string field in new[] { "click", "footsteps" })
                {
                    var data = new SerializedObject(manager);
                    if (data.FindProperty(field).objectReferenceValue == null)
                        U.Reference(manager, field, null);
                }
                for (int i = 0; i < ImportedClips.GetLength(0); i++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ImportedClips[i, 1]);
                    if (clip == null) throw new FileNotFoundException("Imported audio clip is missing", ImportedClips[i, 1]);
                    U.Reference(manager, ImportedClips[i, 0], clip);
                }
                RepairManager(manager);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
                if (!success) throw new IOException("Could not save AudioManager prefab.");
                return saved;
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }
        public static void ConnectScene(Scene scene, GameObject prefab, bool gameplay)
        {
            var managers = U.All<AudioManager>(scene);
            if (managers.Length > 1) throw new InvalidOperationException("Multiple AudioManagers found in " + scene.name + ". Resolve duplicates first.");
            var manager = managers.Length == 0
                ? ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<AudioManager>()
                : managers[0];
            // Apply the imported clips to existing instances as well as the prefab.
            // Preserve any assigned optional click and footstep sounds.
            var template = new SerializedObject(prefab.GetComponent<AudioManager>());
            var instance = new SerializedObject(manager);
            foreach (string field in Fields)
                if ((field != "footsteps" && field != "click") || instance.FindProperty(field).objectReferenceValue == null)
                    instance.FindProperty(field).objectReferenceValue = template.FindProperty(field).objectReferenceValue;
            instance.ApplyModifiedPropertiesWithoutUndo();
            RepairManager(manager);
            if (!gameplay) return;
            var players = U.All<PlayerFoundation>(scene);
            var bridges = U.All<GameplayAudioBridge>(scene);
            if (players.Length != 1 || bridges.Length > 1) throw new InvalidOperationException("Expected one player and at most one audio bridge.");
            var bridge = bridges.Length == 1 ? bridges[0] : new GameObject("Phase4 Audio Events").AddComponent<GameplayAudioBridge>();
            U.Reference(bridge, "player", players[0]);
            U.Reference(bridge, "health", players[0].GetComponent<PlayerHealth>());
            U.References(bridge, "coins", U.All<CoinCollectible>(scene));
            U.References(bridge, "fallZones", U.All<FallHazard>(scene));
        }
        private static void RepairManager(AudioManager manager)
        {
            manager.ConfigureAudioSources();
            EditorUtility.SetDirty(manager);
            if (PrefabUtility.IsPartOfPrefabInstance(manager)) PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            foreach (var source in manager.GetComponentsInChildren<AudioSource>(true))
            {
                EditorUtility.SetDirty(source);
                if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
            }
        }
        [MenuItem("Tools/Zhuzbai/Test Game Audio")]
        public static void TestChannelInitialization()
        {
            U.RequireEditMode();
            // Isolated native-Unity regression check; no scenes/prefabs/WAVs are saved or edited.
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Audio channel regression test");
                SceneManager.MoveGameObjectToScene(root, preview);
                foreach (string channel in new[] { "music", "effects", "ui", "footsteps" })
                    new GameObject(channel + " Channel").transform.SetParent(root.transform, false);
                var manager = root.AddComponent<AudioManager>();
                manager.ConfigureAudioSources();
                var first = TestSources(manager);
                manager.ConfigureAudioSources();
                if (!first.SequenceEqual(TestSources(manager))) throw new InvalidOperationException("Repeated initialization replaced healthy sources.");

                // Destroyed Unity objects are managed wrappers that compare == null. Both the
                // missing music and missing SFX component must be recreated on their existing GOs.
                UnityEngine.Object.DestroyImmediate(first[0]);
                UnityEngine.Object.DestroyImmediate(first[1]);
                manager.ConfigureAudioSources();
                var repaired = TestSources(manager);
                if (repaired[2] != first[2] || repaired[3] != first[3])
                    throw new InvalidOperationException("Repair replaced an unrelated healthy audio channel.");
                // Also repair accidentally shared serialized channel references without adding sources.
                U.Reference(manager, "effectsSource", repaired[0]);
                manager.ConfigureAudioSources();
                if (!repaired.SequenceEqual(TestSources(manager))) throw new InvalidOperationException("Shared-source repair failed.");
                Debug.Log("Audio channel initialization test passed: empty channels, repeated setup, removed Music/SFX sources, and shared references. Exactly four sources remain.");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static AudioSource[] TestSources(AudioManager manager)
        {
            var data = new SerializedObject(manager);
            var sources = new[] { "musicSource", "effectsSource", "uiSource", "footstepsSource" }
                .Select(field => data.FindProperty(field).objectReferenceValue as AudioSource).ToArray();
            if (sources.Any(source => source == null || !source.enabled || source.playOnAwake || source.spatialBlend != 0) ||
                sources.Distinct().Count() != 4 || manager.GetComponentsInChildren<AudioSource>(true).Length != 4 ||
                !sources[0].loop || sources[1].loop || sources[2].loop || !sources[3].loop)
                throw new InvalidOperationException("Audio channel initialization regression: missing, duplicate, or misconfigured sources.");
            return sources;
        }
        [MenuItem("Tools/Zhuzbai/Validate Game Audio")]
        public static void Validate()
        {
            U.RequireEditMode();
            foreach (string path in Scenes)
                U.EditScene(path, scene => {
                    var managers = U.All<AudioManager>(scene);
                    if (managers.Length != 1) throw new InvalidOperationException("Expected exactly one AudioManager in " + path);
                    var data = new SerializedObject(managers[0]);
                    foreach (string field in Fields)
                    {
                        var clip = data.FindProperty(field).objectReferenceValue as AudioClip;
                        if ((field == "footsteps" || field == "click") && clip == null) continue;
                        if (clip == null || clip.length <= 0) throw new InvalidOperationException("Missing/empty audio clip: " + field);
                        for (int i = 0; i < ImportedClips.GetLength(0); i++)
                            if (ImportedClips[i, 0] == field && AssetDatabase.GetAssetPath(clip) != ImportedClips[i, 1])
                                throw new InvalidOperationException("Incorrect imported clip: " + field);
                    }
                    var channels = new[] { "musicSource", "effectsSource", "uiSource", "footstepsSource" }
                        .Select(field => data.FindProperty(field).objectReferenceValue as AudioSource).ToArray();
                    if (channels.Any(source => source == null || source.playOnAwake || source.spatialBlend != 0) || channels.Distinct().Count() != 4)
                        throw new InvalidOperationException("Audio requires four distinct 2D sources without Play On Awake.");
                    foreach (var source in channels)
                        if (!source.enabled || !source.gameObject.activeInHierarchy || !source.transform.IsChildOf(managers[0].transform) ||
                            source.GetComponents<AudioSource>().Length != 1)
                            throw new InvalidOperationException("Each manager channel needs one enabled, local AudioSource on an active GameObject.");
                    if (!channels[0].loop || channels[1].loop || channels[2].loop || !channels[3].loop ||
                        channels[0].clip != data.FindProperty("music").objectReferenceValue ||
                        channels[3].clip != data.FindProperty("footsteps").objectReferenceValue)
                        throw new InvalidOperationException("Music/footstep loop or clip configuration is incorrect. Run Apply Game Audio.");
                    if (path != U.Menu)
                    {
                        var bridges = U.All<GameplayAudioBridge>(scene);
                        if (bridges.Length != 1) throw new InvalidOperationException("Missing or duplicated gameplay audio bridge.");
                        var bridge = new SerializedObject(bridges[0]);
                        foreach (string field in new[] { "player", "health" })
                            if (bridge.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing audio event reference: " + field);
                        CheckSubscriptions(bridge.FindProperty("coins"), U.All<CoinCollectible>(scene));
                        CheckSubscriptions(bridge.FindProperty("fallZones"), U.All<FallHazard>(scene));
                    }
                }, false);
            Debug.Log("Game audio validated in all five scenes: assigned clips, distinct channels, one manager per scene, and gameplay event references.");
        }
        private static void CheckSubscriptions<T>(SerializedProperty array, T[] expected) where T : UnityEngine.Object
        {
            if (array.arraySize != expected.Length) throw new InvalidOperationException("Audio event subscription count differs from scene objects.");
            for (int i = 0; i < expected.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < array.arraySize; j++) if (array.GetArrayElementAtIndex(j).objectReferenceValue == expected[i]) found = true;
                if (!found) throw new InvalidOperationException("A gameplay audio event subscription is missing.");
            }
        }
    }
}
