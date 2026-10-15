using ColorMelt.Core;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>In-level helpers the player owns or buys with coins. Values are saved, append only.</summary>
    public enum BoosterType
    {
        /// <summary>Shows the next move of the best solution.</summary>
        Hint,
        /// <summary>Takes back the last pour.</summary>
        Undo,
        /// <summary>Melts the first block of a channel the player taps, every layer.</summary>
        Rainbow,
        /// <summary>Adds a few moves while the level is still running.</summary>
        ExtraMoves
    }

    /// <summary>Economy and pacing numbers in one tweakable asset (Resources/GameConfig).</summary>
    [CreateAssetMenu(menuName = "Color Melt/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        private const string ResourcePath = "GameConfig";

        [Header("Rewards")]
        public int coinsPerBlock = 5;
        public int coinsPerWin = 20;
        public int coinsPerStar = 10;

        [Header("Stars")]
        [Tooltip("Moves above the optimal solution that still give 2 stars.")]
        public int twoStarSlack = 2;

        [Header("Boosters")]
        public int hintCost = 25;
        public int continueCost = 40;
        public int continueMoves = 3;
        public int startingHints = 3;

        [Tooltip("Coin price when the player has none of this booster left.")]
        public int undoCost = 20;
        public int rainbowCost = 80;
        public int extraMovesCost = 50;
        public int extraMoves = 3;
        public int startingUndos = 3;
        public int startingRainbows = 1;
        public int startingExtraMoves = 1;

        [Tooltip("Level numbers (1-based) where each booster appears. Earlier levels hide it.")]
        public int hintFromLevel = 2;
        public int undoFromLevel = 2;
        public int extraMovesFromLevel = 3;
        public int rainbowFromLevel = 4;

        [Header("Hard and Boss levels")]
        [Tooltip("The win reward is multiplied by this on Hard levels.")]
        [Min(1f)] public float hardRewardMultiplier = 1.5f;
        [Min(1f)] public float bossRewardMultiplier = 2f;

        [Header("Shop")]
        public int hintPackSize = 3;
        public int hintPackCost = 60;
        public int freeCoinsReward = 50;

        private static GameConfig _instance;

        public static GameConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<GameConfig>(ResourcePath);
                if (_instance == null)
                    _instance = CreateInstance<GameConfig>();
                return _instance;
            }
        }

        public int BoosterCost(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Hint: return hintCost;
                case BoosterType.Undo: return undoCost;
                case BoosterType.Rainbow: return rainbowCost;
                default: return extraMovesCost;
            }
        }

        public int BoosterStartCount(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Hint: return startingHints;
                case BoosterType.Undo: return startingUndos;
                case BoosterType.Rainbow: return startingRainbows;
                default: return startingExtraMoves;
            }
        }

        /// <summary>1-based level number from which the booster is shown.</summary>
        public int BoosterFromLevel(BoosterType type)
        {
            switch (type)
            {
                case BoosterType.Hint: return hintFromLevel;
                case BoosterType.Undo: return undoFromLevel;
                case BoosterType.Rainbow: return rainbowFromLevel;
                default: return extraMovesFromLevel;
            }
        }

        public float RewardMultiplier(LevelDifficulty difficulty)
        {
            switch (difficulty)
            {
                case LevelDifficulty.Hard: return hardRewardMultiplier;
                case LevelDifficulty.Boss: return bossRewardMultiplier;
                default: return 1f;
            }
        }

        public int StarsFor(int movesUsed, int optimalMoves)
        {
            if (optimalMoves <= 0 || movesUsed <= optimalMoves) return 3;
            return movesUsed <= optimalMoves + twoStarSlack ? 2 : 1;
        }
    }
}
