using System;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maximumHealth = 3;
        [SerializeField, Min(0)] private float invulnerabilitySeconds = 1;
        private Vector3 spawnPoint;
        private float nextDamageTime;
        private int lastAbyssFrame = -1;
        private SpriteRenderer[] visuals;
        private Color[] originalColors;
        public int CurrentHealth { get; private set; }
        public int MaximumHealth => maximumHealth;
        public bool IsDead => CurrentHealth <= 0;
        public event Action<int> Changed;
        public event Action Died;
        public event Action Damaged;

        private void Awake()
        {
            spawnPoint = transform.position;
            CurrentHealth = maximumHealth;
            visuals = GetComponentsInChildren<SpriteRenderer>();
            originalColors = new Color[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) originalColors[i] = visuals[i].color;
        }

        private void Update()
        {
            bool blink = !IsDead && Time.time < nextDamageTime && ((int)(Time.time * 12) % 2 == 0);
            for (int i = 0; i < visuals.Length; i++)
                if (visuals[i] != null) visuals[i].color = blink ? new Color(originalColors[i].r, originalColors[i].g, originalColors[i].b, 0.35f) : originalColors[i];
        }

        public void SetSpawnPoint(Vector3 position) => spawnPoint = position;

        public void TakeDamage(int amount)
        {
            ApplyDamage(amount, false, true);
        }

        private bool ApplyDamage(int amount, bool bypassInvulnerability, bool damageSound)
        {
            if (IsDead || amount <= 0 || (!bypassInvulnerability && (GameMenuController.IsGameplayBlocked || Time.time < nextDamageTime))) return false;
            nextDamageTime = Time.time + invulnerabilitySeconds;
            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (damageSound) Damaged?.Invoke();
            Changed?.Invoke(CurrentHealth);
            if (CurrentHealth == 0)
            {
                Died?.Invoke();
                var body = GetComponent<Rigidbody2D>();
                if (body != null) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0; }
                GetComponent<PlayerFoundation>()?.SetGameplayInputBlocked(true);
            }
            return true;
        }

        public bool HandleAbyssFall(Action<PlayerHealth> acceptedFall = null)
        {
            if (IsDead || lastAbyssFrame == Time.frameCount) return false;
            lastAbyssFrame = Time.frameCount;
            acceptedFall?.Invoke(this); // Existing fall audio plays before a fatal fall pauses SFX.
            ApplyDamage(1, true, false);
            if (!IsDead) Respawn();
            return true;
        }

        public void Respawn()
        {
            if (IsDead) return;
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0;
                body.position = spawnPoint;
            }
            transform.position = spawnPoint;
            nextDamageTime = Time.time + invulnerabilitySeconds;
            GetComponent<PlayerFoundation>()?.ResetAfterRespawn();
            Physics2D.SyncTransforms();
        }
    }
}
