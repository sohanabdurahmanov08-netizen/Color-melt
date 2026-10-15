using System;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Persistent player progress in PlayerPrefs: coins, hints, unlocked
    /// levels, best stars and settings. Everything that survives a restart.
    /// </summary>
    public static class Progress
    {
        private const string CoinsKey = "cm.coins";
        private const string HintsKey = "cm.hints";
        private const string UnlockedKey = "cm.unlocked";
        private const string StarsKeyPrefix = "cm.stars.";
        private const string SoundKey = "cm.sound";
        private const string MusicKey = "cm.music";
        private const string VibrationKey = "cm.vibration";
        private const string InitializedKey = "cm.initialized";
        private const string BoosterKeyPrefix = "cm.booster.";
        private const string BoosterSeenKeyPrefix = "cm.booster_seen.";

        public static event Action<int> CoinsChanged;
        public static event Action<int> HintsChanged;
        public static event Action<BoosterType, int> BoostersChanged;

        static Progress()
        {
            if (PlayerPrefs.GetInt(InitializedKey, 0) == 1) return;

            PlayerPrefs.SetInt(InitializedKey, 1);
            PlayerPrefs.SetInt(HintsKey, GameConfig.Instance.startingHints);
            PlayerPrefs.Save();
        }

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
            PrefsSaver.Request();
            CoinsChanged?.Invoke(Coins);
            if (amount > 0) Achievements.Add(AchievementStat.CoinsEarned, amount);
        }

        public static bool TrySpendCoins(int amount)
        {
            if (Coins < amount) return false;
            AddCoins(-amount);
            return true;
        }

        public static int Hints => PlayerPrefs.GetInt(HintsKey, 0);

        public static void AddHints(int amount) => AddBooster(BoosterType.Hint, amount);

        // Hints keep their original key; the other boosters start with the
        // amounts in GameConfig, also for players who saved before they existed.
        private static string BoosterKey(BoosterType type) =>
            type == BoosterType.Hint ? HintsKey : BoosterKeyPrefix + type;

        public static int GetBoosters(BoosterType type) =>
            PlayerPrefs.GetInt(BoosterKey(type), GameConfig.Instance.BoosterStartCount(type));

        public static void AddBooster(BoosterType type, int amount)
        {
            var count = Mathf.Max(0, GetBoosters(type) + amount);
            PlayerPrefs.SetInt(BoosterKey(type), count);
            PrefsSaver.Request();
            BoostersChanged?.Invoke(type, count);
            if (type == BoosterType.Hint) HintsChanged?.Invoke(count);
        }

        /// <summary>True the first time it is asked for a booster, so its intro shows once.</summary>
        public static bool MarkBoosterSeen(BoosterType type)
        {
            var key = BoosterSeenKeyPrefix + type;
            if (PlayerPrefs.GetInt(key, 0) == 1) return false;
            PlayerPrefs.SetInt(key, 1);
            PrefsSaver.Request();
            return true;
        }

        /// <summary>Highest level index the player may start (0-based).</summary>
        public static int UnlockedLevel => PlayerPrefs.GetInt(UnlockedKey, 0);

        public static int GetStars(int levelIndex) => PlayerPrefs.GetInt(StarsKeyPrefix + levelIndex, 0);

        public static void CompleteLevel(int levelIndex, int stars)
        {
            if (stars > GetStars(levelIndex))
                PlayerPrefs.SetInt(StarsKeyPrefix + levelIndex, stars);
            if (levelIndex + 1 > UnlockedLevel)
                PlayerPrefs.SetInt(UnlockedKey, levelIndex + 1);
            PlayerPrefs.Save();
        }

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set { PlayerPrefs.SetInt(SoundKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool VibrationOn
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set { PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Color Melt/Reset Player Progress")]
        private static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("Color Melt: player progress reset.");
        }
#endif
    }
}
