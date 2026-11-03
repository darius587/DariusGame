using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;

namespace Zhuzbai.Platformer
{
    public sealed class SafeOnScreenStick : OnScreenStick, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private int? pointer;
        public new void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue || data == null || data.button != PointerEventData.InputButton.Left || GameMenuController.IsGameplayBlocked || Time.timeScale <= 0) return;
            pointer = data.pointerId; base.OnPointerDown(data);
        }
        public new void OnDrag(PointerEventData data)
        {
            if (data == null || pointer != data.pointerId) return;
            if (GameMenuController.IsGameplayBlocked || Time.timeScale <= 0) { ResetControl(); return; }
            base.OnDrag(data);
        }
        public new void OnPointerUp(PointerEventData data) { if (data != null && pointer == data.pointerId) ResetControl(); }
        public void ResetControl()
        {
            pointer = null;
            if (control != null && control.device.added) base.OnPointerUp(null);
            else ((RectTransform)transform).anchoredPosition = Vector2.zero;
        }
        protected override void OnDisable() { ResetControl(); base.OnDisable(); }
    }
}
