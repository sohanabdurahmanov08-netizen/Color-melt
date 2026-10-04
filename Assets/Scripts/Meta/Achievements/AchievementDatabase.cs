using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// What an achievement measures. Values are stored by name, so new stats
    /// can go anywhere in the list.
    /// </summary>
    public enum AchievementStat
    {
        /// <summary>Levels finished at least once (read from the saved stars).</summary>
        LevelsCompleted,
        /// <summary>Best stars summed over all levels.</summary>
        TotalStars,
        /// <summary>Levels finished with 3 stars.</summary>
        PerfectLevels,
        BlocksMelted,
        Pours,
        /// <summary>Most blocks melted by a single pour (best ever).</summary>
        BestChain,
        /// <summary>Most moves left over when winning (best ever).</summary>
        MovesLeftAtWin,
        HintsUsed,
        HintPacksBought,
        /// <summary>Extra moves taken after running out.</summary>
        Continues,
        /// <summary>All coins ever received.</summary>
        CoinsEarned
    }

    [Serializable]
    public class AchievementData
    {
        [Tooltip("Saved progress and the name key ach.<id> use this, so do not rename it after release.")]
        public string id;
        public AchievementStat stat;
        [Min(1)] public int target = 1;
        [Tooltip("Target is every level (or every star for TotalStars, ×3), so it grows with new levels.")]
        public bool allLevels;
        [Min(0)] public int reward = 20;
        [Tooltip("White glyph from Art/UI/Kit/Icons.")]
        public Sprite icon;
        [Tooltip("Coloured tile behind the icon once unlocked (Art/UI/Kit/btn_*).")]
        public Sprite tile;
    }

    /// <summary>
    /// The list of achievements (Resources/AchievementDatabase): what to do,
    /// how much, and the coin reward. The window shows them in this order.
    /// Names come from the string table (ach.&lt;id&gt;), task text from
    /// ach.desc.&lt;stat&gt; (plural) or ach.desc.&lt;stat&gt;.all.
    /// </summary>
    [CreateAssetMenu(menuName = "Color Melt/Achievement Database", fileName = "AchievementDatabase")]
    public class AchievementDatabase : ScriptableObject
    {
        public const string ResourcePath = "AchievementDatabase";

        public List<AchievementData> achievements = new List<AchievementData>();

        private static AchievementDatabase _instance;

        public static AchievementDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<AchievementDatabase>(ResourcePath);
                if (_instance == null)
                    _instance = CreateInstance<AchievementDatabase>();
                return _instance;
            }
        }
    }
}
