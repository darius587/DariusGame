using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class EnemyPerception2D : MonoBehaviour
    {
        [SerializeField] private PlayerHealth player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private Transform sightOrigin = null;
        [SerializeField, Min(0.1f)] private float sightDistance = 7f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 110f;
        [SerializeField] private LayerMask walls;

        private Collider2D playerCollider;
        private ContactFilter2D wallFilter;
        private readonly Plane[] cameraPlanes = new Plane[6];
        private readonly RaycastHit2D[] sightHits = new RaycastHit2D[1];
        private readonly List<RaycastHit2D> movementHits = new List<RaycastHit2D>(8);

        public SpriteRenderer Visual => visual;
        public PlayerHealth Player => player;
        public LayerMask Walls => walls;
        public bool HasLiveTarget => player != null && player.isActiveAndEnabled && !player.IsDead;
        public Vector2 TargetPosition => playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)player.transform.position;
        private Vector2 SightOrigin => sightOrigin != null ? sightOrigin.position : transform.position;

        private void Awake()
        {
            if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
            if (walls.value == 0) walls = LayerMask.GetMask("Walls");
            wallFilter.SetLayerMask(walls);
            wallFilter.useTriggers = false;
        }

        private void Start()
        {
            if (player == null) player = FindAnyObjectByType<PlayerHealth>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (player != null) playerCollider = player.GetComponent<Collider2D>();
        }

        public void Face(int direction)
        {
            if (visual != null) visual.flipX = direction < 0;
        }

        public bool CanSeeTarget(int facing)
        {
            if (!HasLiveTarget) return false;

            Vector2 origin = SightOrigin;
            Vector2 offset = TargetPosition - origin;
            return offset.sqrMagnitude <= sightDistance * sightDistance &&
                Vector2.Angle(Vector2.right * facing, offset) <= fieldOfView * 0.5f &&
                HasClearPath(origin, TargetPosition);
        }

        public bool HasClearPath(Vector2 origin, Vector2 destination)
        {
            if (walls.value == 0) return false;

            Vector2 offset = destination - origin;
            return offset.sqrMagnitude == 0 ||
                Physics2D.Raycast(origin, offset.normalized, wallFilter, sightHits, offset.magnitude) == 0;
        }

        public bool IsOnScreen() => visual != null && visual.sprite != null && IsOnScreen(visual);

        public bool IsOnScreen(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                gameplayCamera == null || !gameplayCamera.isActiveAndEnabled ||
                (gameplayCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                return false;

            GeometryUtility.CalculateFrustumPlanes(gameplayCamera, cameraPlanes);
            return GeometryUtility.TestPlanesAABB(cameraPlanes, renderer.bounds);
        }

        public float ClearTravel(Rigidbody2D body, Vector2 direction, float distance, float clearance)
        {
            if (walls.value == 0 || distance <= 0) return 0;

            int count = body.Cast(direction, wallFilter, movementHits, distance + clearance);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = movementHits[i];
                // Ignore surfaces parallel to travel, such as the floor under a horizontal dash.
                if (Vector2.Dot(hit.normal, direction) < -0.01f)
                    distance = Mathf.Min(distance, Mathf.Max(0, hit.distance - clearance));
            }
            return distance;
        }

        private void OnDrawGizmosSelected()
        {
            Vector2 origin = SightOrigin;
            bool facesLeft = visual != null && visual.flipX;
            float facingAngle = facesLeft ? 180f : 0f;
            Vector3 previous = origin;
            Gizmos.color = Color.yellow;
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (facingAngle + Mathf.Lerp(-fieldOfView * 0.5f, fieldOfView * 0.5f, i / (float)segments)) * Mathf.Deg2Rad;
                Vector3 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * sightDistance;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
            Gizmos.DrawLine(previous, origin);

            if (player == null) return;
            Gizmos.color = Application.isPlaying && CanSeeTarget(facesLeft ? -1 : 1) ? Color.green : Color.red;
            Gizmos.DrawLine(origin, TargetPosition);
        }
    }
}
