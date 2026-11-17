using System;
using UnityEditor;
using UnityEngine;

namespace Zhuzbai.Platformer.Editor
{
    internal static class BalanceEditorUtility
    {
        public static SerializedProperty Property(ref SerializedObject owner, UnityEngine.Object target, string field)
        {
            var local = owner.FindProperty(field);
            if (local != null && field != "hitPoints") return local;
            var reference = owner.FindProperty("stats");
            if (reference == null) throw new InvalidOperationException("Unknown balance field " + field);
            if (reference.objectReferenceValue == null)
            {
                string label = target is PlayerHealth || target is PlayerFoundation || target is BulletProjectile ? "Player" :
                    target is ChargerEnemy || target.name.Contains("Charger") ? "Charger" :
                    target is ShooterEnemy || target is EnemyProjectile || target.name.Contains("Shooter") ? "Shooter" :
                    target is ShadowKingBoss || target is ShadowKingHealth || target is ShadowKingProjectile ? "Boss" : "Patroller";
                reference.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/Balance/" + label + "_Stats.asset");
                owner.ApplyModifiedPropertiesWithoutUndo();
            }
            var asset = reference.objectReferenceValue;
            owner = new SerializedObject(asset);
            string type = target.GetType().Name;
            string key = char.ToLowerInvariant(type[0]) + type.Substring(1) + "_" + field;
            if (target is PatrolEnemy && field == "hitPoints") key = "patrolEnemy_maximumHealth";
            return owner.FindProperty(key) ?? throw new InvalidOperationException("Unknown balance field " + key);
        }
        public static float PlayerValue(PlayerFoundation player, string field)
        {
            var owner = new SerializedObject(player);
            return Property(ref owner, player, field).floatValue;
        }
    }
}
