using UnityEngine;

namespace Zhuzbai.Platformer
{
    [RequireComponent(typeof(EnemyHealth), typeof(EnemyPerception2D), typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ChargerEnemy : MonoBehaviour, IEnemyStatsReceiver
    {
        [SerializeField] private EnemyStats stats = null;
        public void ApplyStats(EnemyStats data) { if (data != null) stats = data; }
        private enum State { Patrol, WindUp, Charge, Recover }

        [SerializeField] private Transform waypointA = null;
        [SerializeField] private Transform waypointB = null;
        private float patrolSpeed => stats.chargerEnemy_patrolSpeed;
        private float chargeSpeed => stats.chargerEnemy_chargeSpeed;
        private float chargeRange => stats.chargerEnemy_chargeRange;
        private float chargeSeconds => stats.chargerEnemy_chargeSeconds;
        private float maximumHeightDifference => stats.chargerEnemy_maximumHeightDifference;
        private float wallClearance => stats.chargerEnemy_wallClearance;
        private int damage => stats.chargerEnemy_damage;
        private float windUpSeconds => stats.chargerEnemy_windUpSeconds;
        private float recoverySeconds => stats.chargerEnemy_recoverySeconds;
        [SerializeField] private Color warningColor = new Color(1f, 0.25f, 0.3f);
        [SerializeField] private LineRenderer dashDirectionIndicator = null;

        private Rigidbody2D body;
        private EnemyPerception2D perception;
        private Color normalColor;
        private State state;
        private float stateTimer;
        private int windUpFrame;
        private int facing = 1;
        private int chargeDirection;
        private bool towardB = true;
        private bool hitDuringCharge;
        private bool dashMoving;
        private bool HasLane => waypointA != null && waypointB != null;

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
            if (state == State.WindUp || state == State.Charge) SetState(State.Recover);
        }

        private void FixedUpdate()
        {
            if (GameMenuController.IsGameplayBlocked) return;

            switch (state)
            {
                case State.Patrol:
                    if (CanStartCharge()) SetState(State.WindUp);
                    else Patrol();
                    break;
                case State.WindUp:
                    stateTimer -= Time.fixedDeltaTime;
                    if (!perception.HasLiveTarget || !perception.IsOnScreen()) SetState(State.Recover);
                    else if (stateTimer <= 0 && Time.frameCount != windUpFrame)
                    {
                        SetState(State.Charge);
                        goto case State.Charge;
                    }
                    break;
                case State.Charge:
                    if (stateTimer <= 0 || !perception.HasLiveTarget || !perception.IsOnScreen() || !MoveCharge())
                        SetState(State.Recover);
                    else
                        stateTimer -= Time.fixedDeltaTime;
                    break;
                case State.Recover:
                    stateTimer -= Time.fixedDeltaTime;
                    if (stateTimer <= 0) SetState(State.Patrol);
                    break;
            }
        }

        private void SetState(State next)
        {
            state = next;
            stateTimer = next switch
            {
                State.WindUp => Mathf.Clamp(windUpSeconds, 0.3f, 0.8f),
                State.Charge => Mathf.Max(0.1f, chargeSeconds),
                State.Recover => Mathf.Max(0.1f, recoverySeconds),
                _ => 0f
            };
            dashMoving = false;
            if (next == State.WindUp)
            {
                chargeDirection = facing;
                windUpFrame = Time.frameCount;
                hitDuringCharge = false;
            }
            if (dashDirectionIndicator != null)
            {
                dashDirectionIndicator.enabled = next == State.WindUp;
                if (next == State.WindUp)
                {
                    Vector3 scale = dashDirectionIndicator.transform.localScale;
                    scale.x = Mathf.Abs(scale.x) * chargeDirection;
                    dashDirectionIndicator.transform.localScale = scale;
                }
            }
            if (perception.Visual != null)
                perception.Visual.color = next == State.WindUp ? warningColor : normalColor;
        }

        private bool CanStartCharge()
        {
            if (!HasLane || !perception.IsOnScreen() || !perception.CanSeeTarget(facing)) return false;

            Vector2 offset = perception.TargetPosition - body.position;
            return offset.x * facing > 0 && Mathf.Abs(offset.x) <= chargeRange &&
                Mathf.Abs(offset.y) <= maximumHeightDifference && ChargeTravel(facing, Time.fixedDeltaTime) > 0;
        }

        private void Patrol()
        {
            if (!HasLane) return;

            float targetX = towardB ? waypointB.position.x : waypointA.position.x;
            if (Mathf.Abs(targetX - body.position.x) <= 0.01f)
            {
                towardB = !towardB;
                targetX = towardB ? waypointB.position.x : waypointA.position.x;
            }
            float offset = targetX - body.position.x;
            if (Mathf.Abs(offset) <= 0.01f) return;

            facing = offset < 0 ? -1 : 1;
            perception.Face(facing);
            Vector2 direction = Vector2.right * facing;
            float distance = Mathf.Min(Mathf.Abs(offset), patrolSpeed * Time.fixedDeltaTime);
            float travel = perception.ClearTravel(body, direction, distance, wallClearance);
            if (travel > 0) body.MovePosition(body.position + direction * travel);
        }

        private float ChargeTravel(int direction, float stepSeconds)
        {
            if (!HasLane) return 0;

            float left = Mathf.Min(waypointA.position.x, waypointB.position.x);
            float right = Mathf.Max(waypointA.position.x, waypointB.position.x);
            float room = direction > 0 ? right - body.position.x : body.position.x - left;
            float distance = Mathf.Min(room, chargeSpeed * stepSeconds);
            return perception.ClearTravel(body, Vector2.right * direction, distance, wallClearance);
        }

        private bool MoveCharge()
        {
            float travel = ChargeTravel(chargeDirection, Mathf.Min(stateTimer, Time.fixedDeltaTime));
            if (travel <= 0) return false;

            dashMoving = true;
            body.MovePosition(body.position + Vector2.right * (chargeDirection * travel));
            return true;
        }

        private void OnCollisionEnter2D(Collision2D collision) => TryHitPlayer(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => TryHitPlayer(collision.collider);

        private void TryHitPlayer(Collider2D other)
        {
            if (!isActiveAndEnabled || state != State.Charge || !dashMoving || hitDuringCharge || GameMenuController.IsGameplayBlocked ||
                !perception.HasLiveTarget || !perception.IsOnScreen())
                return;

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != perception.Player || (perception.TargetPosition.x - body.position.x) * chargeDirection < 0) return;

            hitDuringCharge = true;
            player.TakeDamage(damage);
        }
    }
}
