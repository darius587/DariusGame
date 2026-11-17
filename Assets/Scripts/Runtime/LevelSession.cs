using System;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    public sealed class LevelSession : MonoBehaviour
    {
        public int Coins { get; private set; }
        public int TotalCoins { get; private set; }
        public int Score { get; private set; }
        public bool Completed { get; private set; }
        public LevelDefinition Definition { get; private set; }
        public bool GateUnlocked => Definition != null && Definition.IsBossLevel ? false :
            (Definition != null && Definition.CoinsOptional) || Coins >= TotalCoins;
        public event Action Changed;

        private float elapsed;
        private void Update()
        {
            if (Completed || GameMenuController.IsGameplayBlocked || Definition == null || Definition.Data == null || Definition.Data.timeLimit <= 0) return;
            elapsed += Time.deltaTime;
            if (elapsed >= Definition.Data.timeLimit)
            {
                var player = FindAnyObjectByType<PlayerHealth>();
                if (player != null) player.TakeDamage(player.MaximumHealth);
            }
        }
        private void Start()
        {
            Definition = GetComponent<LevelDefinition>();
            LevelDataConfigurator.Configure(Definition);
            TotalCoins = Definition != null ? Definition.RequiredCoins : FindObjectsByType<CoinCollectible>().Length;
            if (Definition != null && Definition.Data == null && Definition.SpawnPoint != null)
            {
                var player = FindAnyObjectByType<PlayerHealth>();
                if (player != null) player.SetSpawnPoint(Definition.SpawnPoint.position);
            }
            Changed?.Invoke();
        }

        public void CollectCoin(int value = 1, int score = 1)
        {
            if (Completed) return;
            Coins += Mathf.Max(1, value);
            Score += Mathf.Max(0, score);
            Changed?.Invoke();
        }

        public void Complete()
        {
            if (Completed || !GateUnlocked) return;
            Completed = true;
            SaveManager.Instance.CompleteLevel(gameObject.scene.name, Definition != null ? Definition.NextScene : null, Coins, Score);
            Changed?.Invoke();
            Debug.Log("Level complete! Exit gate reached.");
        }
        public void CompleteBossEncounter(ShadowKingHealth boss)
        {
            if (Completed || Definition == null || !Definition.IsBossLevel || boss == null || !boss.IsDead) return;
            Completed = true;
            SaveManager.Instance.CompleteLevel(gameObject.scene.name, Definition != null ? Definition.NextScene : null, Coins, Score);
            Changed?.Invoke();
        }
    }
}
