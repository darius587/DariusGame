using UnityEngine;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    public sealed class ShadowKingHud : MonoBehaviour
    {
        [SerializeField] private ShadowKingHealth health = null;
        [SerializeField] private ShadowKingBoss boss = null;
        [SerializeField] private Image fill = null;
        [SerializeField] private Text hitPoints = null;
        [SerializeField] private Text warning = null;
        private void OnEnable() { if (health != null) health.Changed += Refresh; }
        private void OnDisable() { if (health != null) health.Changed -= Refresh; }
        private void Start() => Refresh();
        private void Refresh()
        {
            if (health == null) return;
            fill.fillAmount = health.CurrentHealth / (float)ShadowKingHealth.FullHealth;
            fill.color = health.Phase == 1 ? new Color32(156, 73, 204, 255) : new Color32(216, 64, 112, 255);
            hitPoints.text = $"{health.CurrentHealth} / 15 HP";
        }
        private void LateUpdate()
        {
            warning.text = boss.WarningText;
            var rect = (RectTransform)transform; var parent = (RectTransform)rect.parent;
            rect.sizeDelta = new Vector2(Mathf.Min(640, parent.rect.width - 48), 120);
            rect.anchoredPosition = new Vector2(0, parent.rect.width < 1450 ? -175 : -24);
        }
    }
}
