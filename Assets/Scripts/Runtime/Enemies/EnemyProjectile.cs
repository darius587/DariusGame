using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    [DisallowMultipleComponent]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private EnemyStats stats = null;
        private int damage => stats.enemyProjectile_damage;
        private float speed => stats.enemyProjectile_speed;
        private float lifetime => stats.enemyProjectile_lifetime;
        private float collisionRadius => stats.enemyProjectile_collisionRadius;

        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        private ContactFilter2D hitFilter;
        private LayerMask walls;
        private PlayerHealth target;
        private Vector2 direction;
        private bool flying;

        private void Awake() => Destroy(gameObject, Mathf.Max(0.1f, lifetime));

        public void Launch(Vector2 aim, LayerMask wallMask, PlayerHealth player)
        {
            if (flying || aim.sqrMagnitude == 0 || wallMask.value == 0 || player == null) return;

            walls = wallMask;
            target = player;
            direction = aim.normalized;
            hitFilter.SetLayerMask(walls.value | (1 << player.gameObject.layer));
            hitFilter.useTriggers = false;
            flying = true;
        }

        private void FixedUpdate()
        {
            if (!flying || GameMenuController.IsGameplayBlocked) return;

            // Sweep the whole step so a fast shot cannot pass through a thin wall or player.
            float distance = speed * Time.fixedDeltaTime;
            int count = Physics2D.CircleCast(transform.position, collisionRadius, direction, hitFilter, hits, distance);
            float closestDistance = float.PositiveInfinity;
            bool hitWall = false;
            bool hitPlayer = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                bool wall = (walls.value & (1 << hit.collider.gameObject.layer)) != 0;
                bool player = !wall && target != null && hit.collider.GetComponentInParent<PlayerHealth>() == target;
                if ((!wall && !player) || hit.distance > closestDistance ||
                    (hit.distance == closestDistance && hitWall))
                    continue;

                closestDistance = hit.distance;
                hitWall = wall;
                hitPlayer = player;
            }

            if (hitWall || hitPlayer)
            {
                flying = false;
                if (hitPlayer) target.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(direction * distance);
        }
    }
}
