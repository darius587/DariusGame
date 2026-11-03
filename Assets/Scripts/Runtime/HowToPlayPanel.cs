using UnityEngine;
using UnityEngine.UI;

namespace Zhuzbai.Platformer
{
    public sealed class HowToPlayPanel : MonoBehaviour
    {
        [SerializeField] private Text objective = null;
        private void OnEnable()
        {
            var definition = FindAnyObjectByType<LevelDefinition>();
            if (objective != null && definition != null)
                objective.text = $"LEVEL {definition.LevelNumber} — {definition.LevelName.ToUpperInvariant()}\n\n{definition.Objective}";
            var goals = transform.Find("Goals");
            if (goals != null && definition != null)
                goals.GetComponent<Text>().text = definition.IsBossLevel ?
                    "1. Hit SHADOW KING with 15 green energy blasts.\n\n2. Dodge warned shots. Jump over low waves in Phase 2.\n\n3. You have 3 HP. Death restarts this boss fight.\n\n4. Defeat the king to win. No coins or gate required." :
                    "1. Collect all coins to unlock the exit gate.\n\n2. Reach the unlocked gate to finish the level.\n\n3. You have 3 HP. Avoid falls, enemies, rocks and bombs.\n\n4. Complete all four levels to win.";
        }
    }
}
