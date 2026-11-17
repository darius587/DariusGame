using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PatrolEnemy : MonoBehaviour, IEnemyStatsReceiver
    {
        [SerializeField] private EnemyStats stats = null;
        public void ApplyStats(EnemyStats data) { if (data != null) stats = data; hitPoints = stats.patrolEnemy_maximumHealth; }
        [SerializeField] private float left = 0;
        [SerializeField] private float right = 3;
        private float speed => stats.patrolEnemy_speed;
        private int hitPoints;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private int direction = 1;
        private void Awake() { hitPoints = stats.patrolEnemy_maximumHealth; body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>(); }
        public void Configure(Vector3 position, EnemyStats data)
        {
            if (data != null) stats = data;
            hitPoints = stats.patrolEnemy_maximumHealth;
            transform.position = position;
            body.position = position;
        }
        private void FixedUpdate()
        {
            if (body.position.x >= right) direction = -1;
            if (body.position.x <= left) direction = 1;
            body.MovePosition(body.position + Vector2.right * (speed * direction * Time.fixedDeltaTime));
            if (visual != null) visual.flipX = direction < 0;
        }
        public void TakeHit(int damage)
        {
            if (hitPoints <= 0 || damage <= 0) return;
            hitPoints = Mathf.Max(0, hitPoints - damage);
            AudioManager.Instance?.Play(GameSound.Damage);
            if (hitPoints == 0) { gameObject.SetActive(false); Destroy(gameObject); }
        }
        private void OnCollisionEnter2D(Collision2D collision) => Hurt(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Hurt(collision.collider);
        private void Hurt(Collider2D other) => other.GetComponentInParent<PlayerHealth>()?.TakeDamage(stats.patrollerEnemy_damage);
    }
}
