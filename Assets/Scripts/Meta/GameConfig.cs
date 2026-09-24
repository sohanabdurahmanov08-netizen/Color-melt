using UnityEngine;

namespace ColorMelt.Meta
{
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

        public int StarsFor(int movesUsed, int optimalMoves)
        {
            if (optimalMoves <= 0 || movesUsed <= optimalMoves) return 3;
            return movesUsed <= optimalMoves + twoStarSlack ? 2 : 1;
        }
    }
}
