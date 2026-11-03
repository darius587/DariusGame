using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class LevelDefinition : MonoBehaviour
    {
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelName = "Green Valley";
        [SerializeField] private int requiredCoins = 8;
        [SerializeField] private string nextScene = "Level2";
        [SerializeField] private Transform spawnPoint = null;
        [SerializeField, TextArea] private string objective = "Learn the platforms, collect 8 coins, find the exit.";
        [SerializeField, HideInInspector] private string generatedLayoutScene = "";
        [SerializeField] private bool bossLevel = false;
        public int LevelNumber => levelNumber;
        public string LevelName => levelName;
        public int RequiredCoins => requiredCoins;
        public string NextScene => nextScene;
        public Transform SpawnPoint => spawnPoint;
        public string Objective => objective;
        public string GeneratedLayoutScene => generatedLayoutScene;
        public bool IsBossLevel => bossLevel;
    }
}
