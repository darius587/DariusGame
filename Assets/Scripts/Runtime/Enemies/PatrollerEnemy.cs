using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyPerception2D), typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PatrollerEnemy : MonoBehaviour
    {
        private enum State { Patrol, Chase, WindUp, Strike, Cooldown }

        [SerializeField] private Transform waypointA = null;
        [SerializeField] private Transform waypointB = null;
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1.4f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float strikeRange = 1.25f;
        [SerializeField, Min(0)] private float wallClearance = 0.02f;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Range(0.3f, 0.8f)] private float windUpSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float cooldownSeconds = 1f;
        [SerializeField] private Color warningColor = new Color(1f, 0.65f, 0.2f);

        private Rigidbody2D body;
        private EnemyPerception2D perception;
        private Color normalColor;
        private State state;
        private float stateTimer;
        private int windUpFrame;
        private int facing = 1;
        private bool towardB = true;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            perception = GetComponent<EnemyPerception2D>();
        }

        private void Start()
        {
            if (perception.Visual != null) normalColor = perception.Visual.color;
            perception.Face(facing);
            SetState(State.Patrol);
        }

        private void OnDisable()
        {
            if (state == State.WindUp || state == State.Strike) SetState(State.Cooldown);
        }

        private void FixedUpdate()
        {
            if (GameMenuController.IsGameplayBlocked) return;

            stateTimer -= Time.fixedDeltaTime;
            switch (state)
            {
                case State.Patrol:
                    if (perception.CanSeeTarget(facing)) SetState(State.Chase);
                    else Patrol();
                    break;
                case State.Chase:
                    if (!perception.CanSeeTarget(facing)) SetState(State.Patrol);
                    else if (CanStrike()) SetState(State.WindUp);
                    else Chase();
                    break;
                case State.WindUp:
                    if (!perception.HasLiveTarget || !perception.IsOnScreen()) SetState(State.Cooldown);
                    else if (stateTimer <= 0 && Time.frameCount != windUpFrame)
                    {
                        SetState(State.Strike);
                        goto case State.Strike;
                    }
                    break;
                case State.Strike:
                    if (CanStrike()) perception.Player.TakeDamage(damage);
                    SetState(State.Cooldown);
                    break;
                case State.Cooldown:
                    if (stateTimer <= 0) SetState(State.Patrol);
                    break;
            }
        }

        private void SetState(State next)
        {
            state = next;
            stateTimer = 0;
            if (next == State.WindUp)
            {
                stateTimer = Mathf.Clamp(windUpSeconds, 0.3f, 0.8f);
                windUpFrame = Time.frameCount;
            }
            else if (next == State.Cooldown)
                stateTimer = Mathf.Max(0.1f, cooldownSeconds);

            if (perception.Visual != null)
                perception.Visual.color = next == State.WindUp || next == State.Strike ? warningColor : normalColor;
        }

        private bool CanStrike()
        {
            if (!perception.HasLiveTarget || !perception.IsOnScreen()) return false;

            Vector2 target = perception.TargetPosition;
            Vector2 offset = target - body.position;
            return offset.x * facing >= 0 && offset.sqrMagnitude <= strikeRange * strikeRange &&
                perception.HasClearPath(body.position, target);
        }

        private void Patrol()
        {
            if (waypointA == null || waypointB == null) return;

            Vector2 destination = towardB ? waypointB.position : waypointA.position;
            if ((destination - body.position).sqrMagnitude <= 0.0001f)
            {
                towardB = !towardB;
                destination = towardB ? waypointB.position : waypointA.position;
            }
            MoveToward(destination, patrolSpeed);
        }

        private void Chase()
        {
            if (waypointA == null || waypointB == null) return;

            float left = Mathf.Min(waypointA.position.x, waypointB.position.x);
            float right = Mathf.Max(waypointA.position.x, waypointB.position.x);
            float x = Mathf.Clamp(perception.TargetPosition.x, left, right);
            MoveToward(new Vector2(x, body.position.y), chaseSpeed);
        }

        private void MoveToward(Vector2 destination, float speed)
        {
            Vector2 offset = destination - body.position;
            if (offset.sqrMagnitude <= 0.0001f) return;

            if (offset.x != 0)
            {
                facing = offset.x < 0 ? -1 : 1;
                perception.Face(facing);
            }
            Vector2 direction = offset.normalized;
            float distance = Mathf.Min(offset.magnitude, speed * Time.fixedDeltaTime);
            float travel = perception.ClearTravel(body, direction, distance, wallClearance);
            if (travel > 0) body.MovePosition(body.position + direction * travel);
        }
    }
}
