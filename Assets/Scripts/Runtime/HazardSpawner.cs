using System.Collections.Generic;
using UnityEngine;

namespace Zhuzbai.Platformer
{
    public sealed class HazardSpawner : MonoBehaviour
    {
        [SerializeField] private FallingProjectile prefab = null;
        [SerializeField] private Sprite warningSprite = null;
        [SerializeField, Min(2)] private float interval = 4;
        [SerializeField, Min(1)] private int maximumAlive = 2;
        private readonly List<FallingProjectile> alive = new List<FallingProjectile>(2);
        private float nextSpawn;
        private void Start() => nextSpawn = Time.time + interval;
        private void Update()
        {
            if (GameMenuController.IsGameplayBlocked || Time.timeScale <= 0 || prefab == null || Time.time < nextSpawn) return;
            nextSpawn = Time.time + interval;
            for (int i = alive.Count - 1; i >= 0; i--) if (alive[i] == null) alive.RemoveAt(i);
            if (alive.Count >= maximumAlive) return;
            var projectile = Instantiate(prefab, transform.position, Quaternion.identity, transform);
            alive.Add(projectile);
            if (warningSprite != null)
            {
                var marker = new GameObject("Hazard warning", typeof(SpriteRenderer));
                marker.transform.SetParent(transform, true);
                marker.transform.position = new Vector3(transform.position.x, 0.1f, 0);
                marker.transform.localScale = Vector3.one * 3.3f;
                var sprite = marker.GetComponent<SpriteRenderer>(); sprite.sprite = warningSprite; sprite.sortingOrder = 6;
                var source = prefab.GetComponent<SpriteRenderer>(); if (source != null) sprite.sharedMaterial = source.sharedMaterial;
                projectile.SetWarning(marker);
            }
        }
    }
}
