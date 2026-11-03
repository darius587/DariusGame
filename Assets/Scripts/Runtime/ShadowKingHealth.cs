using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class ShadowKingHealth : MonoBehaviour
    {
        public const int FullHealth = 15;
        private readonly HashSet<EntityId> hits = new HashSet<EntityId>();
        public int CurrentHealth { get; private set; } = FullHealth;
        public bool IsDead => CurrentHealth == 0;
        public int Phase => CurrentHealth >= 8 ? 1 : 2;
        public event Action Changed;
        public event Action Damaged;
        public event Action Defeated;
        public bool TryTakeBulletHit(BulletProjectile bullet)
        {
            if (bullet == null || IsDead || GameMenuController.IsGameplayBlocked || !hits.Add(bullet.GetEntityId())) return false;
            CurrentHealth = Mathf.Max(0, CurrentHealth - 1);
            Changed?.Invoke();
            if (IsDead) Defeated?.Invoke();
            else Damaged?.Invoke();
            return true;
        }
    }
}
