using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(PlayerHealth))]
    public sealed class PlayerFoundation : MonoBehaviour
    {
        [SerializeField] private Transform groundCheck = null;
        [SerializeField] private Transform projectileOrigin = null;
        [SerializeField] private Animator animator = null;
        [SerializeField] private SpriteRenderer spriteRenderer = null;
        [SerializeField] private BulletProjectile bulletPrefab = null;
        [SerializeField, Min(0)] private float moveSpeed = 6;
        [SerializeField, Min(0)] private float jumpSpeed = 13.5f;
        [SerializeField, Min(0.01f)] private float fireCooldown = 0.3f;
        [SerializeField] private LayerMask groundLayers = ~0;
        private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
        private readonly RaycastHit2D[] muzzleHits = new RaycastHit2D[16];
        private Rigidbody2D body;
        private PlayerHealth health;
        private Collider2D ownCollider;
        private Vector2 moveInput;
        private bool jumpRequested;
        private bool fireHeld;
        private InputAction fireAction;
        private readonly HashSet<InputControl> firePresses = new HashSet<InputControl>();
        private readonly HashSet<InputControl> acceptedFirePresses = new HashSet<InputControl>();
        private EventSystem pointerEventSystem;
        private PointerEventData pointerEvent;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>(16);
        private int facing = 1;
        private float nextFireTime;
        private float muzzleOffset;
        private bool gameplayInputBlocked;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private Vector2 platformVelocity;
        public event Action Jumped;
        public event Action Fired;
        public event Action InputReset;
        public bool IsGrounded { get; private set; }
        public Transform ProjectileOrigin => projectileOrigin;
        public Rigidbody2D Body => body;
        public int Facing => facing;

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, 0.06f);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<PlayerHealth>();
            ownCollider = GetComponent<Collider2D>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            facing = spriteRenderer != null && spriteRenderer.flipX ? -1 : 1;
            muzzleOffset = projectileOrigin != null ? Mathf.Abs(projectileOrigin.localPosition.x) : 0.55f;
        }

        public void OnMove(InputValue value)
        {
            if (!gameplayInputBlocked)
            {
                moveInput = Vector2.ClampMagnitude(value.Get<Vector2>(), 1);
                UpdateFacing();
            }
        }
        public void OnJump(InputValue value)
        {
            if (!gameplayInputBlocked && value.isPressed) jumpRequested = true;
        }
        private bool CanFire => isActiveAndEnabled && applicationFocused && !applicationPaused && !gameplayInputBlocked && !GameMenuController.IsGameplayBlocked && !health.IsDead && Time.timeScale > 0;

        private void RefreshFireInput()
        {
            if (fireAction == null) return;
            bool allowed = CanFire && fireAction.enabled;
            fireHeld = false;
            foreach (var control in fireAction.controls)
            {
                bool pressed = control is ButtonControl button && button.isPressed && !SafeOnScreenButton.IsReleased(control);
                if (!pressed) { firePresses.Remove(control); acceptedFirePresses.Remove(control); continue; }
                var mouse = control.device as Mouse;
                bool overUi = mouse != null && IsMouseOverInteractiveUi(mouse);
                if (firePresses.Add(control) && allowed && !overUi) acceptedFirePresses.Add(control);
                if (!allowed || overUi) acceptedFirePresses.Remove(control);
                if (allowed && acceptedFirePresses.Contains(control)) fireHeld = true;
            }
            if (fireHeld && Time.time >= nextFireTime) Fire();
        }
        private void OnEnable()
        {
            var input = GetComponent<PlayerInput>();
            if (input == null || input.actions == null) return;
            fireAction = input.actions.FindAction("Player/Fire", true);
            ClearFireInput(); // A key held across scene changes/re-enabling needs a fresh press.
            // Use this player's action instance, including virtual OnScreenButton devices.
            // started does not shoot: a Button must reach its performed press threshold.
            fireAction.canceled += OnFireCanceled;
            InputSystem.onAfterUpdate += RefreshFireInput;
        }
        private void OnFireCanceled(InputAction.CallbackContext context)
        {
            fireHeld = false;
            if (!fireAction.enabled) ClearFireInput();
        }
        private void Update()
        {
            RefreshFireInput();
        }
        private bool IsMouseOverInteractiveUi(Mouse mouse)
        {
            var system = EventSystem.current;
            if (system == null || !system.isActiveAndEnabled) return false;
            if (pointerEventSystem != system)
            {
                pointerEventSystem = system;
                pointerEvent = new PointerEventData(system);
            }
            pointerEvent.Reset();
            pointerEvent.position = mouse.position.ReadValue();
            uiHits.Clear();
            system.RaycastAll(pointerEvent, uiHits);
            bool overUi = false;
            foreach (var hit in uiHits)
            {
                if (!(hit.module is GraphicRaycaster) || hit.gameObject == null) continue;
                var go = hit.gameObject;
                if (go.GetComponentInParent<Selectable>() != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null ||
                    ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null ||
                    ExecuteEvents.GetEventHandler<IDragHandler>(go) != null ||
                    ExecuteEvents.GetEventHandler<IScrollHandler>(go) != null)
                { overUi = true; break; }
            }
            uiHits.Clear();
            return overUi;
        }
        private void ClearFireInput()
        {
            fireHeld = false;
            acceptedFirePresses.Clear();
            firePresses.Clear();
            if (fireAction != null) foreach (var control in fireAction.controls)
                if (control is ButtonControl button && button.isPressed) firePresses.Add(control);
        }
        public void SetGameplayInputBlocked(bool blocked)
        {
            gameplayInputBlocked = blocked;
            ClearInput();
        }
        public void ResetAfterRespawn()
        {
            ClearInput();
            IsGrounded = false;
            platformVelocity = Vector2.zero;
        }

        private void ClearInput()
        {
            moveInput = Vector2.zero;
            jumpRequested = false;
            ClearFireInput();
            InputReset?.Invoke();
        }
        private void OnDisable()
        {
            if (fireAction != null)
            {
                fireAction.canceled -= OnFireCanceled;
            }
            fireAction = null;
            InputSystem.onAfterUpdate -= RefreshFireInput;
            ClearInput();
        }
        private void OnApplicationFocus(bool focused) { applicationFocused = focused; if (!focused) ClearInput(); }
        private void OnApplicationPause(bool paused) { applicationPaused = paused; if (paused) ClearInput(); }
        public void OnDeviceLost(PlayerInput input) => ClearInput();
        public void OnControlsChanged(PlayerInput input) => ClearInput();

        private void FixedUpdate()
        {
            if (gameplayInputBlocked || health.IsDead) return;
            UpdateGrounded();
            Vector2 velocity = body.linearVelocity;
            velocity.x = moveInput.x * moveSpeed + platformVelocity.x;
            if (IsGrounded) velocity.y = platformVelocity.y;
            if (jumpRequested && IsGrounded)
            {
                velocity.y = jumpSpeed + Mathf.Max(0, platformVelocity.y);
                IsGrounded = false;
                Jumped?.Invoke();
            }
            jumpRequested = false;
            body.linearVelocity = velocity;

            UpdateFacing();

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat("Speed", Mathf.Abs(velocity.x));
                animator.SetBool("Grounded", IsGrounded);
                animator.SetFloat("VerticalSpeed", velocity.y);
            }
        }

        private void UpdateGrounded()
        {
            IsGrounded = false;
            platformVelocity = Vector2.zero;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayers);
            filter.useTriggers = false;
            int count = ownCollider.Cast(Vector2.down, filter, groundHits, 0.06f);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = groundHits[i].collider;
                var platform = hit != null && hit.attachedRigidbody != null ? hit.attachedRigidbody.GetComponent<MovingPlatform>() : null;
                Vector2 supportVelocity = platform != null ? platform.StepVelocity : Vector2.zero;
                if (hit != null && hit.attachedRigidbody != body && groundHits[i].normal.y >= 0.65f &&
                    hit.GetComponent<BulletProjectile>() == null && body.linearVelocity.y - supportVelocity.y <= 0.2f)
                {
                    IsGrounded = true;
                    platformVelocity = supportVelocity;
                    return;
                }
            }
        }

        private void Fire()
        {
            if (bulletPrefab == null) return;
            UpdateFacing();
            nextFireTime = Time.time + fireCooldown;
            Vector2 origin = projectileOrigin != null ? projectileOrigin.position : transform.position;
            float radius = bulletPrefab.CollisionRadius;
            float edge = facing > 0 ? ownCollider.bounds.max.x : ownCollider.bounds.min.x;
            origin.x = facing > 0 ? Mathf.Max(origin.x, edge + radius + 0.06f) :
                Mathf.Min(origin.x, edge - radius - 0.06f);

            Vector2 start = new Vector2(ownCollider.bounds.center.x, origin.y);
            Vector2 travel = origin - start;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(bulletPrefab.gameObject.layer));
            filter.useTriggers = false;
            int count = Physics2D.CircleCast(start, radius, travel.normalized, filter, muzzleHits, travel.magnitude);
            for (int i = 0; i < count; i++)
            {
                var hit = muzzleHits[i].collider;
                if (hit != null && hit.attachedRigidbody != body && !hit.transform.IsChildOf(transform)) return;
            }
            var bullet = Instantiate(bulletPrefab, origin, Quaternion.identity);
            bullet.Launch(facing, gameObject);
            Fired?.Invoke();
        }
        private void UpdateFacing()
        {
            if (Mathf.Abs(moveInput.x) > 0.01f) facing = moveInput.x < 0 ? -1 : 1;
            if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
            if (projectileOrigin != null)
            {
                Vector3 position = projectileOrigin.localPosition;
                position.x = muzzleOffset * facing;
                projectileOrigin.localPosition = position;
            }
        }
    }
}
