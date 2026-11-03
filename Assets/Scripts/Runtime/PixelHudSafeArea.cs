using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class PixelHudSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform counters = null;
        [SerializeField, Min(0.1f)] private float preferredScale = 1;
        private RectTransform panel;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private void OnEnable()
        {
            panel = (RectTransform)transform;
            lastScreen = Vector2Int.zero;
            ApplySafeArea();
        }
        private void LateUpdate() => ApplySafeArea();
        private void ApplySafeArea()
        {
            if (panel == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (area != lastSafeArea || size != lastScreen)
            {
                Vector2 min = area.min;
                Vector2 max = area.max;
                panel.anchorMin = new Vector2(min.x / size.x, min.y / size.y);
                panel.anchorMax = new Vector2(max.x / size.x, max.y / size.y);
                panel.offsetMin = panel.offsetMax = Vector2.zero;
                lastSafeArea = area;
                lastScreen = size;
            }
            if (counters != null)
            {
                float scale = Mathf.Min(preferredScale, Mathf.Max(0.1f, (panel.rect.width - 48) / 364));
                counters.localScale = new Vector3(scale, scale, 1);
            }
        }
    }
}
