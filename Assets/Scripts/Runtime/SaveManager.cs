using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zhuzbai.Platformer
{
    [DefaultExecutionOrder(-2000)]
    public sealed class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        public SaveData Data { get; private set; }
        public event Action Changed;
        private bool initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null) new GameObject("Save Manager").AddComponent<SaveManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnEnable()
        {
            // Recover static state if Unity recompiles scripts during Play Mode.
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); Load(); }
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void Load() { Data = SaveSystem.Load(); initialized = true; Changed?.Invoke(); }
        public void Save() { if (initialized) { SaveSystem.Save(Data); PlayerPrefs.Save(); } }
        public bool IsUnlocked(string scene) => scene == "EnemyAI" || scene == "MainMenu" || Data.unlockedLevels.Contains(scene);

        public void CompleteLevel(string scene, string next, int coins, int score)
        {
            if (!IsUnlocked(scene)) return;
            // Replays earn coins too; a session calls this only once per completion.
            Data.totalCoins = (int)Math.Min(int.MaxValue, (long)Data.totalCoins + Math.Max(0, coins));
            Data.bestScore = Math.Max(Data.bestScore, score);
            if (!Data.completedLevels.Contains(scene)) Data.completedLevels.Add(scene);
            if (!string.IsNullOrEmpty(next) && !Data.unlockedLevels.Contains(next)) Data.unlockedLevels.Add(next);
            Data.Normalize();
            Save();
            Changed?.Invoke();
        }

        public bool Delete()
        {
            if (!SaveSystem.Delete()) return false;
            Data = new SaveData();
            // Existing audio/display preferences are the only settings this game owns.
            PlayerPrefs.DeleteKey("Zhuzbai.SfxVolume");
            PlayerPrefs.DeleteKey("Zhuzbai.MusicVolume");
            PlayerPrefs.DeleteKey("Zhuzbai.Fullscreen");
            AudioManager.Instance?.ResetSavedSettings();
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if ((scene.name == "Level2" || scene.name == "Level3" || scene.name == "Level4") && !IsUnlocked(scene.name))
            {
                Debug.LogWarning("Blocked entry to locked level " + scene.name);
                Time.timeScale = 1;
                SceneManager.LoadScene("MainMenu");
            }
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        private void OnApplicationQuit() => Save();
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
