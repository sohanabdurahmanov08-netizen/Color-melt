using System;

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
        /// Смешивает два цвета как краску.
        ///
        /// Основные цвета:
        /// Red    = R
        /// Blue   = B
        /// Yellow = Y
        ///
        /// Вторичные:
        /// Purple = R + B
        /// Green  = B + Y
        /// Orange = R + Y
        ///
        /// Все три основных:
        /// Brown = R + B + Y
        /// </summary>
        public static ColorType Mix(this ColorType first, ColorType second)
        {
            if (first == ColorType.None)
                return second;

            if (second == ColorType.None)
                return first;

            int mask = GetMask(first) | GetMask(second);

            switch (mask)
            {
                case 1:
                    return ColorType.Red;

                case 2:
                    return ColorType.Blue;

                case 4:
                    return ColorType.Yellow;

                case 3:
                    // Red + Blue
                    return ColorType.Purple;

                case 5:
                    // Red + Yellow
                    return ColorType.Orange;

                case 6:
                    // Blue + Yellow
                    return ColorType.Green;

                case 7:
                    // Red + Blue + Yellow
                    return ColorType.Brown;

                default:
                    return ColorType.None;
            }
        }

        /// <summary>
        /// Проверяет, является ли цвет пустым.
        /// </summary>
        public static bool IsEmpty(this ColorType color)
        {
            return color == ColorType.None;
        }

        /// <summary>
        /// Проверяет совпадение цвета.
        /// </summary>
        public static bool Matches(this ColorType color, ColorType required)
        {
            return color == required;
        }

        private static int GetMask(ColorType color)
        {
            switch (color)
            {
                case ColorType.Red:
                    return 1;

                case ColorType.Blue:
                    return 2;

                case ColorType.Yellow:
                    return 4;

                case ColorType.Purple:
                    return 1 | 2;

                case ColorType.Orange:
                    return 1 | 4;

                case ColorType.Green:
                    return 2 | 4;

                case ColorType.Brown:
                    return 1 | 2 | 4;

                default:
                    return 0;
            }
        }
    }
}

