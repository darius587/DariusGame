using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(CanvasGroup)), DisallowMultipleComponent]
    public sealed class MultiDeviceControlsPanel : MonoBehaviour
    {
        [SerializeField] private PlayerFoundation player = null;
        [SerializeField] private SafeOnScreenStick stick = null;
        [SerializeField] private SafeOnScreenButton jump = null;
        [SerializeField] private SafeOnScreenButton fire = null;
        [SerializeField] private RectTransform leftDock = null;
        [SerializeField] private RectTransform rightDock = null;
        private CanvasGroup group;
        private bool focused = true;
        private bool available;
        private void Awake() => group = GetComponent<CanvasGroup>();
        private void OnEnable() { if (player != null) player.InputReset += ResetControls; }
        private void OnDisable() { if (player != null) player.InputReset -= ResetControls; ResetControls(); }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) ResetControls(); }
        private void OnApplicationPause(bool value) { if (value) ResetControls(); }
        private void LateUpdate()
        {
            bool next = focused && !GameMenuController.IsGameplayBlocked && Time.timeScale > 0;
            if (!next && available) ResetControls();
            available = next;
            group.alpha = next ? 1 : 0; group.interactable = next; group.blocksRaycasts = next;
            float scale = Mathf.Clamp(((RectTransform)transform).rect.width / 1100, 0.55f, 1);
            if (leftDock != null) leftDock.localScale = Vector3.one * scale;
            if (rightDock != null) rightDock.localScale = Vector3.one * scale;
        }
        private void ResetControls()
        {
            if (stick != null) stick.ResetControl();
            if (jump != null) jump.ResetControl();
            if (fire != null) fire.ResetControl();
        }
    }
}
