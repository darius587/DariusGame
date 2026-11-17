using UnityEngine;
namespace Zhuzbai.Platformer
{
    // Explicit values preserve existing arena/boss asset serialization.
    public enum LevelGoal { CollectCoinsAndExit = 0, DefeatBoss = 1, ReachExit = 2 }
    [System.Serializable]
    public sealed class LevelEnemyConfiguration
    {
        public string objectName;
        public Vector3 spawnPosition;
        public EnemyStats stats;
    }
    [CreateAssetMenu(menuName = "Darius/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public int levelNumber;
        public string levelName;
        public string sceneName;
        public string nextScene;
        [Min(0)] public float timeLimit; // Zero keeps the existing untimed platformer.
        public LevelGoal goal;
        [Tooltip("Coin requirement for CollectCoinsAndExit; available coins for ReachExit.")]
        [Min(0)] public int goalAmount;
        [TextArea] public string objective;
        public Vector3 playerSpawn;
        public LevelEnemyConfiguration[] enemies = System.Array.Empty<LevelEnemyConfiguration>();
    }
}
