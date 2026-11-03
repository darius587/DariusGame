using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BulletProjectile : MonoBehaviour
    {
        [SerializeField, Min(0)] private float speed = 14;
        [SerializeField, Min(0.01f)] private float lifetime = 3;
        private bool spent;
        public float CollisionRadius => GetComponent<CircleCollider2D>().radius *
            Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));

        private void Awake() => Destroy(gameObject, lifetime);

        public void Launch(int direction, GameObject owner)
        {
            var shape = GetComponent<Collider2D>();
            foreach (var ownerCollider in owner.GetComponentsInChildren<Collider2D>(true))
                Physics2D.IgnoreCollision(shape, ownerCollider);
            GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * (direction < 0 ? -speed : speed);
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.flipX = direction < 0;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (spent) return;
            spent = true;
            var boss = collision.collider.GetComponentInParent<ShadowKingHealth>();
            if (boss != null) boss.TryTakeBulletHit(this);
            var enemy = collision.collider.GetComponentInParent<PatrolEnemy>();
            if (enemy != null) enemy.TakeHit(1);
            Destroy(gameObject);
        }
    }
}
