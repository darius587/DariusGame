using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class FallingProjectile : MonoBehaviour
    {
        [SerializeField] private bool bomb = false;
        [SerializeField] private int damage = 1;
        [SerializeField] private float explosionRadius = 1.65f;
        [SerializeField] private float fuseSeconds = 1.4f;
        [SerializeField] private float warningSeconds = 0.7f;
        private readonly Collider2D[] hits = new Collider2D[64];
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private GameObject warning;
        private float born;
        private float explodeAt = float.PositiveInfinity;
        private bool spent;
        public void SetWarning(GameObject value) => warning = value;
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>();
            born = Time.time; body.simulated = false;
        }
        private void Update()
        {
            if (spent) return;
            if (!body.simulated && Time.time - born >= warningSeconds) body.simulated = true;
            if (visual != null) visual.color = bomb && ((int)(Time.time * 8) % 2 == 0) ? new Color(1, 0.45f, 0.25f) : Color.white;
            if (warning != null)
            {
                var marker = warning.GetComponent<SpriteRenderer>();
                if (marker != null) marker.color = new Color(1, 0.35f, 0.1f, 0.35f + Mathf.Sin(Time.time * 9) * 0.15f);
            }
            if (bomb && Time.time >= explodeAt) Explode();
            if (Time.time - born > 8 || transform.position.y < -8) Destroy(gameObject);
        }
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (spent) return;
            if (bomb)
            {
                if (float.IsPositiveInfinity(explodeAt)) explodeAt = Time.time + fuseSeconds;
                if (warning != null) warning.transform.position = body.position;
                return;
            }
            spent = true;
            collision.collider.GetComponentInParent<PlayerHealth>()?.TakeDamage(damage);
            Destroy(gameObject);
        }
        private void Explode()
        {
            spent = true;
            var filter = new ContactFilter2D { useTriggers = false };
            int count = Physics2D.OverlapCircle(body.position, explosionRadius, filter, hits);
            PlayerHealth damagedPlayer = null;
            for (int i = 0; i < count; i++)
            {
                var health = hits[i].GetComponentInParent<PlayerHealth>();
                if (health == null || health == damagedPlayer) continue;
                damagedPlayer = health;
                health.TakeDamage(damage);
            }
            if (warning != null)
            {
                warning.transform.position = body.position;
                warning.transform.localScale = Vector3.one * (explosionRadius * 2);
                Destroy(warning, 0.2f); warning = null;
            }
            Destroy(gameObject);
        }
        private void OnDestroy() { if (warning != null) Destroy(warning); }
    }
}
