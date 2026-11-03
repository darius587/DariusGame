using System;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    public sealed class LevelSession : MonoBehaviour
    {
        public int Coins { get; private set; }
        public int TotalCoins { get; private set; }
        public bool Completed { get; private set; }
        public LevelDefinition Definition { get; private set; }
        public bool GateUnlocked => TotalCoins > 0 && Coins >= TotalCoins;
        public event Action Changed;

        private void Start()
        {
            Definition = GetComponent<LevelDefinition>();
            TotalCoins = Definition != null ? Definition.RequiredCoins : FindObjectsByType<CoinCollectible>().Length;
            if (Definition != null && Definition.SpawnPoint != null)
            {
                var player = FindAnyObjectByType<PlayerHealth>();
                if (player != null) player.SetSpawnPoint(Definition.SpawnPoint.position);
            }
            Changed?.Invoke();
        }

        public void CollectCoin()
        {
            if (Completed) return;
            Coins++;
            Changed?.Invoke();
        }

        public void Complete()
        {
            if (Completed || !GateUnlocked) return;
            Completed = true;
            Changed?.Invoke();
            Debug.Log("Level complete! Exit gate reached.");
        }
        public void CompleteBossEncounter(ShadowKingHealth boss)
        {
            if (Completed || Definition == null || !Definition.IsBossLevel || boss == null || !boss.IsDead) return;
            Completed = true;
            Changed?.Invoke();
        }
    }
}
