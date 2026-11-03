using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(ShadowKingHealth), typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class ShadowKingBoss : MonoBehaviour
    {
        [SerializeField] private Animator animator = null;
        [SerializeField] private SpriteRenderer visual = null;
        [SerializeField] private SpriteRenderer warning = null;
        [SerializeField] private Transform muzzle = null;
        [SerializeField] private ShadowKingProjectile projectilePrefab = null;
        [SerializeField] private AudioClip victorySound = null;
        [SerializeField] private PlayerFoundation player = null;
        [SerializeField] private LevelSession session = null;
        private ShadowKingHealth health;
        private Collider2D bodyCollider;
        private readonly List<ShadowKingProjectile> projectiles = new List<ShadowKingProjectile>(12);
        public string WarningText { get; private set; } = "";
        private void Awake() { health = GetComponent<ShadowKingHealth>(); bodyCollider = GetComponent<Collider2D>(); }
        private void OnEnable() { health.Damaged += OnDamaged; health.Defeated += OnDefeated; }
        private void Start() => StartCoroutine(AttackLoop());
        private void OnDisable() { health.Damaged -= OnDamaged; health.Defeated -= OnDefeated; StopAllCoroutines(); ClearProjectiles(); }
        private void Update()
        {
            if (visual != null && !health.IsDead) visual.color = health.Phase == 2 ? new Color(1, 0.62f, 0.8f) : Color.white;
            if (warning != null && warning.gameObject.activeSelf) warning.transform.localScale = Vector3.one * (0.65f + Mathf.Sin(Time.time * 15) * 0.15f);
        }
        private bool Ready => !health.IsDead && player != null && !player.GetComponent<PlayerHealth>().IsDead && !GameMenuController.IsGameplayBlocked;
        private IEnumerator AttackLoop()
        {
            yield return new WaitForSeconds(2.5f); // Safe entry window.
            bool wave = false;
            while (!health.IsDead)
            {
                yield return new WaitUntil(() => Ready);
                bool phase2 = health.Phase == 2;
                wave = phase2 && !wave;
                float side = player.transform.position.x < transform.position.x ? -1 : 1;
                Vector2 origin = new Vector2(transform.position.x + side * 1.15f, muzzle.position.y);
                Vector2 aim = ((Vector2)player.transform.position - origin).normalized;
                WarningText = wave ? "LOW WAVE — JUMP!" : "ENERGY SHOT — DODGE!";
                warning.gameObject.SetActive(true);
                warning.transform.position = origin;
                animator.SetTrigger("Attack");
                yield return new WaitForSeconds(phase2 ? 0.75f : 0.95f);
                yield return new WaitUntil(() => Ready);
                warning.gameObject.SetActive(false); WarningText = "";
                if (wave)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Spawn(new Vector2(origin.x, 0.55f), Vector2.right * (side * 4.5f));
                        Spawn(new Vector2(origin.x, 1.25f), Vector2.right * (side * 4.5f));
                        yield return new WaitForSeconds(0.2f);
                    }
                }
                else Spawn(origin, aim * (phase2 ? 4.8f : 4));
                yield return new WaitForSeconds(phase2 ? 0.95f : 1.7f);
            }
        }
        private void Spawn(Vector2 position, Vector2 velocity)
        {
            if (!Ready) return;
            projectiles.RemoveAll(p => p == null);
            if (projectiles.Count >= 12) return;
            var shot = Instantiate(projectilePrefab, position, Quaternion.identity);
            shot.Launch(velocity, bodyCollider);
            foreach (var other in projectiles) Physics2D.IgnoreCollision(shot.GetComponent<Collider2D>(), other.GetComponent<Collider2D>());
            projectiles.Add(shot);
            AudioManager.Instance?.Play(GameSound.Shoot);
        }
        private void OnDamaged() { animator.SetTrigger("Damage"); AudioManager.Instance?.Play(GameSound.Damage); }
        private void OnDefeated()
        {
            StopAllCoroutines(); ClearProjectiles();
            WarningText = "DEFEATED"; warning.gameObject.SetActive(false);
            bodyCollider.enabled = false;
            animator.ResetTrigger("Attack"); animator.ResetTrigger("Damage"); animator.SetTrigger("Death");
            StartCoroutine(VictoryAfterAnimation());
        }
        private IEnumerator VictoryAfterAnimation()
        {
            yield return new WaitForSeconds(1.1f);
            if (player == null || player.GetComponent<PlayerHealth>().IsDead) yield break;
            AudioManager.Instance?.PlayVictory(victorySound);
            session.CompleteBossEncounter(health); // Existing YOU WIN and pause architecture.
        }
        private void ClearProjectiles() { foreach (var shot in projectiles) if (shot != null) shot.Disarm(); projectiles.Clear(); }
        private void OnCollisionEnter2D(Collision2D collision) => Contact(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => Contact(collision.collider);
        private void Contact(Collider2D other) { if (!health.IsDead) other.GetComponentInParent<PlayerHealth>()?.TakeDamage(1); }
    }
}
