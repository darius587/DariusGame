using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class LevelDefinition : MonoBehaviour
    {
        [SerializeField] private LevelData levelData = null;
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelName = "Green Valley";
        [SerializeField] private int requiredCoins = 8;
        [SerializeField] private string nextScene = "Level2";
        [SerializeField] private Transform spawnPoint = null;
        [SerializeField, TextArea] private string objective = "Learn the platforms, collect 8 coins, find the exit.";
        [SerializeField, HideInInspector] private string generatedLayoutScene = "";
        [SerializeField] private bool bossLevel = false;
        [SerializeField] private Transform[] enemyObjects = System.Array.Empty<Transform>();
        public Transform[] EnemyObjects => enemyObjects;
        public LevelData Data => levelData;
        public int LevelNumber => levelData != null ? levelData.levelNumber : levelNumber;
        public string LevelName => levelData != null ? levelData.levelName : levelName;
        public int RequiredCoins => levelData != null ? levelData.goalAmount : requiredCoins;
        public string NextScene => levelData != null ? levelData.nextScene : nextScene;
        public Transform SpawnPoint => spawnPoint;
        public string Objective => levelData != null ? levelData.objective : objective;
        public string GeneratedLayoutScene => generatedLayoutScene;
        public bool IsBossLevel => levelData != null ? levelData.goal == LevelGoal.DefeatBoss : bossLevel;
        public bool CoinsOptional => levelData != null && levelData.goal == LevelGoal.ReachExit;
    }
}
