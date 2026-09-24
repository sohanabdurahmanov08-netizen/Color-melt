using ColorMelt.Core;
using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Scene flow between the menu and the single game scene, with a short
    /// fade. The game scene reads LevelIndex to know which level to build.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "Main Menu";
        public const string GameScene = "Game";

        private static int _levelIndex = -1;

        /// <summary>Level to play; defaults to the furthest unlocked one.</summary>
        public static int LevelIndex
        {
            get
            {
                if (_levelIndex < 0)
                    _levelIndex = ContinueLevelIndex;
                return _levelIndex;
            }
            private set => _levelIndex = value;
        }

        public static int LevelCount => LevelDatabase.Instance != null ? LevelDatabase.Instance.Count : 0;

        public static int ContinueLevelIndex => Mathf.Clamp(Progress.UnlockedLevel, 0, Mathf.Max(0, LevelCount - 1));

        public static bool HasNextLevel => LevelIndex + 1 < LevelCount;

        /// <summary>Marks a level as current without loading (editor test runs).</summary>
        public static void SetCurrent(int index) => LevelIndex = index;

        public static void PlayLevel(int index)
        {
            LevelIndex = Mathf.Clamp(index, 0, Mathf.Max(0, LevelCount - 1));
            Load(GameScene);
        }

        public static void Restart() => PlayLevel(LevelIndex);

        public static void NextLevel() => PlayLevel(HasNextLevel ? LevelIndex + 1 : LevelIndex);

        public static void ToMenu() => Load(MenuScene);

        private static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneFader.Instance.FadeTo(scene);
        }
    }
}
