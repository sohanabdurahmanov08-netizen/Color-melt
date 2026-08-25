using System;

namespace ColorMelt.Core
{
    /// <summary>
    /// Цвет потока представлен как битовая маска базовых цветов.
    /// Это позволяет получать результат смешивания простым OR:
    /// Red | Blue = Purple, и т.д. Не нужно хранить таблицу смешивания вручную,
    /// и легко добавить новые базовые цвета в будущем.
    /// </summary>
    [Flags]
    public enum ColorType
    {
        None   = 0,
        Red    = 1 << 0,
        Blue   = 1 << 1,
        Yellow = 1 << 2,

        // Производные цвета — для удобства чтения в коде и в инспекторе Unity
        Purple = Red | Blue,
        Green  = Blue | Yellow,
        Orange = Red | Yellow,
        Brown  = Red | Blue | Yellow
    }

    public static class ColorTypeExtensions
    {
        /// <summary>Смешивает два потока объединением битов.</summary>
        public static ColorType Mix(this ColorType a, ColorType b) => a | b;

        public static bool IsEmpty(this ColorType c) => c == ColorType.None;

        /// <summary>Точное совпадение цвета — используется для проверки блока.</summary>
        public static bool Matches(this ColorType flow, ColorType required) => flow == required;
    }
}
