using UnityEngine;
namespace Zhuzbai.Platformer
{
    [CreateAssetMenu(menuName = "Darius/Item Data")]
    public sealed class ItemData : ScriptableObject
    {
        [Min(1)] public int coinValue = 1;
        [Min(0)] public int scoreReward = 1;
    }
}
