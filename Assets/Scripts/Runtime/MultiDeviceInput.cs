using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace Zhuzbai.Platformer
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(PlayerInput))]
    public sealed class MultiDeviceInput : MonoBehaviour
    {
        private PlayerInput input;
        private void Awake() { input = GetComponent<PlayerInput>(); input.neverAutoSwitchControlSchemes = true; }
        private void OnEnable() => InputSystem.onDeviceChange += OnDeviceChange;
        private void Start() => PairAvailableDevices();
        private void OnDisable() => InputSystem.onDeviceChange -= OnDeviceChange;
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled) Pair(device);
        }
        private void PairAvailableDevices() { foreach (var device in InputSystem.devices) Pair(device); }
        private void Pair(InputDevice device)
        {
            if (input == null || !input.user.valid || !(device is Keyboard || device is Mouse || device is Gamepad || device is Touchscreen || device is Pen)) return;
            foreach (var paired in input.user.pairedDevices) if (paired == device) return;
            InputUser.PerformPairingWithDevice(device, input.user);
        }
    }
}
