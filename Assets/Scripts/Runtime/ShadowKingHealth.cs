using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class ShadowKingHealth : MonoBehaviour, IEnemyStatsReceiver
    {
        [SerializeField] private EnemyStats stats = null;
        public void ApplyStats(EnemyStats data) { if (data != null) stats = data; CurrentHealth = MaximumHealth; }
        public int MaximumHealth => stats.bossMaximumHealth;
        private readonly HashSet<EntityId> hits = new HashSet<EntityId>();
        public int CurrentHealth { get; private set; }
        private void Awake() => CurrentHealth = MaximumHealth;
        public bool IsDead => CurrentHealth == 0;
        public int Phase => CurrentHealth >= stats.bossPhaseThreshold ? 1 : 2;
        public event Action Changed;
        public event Action Damaged;
        public event Action Defeated;
        public bool TryTakeBulletHit(BulletProjectile bullet)
        {
            if (bullet == null || IsDead || GameMenuController.IsGameplayBlocked || !hits.Add(bullet.GetEntityId())) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - bullet.Damage);
            Changed?.Invoke();
            if (IsDead) Defeated?.Invoke();
            else Damaged?.Invoke();
            return true;
        }
    }
}
