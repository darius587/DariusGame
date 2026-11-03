using UnityEngine;

namespace Zhuzbai.Platformer
{
    public sealed class GameplayAudioBridge : MonoBehaviour
    {
        [SerializeField] private PlayerFoundation player = null;
        [SerializeField] private PlayerHealth health = null;
        [SerializeField] private CoinCollectible[] coins = System.Array.Empty<CoinCollectible>();
        [SerializeField] private FallHazard[] fallZones = System.Array.Empty<FallHazard>();
        private void OnEnable()
        {
            if (player != null) { player.Jumped += OnJump; player.Fired += OnShot; }
            if (health != null) { health.Damaged += OnDamage; health.Died += StopWalking; }
            foreach (var coin in coins) if (coin != null) coin.Collected += OnCoin;
            foreach (var zone in fallZones) if (zone != null) zone.PlayerFell += OnFall;
        }
        private void OnDisable()
        {
            if (player != null) { player.Jumped -= OnJump; player.Fired -= OnShot; }
            if (health != null) { health.Damaged -= OnDamage; health.Died -= StopWalking; }
            foreach (var coin in coins) if (coin != null) coin.Collected -= OnCoin;
            foreach (var zone in fallZones) if (zone != null) zone.PlayerFell -= OnFall;
            StopWalking();
        }
        private void Update()
        {
            bool walking = player != null && player.isActiveAndEnabled && player.Body != null &&
                player.IsGrounded && Mathf.Abs(player.Body.linearVelocity.x) > 0.15f &&
                Mathf.Abs(player.Body.linearVelocity.y) < 0.1f && health != null && health.CurrentHealth > 0 &&
                !GameMenuController.IsGameplayBlocked && Time.timeScale > 0;
            AudioManager.Instance?.SetFootsteps(walking);
        }
        private void StopWalking() => AudioManager.Instance?.SetFootsteps(false);
        private void OnJump() { StopWalking(); AudioManager.Instance?.Play(GameSound.Jump); }
        private void OnShot() => AudioManager.Instance?.Play(GameSound.Shoot);
        private void OnDamage() { StopWalking(); AudioManager.Instance?.Play(GameSound.Damage, health != null && health.IsDead); }
        private void OnCoin() => AudioManager.Instance?.Play(GameSound.Coin);
        private void OnFall(PlayerHealth fallen)
        {
            if (fallen != health) return;
            StopWalking();
            AudioManager.Instance?.Play(GameSound.Fall, true);
        }
    }
}
