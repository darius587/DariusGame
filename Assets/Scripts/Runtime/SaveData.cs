using System;
using System.Collections.Generic;

namespace Zhuzbai.Platformer
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;
        public int saveVersion = CurrentVersion;
        public int bestScore;
        public int totalCoins;
        public List<string> unlockedLevels = new List<string> { "MainLevel" };
        public List<string> completedLevels = new List<string>();

        public void Normalize()
        {
            bestScore = Math.Max(0, bestScore);
            totalCoins = Math.Max(0, totalCoins);
            if (unlockedLevels == null) unlockedLevels = new List<string>();
            if (completedLevels == null) completedLevels = new List<string>();
            string[] campaign = { "MainLevel", "Level2", "Level3", "Level4" };
            unlockedLevels.RemoveAll(id => Array.IndexOf(campaign, id) < 0);
            completedLevels.RemoveAll(id => Array.IndexOf(campaign, id) < 0 && id != "EnemyAI");
            unlockedLevels = new List<string>(new HashSet<string>(unlockedLevels));
            completedLevels = new List<string>(new HashSet<string>(completedLevels));
            if (!unlockedLevels.Contains("MainLevel")) unlockedLevels.Add("MainLevel");
            for (int i = 0; i < campaign.Length; i++)
                if (completedLevels.Contains(campaign[i]))
                    for (int j = 0; j <= Math.Min(i + 1, campaign.Length - 1); j++)
                        if (!unlockedLevels.Contains(campaign[j])) unlockedLevels.Add(campaign[j]);
        }
    }
}
