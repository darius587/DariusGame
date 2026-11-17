using UnityEngine;
namespace Zhuzbai.Platformer
{
    [CreateAssetMenu(menuName = "Darius/PlayerStats") ]
    public sealed class PlayerStats : ScriptableObject
    {
        [Min(1)] public int bulletProjectile_damage = 1;
        [Min(1)] public int playerHealth_abyssDamage = 1;
        [Tooltip("PlayerFoundation"), Min(0)] public float playerFoundation_moveSpeed = 6;
        [Tooltip("PlayerFoundation"), Min(0)] public float playerFoundation_jumpSpeed = 13.5f;
        [Tooltip("PlayerFoundation"), Min(0.01f)] public float playerFoundation_fireCooldown = 0.3f;
        [Tooltip("PlayerHealth"), Min(1)] public int playerHealth_maximumHealth = 3;
        [Tooltip("PlayerHealth"), Min(0)] public float playerHealth_invulnerabilitySeconds = 1;
        [Tooltip("BulletProjectile"), Min(0)] public float bulletProjectile_speed = 14;
        [Tooltip("BulletProjectile"), Min(0.01f)] public float bulletProjectile_lifetime = 3;
    }
}
