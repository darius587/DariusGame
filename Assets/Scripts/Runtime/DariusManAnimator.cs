using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
    public sealed class DariusManAnimator : MonoBehaviour
    {
        private PlayerFoundation player;
        private Animator animator;
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        private void OnEnable()
        {
            animator = GetComponent<Animator>();
            player = GetComponentInParent<PlayerFoundation>();
            if (player != null) player.Fired += OnShot;
        }
        private void OnDisable() { if (player != null) player.Fired -= OnShot; }
        private void OnShot()
        {
            if (animator.runtimeAnimatorController != null) animator.SetTrigger(Shoot);
        }
    }
}
