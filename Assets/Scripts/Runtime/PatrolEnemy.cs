using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PatrolEnemy : MonoBehaviour
    {
        [SerializeField] private float left = 0;
        [SerializeField] private float right = 3;
        [SerializeField] private float speed = 1.1f;
        [SerializeField] private int hitPoints = 2;
        private Rigidbody2D body;
        private SpriteRenderer visual;
        private int direction = 1;
        private void Awake() { body = GetComponent<Rigidbody2D>(); visual = GetComponent<SpriteRenderer>(); }
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
        private static void Hurt(Collider2D other) => other.GetComponentInParent<PlayerHealth>()?.TakeDamage(1);
    }
}
