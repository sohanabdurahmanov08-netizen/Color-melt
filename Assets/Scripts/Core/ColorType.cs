using UnityEngine;

namespace ColorMelt.Core
{
    public enum ColorType
    {
        None,
        Red,
        Blue,
        Yellow,
        Purple,
        Green,
        Orange,
        Brown
    }

    public static class ColorTypeExtensions
    {
        /// <summary>
        /// Mixes two paints. Primaries are Red, Blue and Yellow; secondaries are
        /// Purple (R+B), Orange (R+Y) and Green (B+Y); all three give Brown.
        /// </summary>
        public static ColorType Mix(this ColorType first, ColorType second)
        {
            switch (GetMask(first) | GetMask(second))
            {
                case 1: return ColorType.Red;
                case 2: return ColorType.Blue;
                case 4: return ColorType.Yellow;
                case 3: return ColorType.Purple;
                case 5: return ColorType.Orange;
                case 6: return ColorType.Green;
                case 7: return ColorType.Brown;
                default: return ColorType.None;
            }
        }

        public static bool IsEmpty(this ColorType color) => color == ColorType.None;

        public static Color ToUnityColor(this ColorType type)
        {
            switch (type)
            {
                case ColorType.Red: return new Color(0.90f, 0.15f, 0.15f);
                case ColorType.Blue: return new Color(0.15f, 0.35f, 0.95f);
                case ColorType.Yellow: return new Color(0.98f, 0.85f, 0.10f);
                case ColorType.Purple: return new Color(0.55f, 0.15f, 0.75f);
                case ColorType.Green: return new Color(0.15f, 0.75f, 0.30f);
                case ColorType.Orange: return new Color(0.95f, 0.50f, 0.10f);
                case ColorType.Brown: return new Color(0.45f, 0.30f, 0.15f);
                default: return Color.white;
            }
        }

        /// <summary>Upper-case name for UI text, e.g. "BLUE".</summary>
        public static string DisplayName(this ColorType type) => type.ToString().ToUpperInvariant();

        /// <summary>TMP rich-text name tinted with the colour itself.</summary>
        public static string RichName(this ColorType type) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(type.ToUnityColor())}>{type.DisplayName()}</color>";

        private static int GetMask(ColorType color)
        {
            switch (color)
            {
                case ColorType.Red: return 1;
                case ColorType.Blue: return 2;
                case ColorType.Yellow: return 4;
                case ColorType.Purple: return 1 | 2;
                case ColorType.Orange: return 1 | 4;
                case ColorType.Green: return 2 | 4;
                case ColorType.Brown: return 1 | 2 | 4;
                default: return 0;
            }
        }
    }
}
