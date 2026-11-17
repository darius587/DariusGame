using UnityEngine;
namespace Zhuzbai.Platformer
{
    public interface IEnemyStatsReceiver { void ApplyStats(EnemyStats data); }
    [CreateAssetMenu(menuName = "Darius/EnemyStats") ]
    public sealed class EnemyStats : ScriptableObject
    {
        [Min(1)] public int bossMaximumHealth = 15;
        [Min(1)] public int bossPhaseThreshold = 8;
        [Min(1)] public int bossDamage = 1;
        public float bossEntryDelay = 2.5f;
        public float bossWindUp = 0.95f;
        public float bossPhase2WindUp = 0.75f;
        public float bossCooldown = 1.7f;
        public float bossPhase2Cooldown = 0.95f;
        public float bossShotSpeed = 4f;
        public float bossPhase2ShotSpeed = 4.8f;
        public float bossWaveSpeed = 4.5f;
        public float bossWaveInterval = 0.2f;
        public int bossProjectileLimit = 12;
        public float bossProjectileLifetime = 7;
        [Tooltip("PatrollerEnemy"), Min(0.1f)] public float patrollerEnemy_patrolSpeed = 1.4f;
        [Tooltip("PatrollerEnemy"), Min(0.1f)] public float patrollerEnemy_chaseSpeed = 2.8f;
        [Tooltip("PatrollerEnemy"), Min(0.1f)] public float patrollerEnemy_strikeRange = 1.25f;
        [Tooltip("PatrollerEnemy"), Min(0)] public float patrollerEnemy_wallClearance = 0.02f;
        [Tooltip("PatrollerEnemy"), Min(1)] public int patrollerEnemy_damage = 1;
        [Tooltip("PatrollerEnemy"), Range(0.3f, 0.8f)] public float patrollerEnemy_windUpSeconds = 0.4f;
        [Tooltip("PatrollerEnemy"), Min(0.1f)] public float patrollerEnemy_cooldownSeconds = 1f;
        [Tooltip("ShooterEnemy"), Min(0.1f)] public float shooterEnemy_scanTurnSeconds = 1f;
        [Tooltip("ShooterEnemy"), Min(0.1f)] public float shooterEnemy_preferredDistance = 3f;
        [Tooltip("ShooterEnemy"), Min(0.1f)] public float shooterEnemy_retreatSpeed = 2f;
        [Tooltip("ShooterEnemy"), Min(0)] public float shooterEnemy_wallClearance = 0.02f;
        [Tooltip("ShooterEnemy"), Range(0.3f, 0.8f)] public float shooterEnemy_windUpSeconds = 0.6f;
        [Tooltip("ShooterEnemy"), Min(0.1f)] public float shooterEnemy_cooldownSeconds = 1.4f;
        [Tooltip("ChargerEnemy"), Min(0.1f)] public float chargerEnemy_patrolSpeed = 1.2f;
        [Tooltip("ChargerEnemy"), Min(0.1f)] public float chargerEnemy_chargeSpeed = 8f;
        [Tooltip("ChargerEnemy"), Min(0.1f)] public float chargerEnemy_chargeRange = 4.5f;
        [Tooltip("ChargerEnemy"), Min(0.1f)] public float chargerEnemy_chargeSeconds = 0.5f;
        [Tooltip("ChargerEnemy"), Min(0)] public float chargerEnemy_maximumHeightDifference = 0.85f;
        [Tooltip("ChargerEnemy"), Min(0)] public float chargerEnemy_wallClearance = 0.02f;
        [Tooltip("ChargerEnemy"), Min(1)] public int chargerEnemy_damage = 1;
        [Tooltip("ChargerEnemy"), Range(0.3f, 0.8f)] public float chargerEnemy_windUpSeconds = 0.7f;
        [Tooltip("ChargerEnemy"), Min(0.1f)] public float chargerEnemy_recoverySeconds = 1.5f;
        [Tooltip("EnemyHealth"), Min(1)] public int enemyHealth_maximumHealth = 2;
        [Tooltip("EnemyPerception2D"), Min(0.1f)] public float enemyPerception2D_sightDistance = 7f;
        [Tooltip("EnemyPerception2D"), Range(1f, 360f)] public float enemyPerception2D_fieldOfView = 110f;
        [Tooltip("EnemyProjectile"), Min(1)] public int enemyProjectile_damage = 1;
        [Tooltip("EnemyProjectile"), Min(0.1f)] public float enemyProjectile_speed = 5f;
        [Tooltip("EnemyProjectile"), Min(0.1f)] public float enemyProjectile_lifetime = 3f;
        [Tooltip("EnemyProjectile"), Min(0.01f)] public float enemyProjectile_collisionRadius = 0.12f;
        [Tooltip("PatrolEnemy")] public float patrolEnemy_speed = 1.1f;
        [Min(1)] public int patrolEnemy_maximumHealth = 2;
    }
}
