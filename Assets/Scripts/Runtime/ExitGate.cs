using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ExitGate : MonoBehaviour
    {
        private LevelSession session;
        private SpriteRenderer visual;
        private Color unlockedColor;
        private void Start()
        {
            session = FindAnyObjectByType<LevelSession>();
            visual = GetComponent<SpriteRenderer>();
            if (visual != null) unlockedColor = visual.color;
        }
        private void Update()
        {
            if (visual != null && session != null) visual.color = session.GateUnlocked ? unlockedColor : new Color(0.45f, 0.45f, 0.55f);
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            TryFinish(other);
        }
        private void OnTriggerStay2D(Collider2D other) => TryFinish(other);
        private void TryFinish(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerFoundation>() == null) return;
            var health = other.GetComponentInParent<PlayerHealth>();
            if (session != null && health != null && !health.IsDead && !GameMenuController.IsGameplayBlocked) session.Complete();
        }
    }
}
