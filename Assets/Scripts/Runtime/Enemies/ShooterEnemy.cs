using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyPerception2D), typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ShooterEnemy : MonoBehaviour
    {
        private enum State { Scan, KeepDistance, WindUp, Fire, Cooldown }

        [SerializeField] private Transform waypointA = null;
        [SerializeField] private Transform waypointB = null;
        [SerializeField] private LineRenderer aimWarning = null;
        [SerializeField] private EnemyProjectile projectilePrefab = null;
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.55f, 0.2f);
        [SerializeField, Min(0.1f)] private float scanTurnSeconds = 1f;
        [SerializeField, Min(0.1f)] private float preferredDistance = 3f;
        [SerializeField, Min(0.1f)] private float retreatSpeed = 2f;
        [SerializeField, Min(0)] private float wallClearance = 0.02f;
        [SerializeField, Range(0.3f, 0.8f)] private float windUpSeconds = 0.6f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 1.4f;

        private Rigidbody2D body;
        private EnemyPerception2D perception;
        private State state;
        private float stateTimer;
        private int windUpFrame;
        private int facing = 1;
        private Vector2 Muzzle => body.position + new Vector2(muzzleOffset.x * facing, muzzleOffset.y);

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            perception = GetComponent<EnemyPerception2D>();
            if (aimWarning != null)
            {
                aimWarning.useWorldSpace = true;
                aimWarning.positionCount = 2;
                aimWarning.enabled = false;
            }
        }

        private void Start()
        {
            perception.Face(facing);
            SetState(State.Scan);
        }

        private void OnDisable()
        {
            if (state == State.WindUp || state == State.Fire) SetState(State.Cooldown);
        }

        private void FixedUpdate()
        {
            if (GameMenuController.IsGameplayBlocked) return;

            stateTimer -= Time.fixedDeltaTime;
            switch (state)
            {
                case State.Scan:
                    if (perception.CanSeeTarget(facing))
                        SetState(State.KeepDistance);
                    else if (stateTimer <= 0)
                    {
                        facing = -facing;
                        perception.Face(facing);
                        SetState(State.Scan);
                    }
                    break;
                case State.KeepDistance:
                    if (!perception.CanSeeTarget(facing))
                    {
                        SetState(State.Scan);
                        break;
                    }

                    facing = perception.TargetPosition.x < body.position.x ? -1 : 1;
                    perception.Face(facing);
                    if (!TryRetreat() && CanAim()) SetState(State.WindUp);
                    break;
                case State.WindUp:
                    if (!CanAim())
                    {
                        SetState(State.Cooldown);
                        break;
                    }

                    UpdateAimWarning();
                    if (!perception.IsOnScreen(aimWarning)) SetState(State.Cooldown);
                    else if (stateTimer <= 0 && Time.frameCount != windUpFrame)
                    {
                        SetState(State.Fire);
                        goto case State.Fire;
                    }
                    break;
                case State.Fire:
                    if (CanAim() && perception.IsOnScreen(aimWarning))
                    {
                        Vector2 origin = Muzzle;
                        Vector2 direction = perception.TargetPosition - origin;
                        Vector3 spawn = new Vector3(origin.x, origin.y, transform.position.z);
                        EnemyProjectile shot = Instantiate(projectilePrefab, spawn, Quaternion.identity);
                        shot.Launch(direction, perception.Walls, perception.Player);
                        AudioManager.Instance?.Play(GameSound.Shoot);
                    }
                    SetState(State.Cooldown);
                    break;
                case State.Cooldown:
                    if (stateTimer <= 0) SetState(State.Scan);
                    break;
            }
        }

        private void SetState(State next)
        {
            state = next;
            stateTimer = next switch
            {
                State.Scan => Mathf.Max(0.1f, scanTurnSeconds),
                State.WindUp => Mathf.Clamp(windUpSeconds, 0.3f, 0.8f),
                State.Cooldown => Mathf.Max(0.1f, cooldownSeconds),
                _ => 0f
            };
            if (next == State.WindUp)
            {
                windUpFrame = Time.frameCount;
                UpdateAimWarning();
            }
            if (aimWarning != null) aimWarning.enabled = next == State.WindUp || next == State.Fire;
        }

        private bool CanAim()
        {
            if (projectilePrefab == null || aimWarning == null || !aimWarning.gameObject.activeInHierarchy ||
                aimWarning.sharedMaterial == null || aimWarning.widthMultiplier <= 0 ||
                !perception.IsOnScreen() || !perception.CanSeeTarget(facing))
                return false;

            Vector2 origin = Muzzle;
            Vector2 target = perception.TargetPosition;
            return (target - origin).sqrMagnitude > 0.0001f &&
                perception.HasClearPath(body.position, origin) && perception.HasClearPath(origin, target);
        }

        private void UpdateAimWarning()
        {
            Vector2 origin = Muzzle;
            Vector2 target = perception.TargetPosition;
            float z = aimWarning.transform.position.z;
            aimWarning.SetPosition(0, new Vector3(origin.x, origin.y, z));
            aimWarning.SetPosition(1, new Vector3(target.x, target.y, z));
        }

        private bool TryRetreat()
        {
            if (waypointA == null || waypointB == null) return false;

            float separation = Mathf.Abs(perception.TargetPosition.x - body.position.x);
            float gap = preferredDistance - separation;
            if (gap <= 0.01f) return false;

            float left = Mathf.Min(waypointA.position.x, waypointB.position.x);
            float right = Mathf.Max(waypointA.position.x, waypointB.position.x);
            float room = facing > 0 ? body.position.x - left : right - body.position.x;
            float distance = Mathf.Min(Mathf.Min(room, gap), retreatSpeed * Time.fixedDeltaTime);
            Vector2 direction = Vector2.left * facing;
            float travel = perception.ClearTravel(body, direction, distance, wallClearance);
            if (travel <= 0.0001f) return false;

            body.MovePosition(body.position + direction * travel);
            return true;
        }
    }
}
