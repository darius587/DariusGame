using System;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FallHazard : MonoBehaviour
    {
        public event Action<PlayerHealth> PlayerFell;
        private void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponentInParent<PlayerHealth>();
            TryHandleFall(health);
        }
        public bool TryHandleFall(PlayerHealth health) => health != null && health.HandleAbyssFall(PlayerFell);
    }
}
