using UnityEngine;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    public sealed class FoundationHud : MonoBehaviour
    {
        [SerializeField] private Text status = null;
        [SerializeField] private LevelSession session = null;
        [SerializeField] private PlayerHealth health = null;
        [SerializeField] private PixelNumberDisplay healthCounter = null;
        [SerializeField] private PixelNumberDisplay coinCounter = null;
        [SerializeField] private GameObject completionBanner = null;
        [SerializeField] private Text levelIndicator = null;
        [SerializeField] private Image levelBadge = null;
        [SerializeField] private Sprite[] levelBadges = System.Array.Empty<Sprite>();
        private void OnEnable()
        {
            if (session != null) session.Changed += Refresh;
            if (health != null) health.Changed += OnHealthChanged;
        }
        private void Start() => Refresh();
        private void OnDisable()
        {
            if (session != null) session.Changed -= Refresh;
            if (health != null) health.Changed -= OnHealthChanged;
        }
        private void OnHealthChanged(int value) => Refresh();
        private void Refresh()
        {
            if (session == null || health == null) return;
            if (healthCounter != null) healthCounter.SetValue($"{health.CurrentHealth}/{health.MaximumHealth}");
            if (coinCounter != null) coinCounter.SetValue($"{session.Coins:00}/{session.TotalCoins:00}");
            if (levelIndicator != null && session.Definition != null) levelIndicator.text = "LEVEL " + session.Definition.LevelNumber;
            if (levelBadge != null && session.Definition != null && session.Definition.LevelNumber >= 1 && session.Definition.LevelNumber <= levelBadges.Length)
                levelBadge.sprite = levelBadges[session.Definition.LevelNumber - 1];
            if (levelBadge != null && session.Definition != null)
            {
                bool hasBadge = session.Definition.LevelNumber <= levelBadges.Length;
                levelBadge.enabled = hasBadge;
                if (levelIndicator != null) levelIndicator.enabled = !hasBadge;
            }
            if (completionBanner != null) completionBanner.SetActive(session.Completed && levelIndicator == null);
            if (status != null && healthCounter == null)
                status.text = session.Completed ? "LEVEL COMPLETE!" :
                    $"COINS  {session.Coins:00} / {session.TotalCoins:00}     HEALTH  {health.CurrentHealth} / {health.MaximumHealth}";
        }
    }
}
