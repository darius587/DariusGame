using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    public sealed class SafeOnScreenButton : OnScreenButton, IPointerDownHandler, IPointerUpHandler
    {
        private int? pointer;
        private ButtonControl sourcePress;
        private Image background;
        private Color idleColor;
        private static readonly Dictionary<InputControl, SafeOnScreenButton> Owners = new Dictionary<InputControl, SafeOnScreenButton>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwners() => Owners.Clear();
        protected override void OnEnable()
        {
            background = GetComponent<Image>();
            if (background != null) idleColor = background.color;
            SetPressedAppearance(false);
            base.OnEnable(); if (control != null) Owners[control] = this;
        }
        private void SetPressedAppearance(bool pressed)
        {
            if (background == null) return;
            var color = idleColor;
            if (pressed) color.a = Mathf.Min(1, color.a + 0.1f);
            background.color = color;
        }
        public static bool IsReleased(InputControl value) => Owners.TryGetValue(value, out var owner) &&
            (!owner.pointer.HasValue || (owner.sourcePress != null && !owner.sourcePress.isPressed));
        public new void OnPointerDown(PointerEventData data)
        {
            if (pointer.HasValue || data == null || data.button != PointerEventData.InputButton.Left || GameMenuController.IsGameplayBlocked || Time.timeScale <= 0) return;
            pointer = data.pointerId;
            if (data is ExtendedPointerEventData extended)
            {
                if (extended.device is Mouse mouse) sourcePress = mouse.leftButton;
                else if (extended.device is Pen pen) sourcePress = pen.tip;
                else if (extended.device is Touchscreen screen)
                    foreach (var touch in screen.touches) if (touch.touchId.ReadValue() == extended.touchId) { sourcePress = touch.press; break; }
            }
            base.OnPointerDown(data);
            SetPressedAppearance(true);
        }
        public new void OnPointerUp(PointerEventData data) { if (data != null && pointer == data.pointerId) ResetControl(); }
        public void ResetControl()
        {
            pointer = null; sourcePress = null; SetPressedAppearance(false);
            if (control != null && control.device.added) base.OnPointerUp(null);
        }
        protected override void OnDisable() { if (control != null) Owners.Remove(control); ResetControl(); base.OnDisable(); }
    }
}
