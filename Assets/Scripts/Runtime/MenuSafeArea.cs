using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuSafeArea : MonoBehaviour
    {
        [SerializeField] private RectTransform cornerButtons = null;
        private RectTransform rect;
        private Rect previous;
        private Vector2Int previousSize;
        private void Awake() => rect = (RectTransform)transform;
        private void LateUpdate()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size.x <= 0 || size.y <= 0) return;
            Rect safe = Screen.safeArea;
            if (safe == previous && size == previousSize) return;
            rect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (cornerButtons != null)
            {
                var position = cornerButtons.anchoredPosition;
                position.y = rect.rect.width < 760 ? -124 : -24;
                cornerButtons.anchoredPosition = position;
            }
            previous = safe;
            previousSize = size;
        }
    }
}
