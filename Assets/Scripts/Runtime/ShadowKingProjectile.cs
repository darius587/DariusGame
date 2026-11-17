using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class ShadowKingProjectile : MonoBehaviour
    {
        [SerializeField] private EnemyStats stats = null;
        private bool spent;
        private void Awake() => Destroy(gameObject, stats.bossProjectileLifetime);
        public void Launch(Vector2 velocity, Collider2D owner)
        {
            Physics2D.IgnoreCollision(GetComponent<Collider2D>(), owner);
            GetComponent<Rigidbody2D>().linearVelocity = velocity;
        }
        public void Disarm() { spent = true; GetComponent<Collider2D>().enabled = false; Destroy(gameObject); }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (spent) return;
            spent = true;
            collision.collider.GetComponentInParent<PlayerHealth>()?.TakeDamage(stats.bossDamage);
            Destroy(gameObject);
        }
    }
}
