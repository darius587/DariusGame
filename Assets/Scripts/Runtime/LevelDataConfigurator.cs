using UnityEngine;

namespace Zhuzbai.Platformer
{
    public static class LevelDataConfigurator
    {
        public static void Configure(LevelDefinition definition)
        {
            if (definition == null || definition.Data == null) return;
            var data = definition.Data;
            var player = Object.FindAnyObjectByType<PlayerHealth>();
            if (player != null)
            {
                player.SetSpawnPoint(data.playerSpawn);
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null) body.position = data.playerSpawn;
                player.transform.position = data.playerSpawn;
            }
            // Existing campaign enemies and lanes remain in the authored scene.
            // Configure their spawn and balance without generating waves or duplicates.
            for (int i = 0; i < data.enemies.Length && i < definition.EnemyObjects.Length; i++)
            {
                var target = definition.EnemyObjects[i];
                if (target == null) continue;
                var config = data.enemies[i];
                target.position = config.spawnPosition;
                var body = target.GetComponent<Rigidbody2D>();
                if (body != null) body.position = config.spawnPosition;
                foreach (var component in target.GetComponents<MonoBehaviour>())
                    if (component is IEnemyStatsReceiver receiver) receiver.ApplyStats(config.stats);
            }
            Physics2D.SyncTransforms();
        }
    }
}
