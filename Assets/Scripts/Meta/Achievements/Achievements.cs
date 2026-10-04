using System;
using System.Collections.Generic;
using ColorMelt.Core;
using UnityEngine;

namespace ColorMelt.Meta
{
    public enum AchievementState
    {
        Locked,
        /// <summary>Task done, coins waiting in the achievements window.</summary>
        Unlocked,
        Claimed
    }

    /// <summary>
    /// Achievement progress in PlayerPrefs. Gameplay reports what happened
    /// (Add, ReportBest); level and star counts are read from Progress. An
    /// achievement unlocks as soon as its stat reaches the target, and its
    /// coins are paid out when the player claims it in the window.
    /// </summary>
    public static class Achievements
    {
        private const string StatKeyPrefix = "cm.stat.";
        private const string StateKeyPrefix = "cm.ach.";

        /// <summary>Achievements unlocked by one check (usually one, several after a big win).</summary>
        public static event Action<IReadOnlyList<AchievementData>> Unlocked;
        /// <summary>Any unlock or claim; refresh badges and lists.</summary>
        public static event Action Changed;

        public static IReadOnlyList<AchievementData> All => AchievementDatabase.Instance.achievements;

        /// <summary>Counts something that happened, e.g. Add(AchievementStat.Pours).</summary>
        public static void Add(AchievementStat stat, int amount = 1)
        {
            if (amount <= 0) return;
            var key = StatKeyPrefix + stat;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
            Check();
        }

        /// <summary>Keeps the best value seen, e.g. most blocks melted by one pour.</summary>
        public static void ReportBest(AchievementStat stat, int value)
        {
            var key = StatKeyPrefix + stat;
            if (value > PlayerPrefs.GetInt(key, 0))
                PlayerPrefs.SetInt(key, value);
            Check();
        }

        /// <summary>Unlocks every achievement whose target has been reached.</summary>
        public static void Check()
        {
            List<AchievementData> fresh = null;
            foreach (var achievement in All)
            {
                if (string.IsNullOrEmpty(achievement.id) || StateOf(achievement) != AchievementState.Locked) continue;
                if (Value(achievement.stat) < Target(achievement)) continue;

                PlayerPrefs.SetInt(StateKeyPrefix + achievement.id, (int)AchievementState.Unlocked);
                (fresh ??= new List<AchievementData>()).Add(achievement);
            }
            if (fresh == null) return;

            PlayerPrefs.Save();
            Unlocked?.Invoke(fresh);
            Changed?.Invoke();
        }

        public static int Value(AchievementStat stat)
        {
            switch (stat)
            {
                case AchievementStat.LevelsCompleted:
                    return CountLevels(stars => stars > 0 ? 1 : 0);
                case AchievementStat.TotalStars:
                    return CountLevels(stars => stars);
                case AchievementStat.PerfectLevels:
                    return CountLevels(stars => stars >= 3 ? 1 : 0);
                default:
                    return PlayerPrefs.GetInt(StatKeyPrefix + stat, 0);
            }
        }

        public static int Target(AchievementData achievement)
        {
            if (!achievement.allLevels) return Mathf.Max(1, achievement.target);
            var levels = Mathf.Max(1, LevelCount);
            return achievement.stat == AchievementStat.TotalStars ? levels * 3 : levels;
        }

        /// <summary>Progress towards the target, capped at the target.</summary>
        public static int Current(AchievementData achievement)
        {
            var target = Target(achievement);
            return StateOf(achievement) == AchievementState.Locked ? Mathf.Min(Value(achievement.stat), target) : target;
        }

        public static AchievementState StateOf(AchievementData achievement) =>
            (AchievementState)PlayerPrefs.GetInt(StateKeyPrefix + achievement.id, 0);

        public static int UnlockedCount => Count(state => state != AchievementState.Locked);

        /// <summary>Unlocked achievements whose coins have not been collected yet.</summary>
        public static int ClaimableCount => Count(state => state == AchievementState.Unlocked);

        /// <summary>Pays out an unlocked achievement's coins. False if it is locked or already claimed.</summary>
        public static bool Claim(AchievementData achievement)
        {
            if (StateOf(achievement) != AchievementState.Unlocked) return false;

            PlayerPrefs.SetInt(StateKeyPrefix + achievement.id, (int)AchievementState.Claimed);
            Progress.AddCoins(achievement.reward);
            PlayerPrefs.Save();
            Changed?.Invoke();
            return true;
        }

        private static int LevelCount => LevelDatabase.Instance != null ? LevelDatabase.Instance.Count : 0;

        private static int CountLevels(Func<int, int> perLevel)
        {
            var total = 0;
            for (var index = 0; index < LevelCount; index++)
                total += perLevel(Progress.GetStars(index));
            return total;
        }

        private static int Count(Func<AchievementState, bool> match)
        {
            var count = 0;
            foreach (var achievement in All)
                if (!string.IsNullOrEmpty(achievement.id) && match(StateOf(achievement)))
                    count++;
            return count;
        }
    }
}
