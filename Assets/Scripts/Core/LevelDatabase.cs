using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Ordered list of all levels. Lives in a Resources folder so both the
    /// menu and the game scene can load it without scene references.
    /// </summary>
    [CreateAssetMenu(menuName = "Color Melt/Level Database", fileName = "LevelDatabase")]
    public class LevelDatabase : ScriptableObject
    {
        public const string ResourcePath = "LevelDatabase";

        public List<LevelData> levels = new List<LevelData>();

        private static LevelDatabase _instance;

        public static LevelDatabase Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<LevelDatabase>(ResourcePath);
                return _instance;
            }
        }

        public int Count => levels.Count;

        public LevelData Get(int index) =>
            levels.Count == 0 ? null : levels[Mathf.Clamp(index, 0, levels.Count - 1)];

        public int IndexOf(LevelData level) => levels.IndexOf(level);
    }
}
