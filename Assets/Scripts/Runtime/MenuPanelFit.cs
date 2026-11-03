using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuPanelFit : MonoBehaviour
    {
        private RectTransform rect;
        private RectTransform parent;
        private void Awake() { rect = (RectTransform)transform; parent = rect.parent as RectTransform; }
        private void LateUpdate()
        {
            if (parent == null) return;
            float scale = Mathf.Clamp(Mathf.Min((parent.rect.width - 48) / rect.sizeDelta.x,
                (parent.rect.height - 48) / rect.sizeDelta.y), 0.1f, 1);
            rect.localScale = new Vector3(scale, scale, 1);
        }
    }
}
