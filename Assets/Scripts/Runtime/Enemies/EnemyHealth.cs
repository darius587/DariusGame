using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, IEnemyStatsReceiver
    {
        [SerializeField] private EnemyStats stats = null;
        public void ApplyStats(EnemyStats data) { if (data != null) stats = data; CurrentHealth = Mathf.Max(1, maximumHealth); }
        private int maximumHealth => stats.enemyHealth_maximumHealth;

        public int CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        private void Awake() => CurrentHealth = Mathf.Max(1, maximumHealth);

        public void TakeDamage(int amount)
        {
            if (IsDead || amount <= 0 || GameMenuController.IsGameplayBlocked) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            AudioManager.Instance?.Play(GameSound.Damage);
            if (!IsDead) return;

            // Stop movement and collisions immediately, even before Destroy runs.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
