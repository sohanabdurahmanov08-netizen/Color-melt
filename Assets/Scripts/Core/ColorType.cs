using System.Collections.Generic;
using UnityEngine;

namespace ColorMelt.Core
{
    /// <summary>
    /// Paint colours. Values are serialized in level assets, so only ever
    /// append new colours at the end.
    /// </summary>
    public enum ColorType
    {
        None,
        Red,
        Blue,
        Yellow,
        Purple,
        Green,
        Orange,
        Brown,

        White,
        Black,
        Gray,

        // Tints: a hue plus White.
        Pink,
        SkyBlue,
        Cream,
        Lavender,
        Mint,
        Peach,
        Beige,

        // Shades: a hue plus Black.
        Maroon,
        Navy,
        Olive,
        Plum,
        DarkGreen,
        Rust,
        Chocolate,

        /// <summary>A hue spoiled by both White and Black. Never melts anything.</summary>
        Mud
    }

    public static class ColorTypeExtensions
    {
        // Pigments. A paint is the set of pigments mixed into it.
        private const int R = 1, B = 2, Y = 4, W = 8, K = 16;
        private const int Hues = R | B | Y;
        private const int Modifiers = W | K;
        private const int AllPigments = Hues | Modifiers;

        // Indexed by ColorType, in enum order.
        private static readonly (int pigments, Color color)[] Paints =
        {
            (0, Color.white),                                  // None
            (R, new Color(0.90f, 0.15f, 0.15f)),               // Red
            (B, new Color(0.15f, 0.35f, 0.95f)),               // Blue
            (Y, new Color(0.98f, 0.85f, 0.10f)),               // Yellow
            (R | B, new Color(0.55f, 0.15f, 0.75f)),           // Purple
            (B | Y, new Color(0.15f, 0.75f, 0.30f)),           // Green
            (R | Y, new Color(0.95f, 0.50f, 0.10f)),           // Orange
            (R | B | Y, new Color(0.45f, 0.30f, 0.15f)),       // Brown

            (W, new Color(0.96f, 0.96f, 0.94f)),               // White
            (K, new Color(0.10f, 0.10f, 0.12f)),               // Black
            (W | K, new Color(0.55f, 0.57f, 0.60f)),           // Gray

            (R | W, new Color(1.00f, 0.52f, 0.72f)),           // Pink
            (B | W, new Color(0.45f, 0.80f, 1.00f)),           // SkyBlue
            (Y | W, new Color(1.00f, 0.94f, 0.62f)),           // Cream
            (R | B | W, new Color(0.76f, 0.60f, 0.98f)),       // Lavender
            (B | Y | W, new Color(0.55f, 0.96f, 0.78f)),       // Mint
            (R | Y | W, new Color(1.00f, 0.72f, 0.52f)),       // Peach
            (R | B | Y | W, new Color(0.65f, 0.65f, 0.45f)),   // Beige

            (R | K, new Color(0.58f, 0.08f, 0.18f)),           // Maroon
            (B | K, new Color(0.10f, 0.17f, 0.55f)),           // Navy
            (Y | K, new Color(0.50f, 0.50f, 0.08f)),           // Olive
            (R | B | K, new Color(0.42f, 0.10f, 0.47f)),       // Plum
            (B | Y | K, new Color(0.06f, 0.42f, 0.19f)),       // DarkGreen
            (R | Y | K, new Color(0.65f, 0.25f, 0.05f)),       // Rust
            (R | B | Y | K, new Color(0.25f, 0.05f, 0.00f)),   // Chocolate

            (AllPigments, new Color(0.30f, 0.30f, 0.25f)),     // Mud
        };

        private static readonly ColorType[] ByPigments = BuildLookup();

        private static ColorType[] BuildLookup()
        {
            // Every pigment set without a name (a hue with both White and
            // Black) is Mud. Named sets are closed under taking fewer
            // pigments, so mixing stays order-independent.
            var lookup = new ColorType[AllPigments + 1];
            for (var pigments = 0; pigments < lookup.Length; pigments++)
                lookup[pigments] = ColorType.Mud;
            for (var type = 0; type < Paints.Length; type++)
                lookup[Paints[type].pigments] = (ColorType)type;
            return lookup;
        }

        /// <summary>
        /// Mixes two paints by combining their pigments. Red, Blue and Yellow
        /// give Purple (R+B), Orange (R+Y), Green (B+Y) and Brown (all three).
        /// White lightens a hue into a tint (Red+White = Pink), Black darkens
        /// it into a shade (Blue+Black = Navy), White+Black is Gray, and a hue
        /// with both White and Black turns to Mud. Mixing a paint with one it
        /// already contains changes nothing.
        /// </summary>
        public static ColorType Mix(this ColorType first, ColorType second) =>
            ByPigments[Paints[(int)first].pigments | Paints[(int)second].pigments];

        public static bool IsEmpty(this ColorType color) => color == ColorType.None;

        /// <summary>True if this paint melts a block of the given colour.</summary>
        public static bool Melts(this ColorType paint, ColorType block) =>
            paint == block && paint != ColorType.None && paint != ColorType.Mud;

        public static Color ToUnityColor(this ColorType type) => Paints[(int)type].color;

        /// <summary>
        /// Simplest way to mix the colour: a hue plus White or Black
        /// (Purple + White = Lavender), otherwise its pigments (Red + Blue =
        /// Purple). Pure pigments return just themselves.
        /// </summary>
        public static List<ColorType> Recipe(this ColorType type)
        {
            var recipe = new List<ColorType>();
            if (type == ColorType.None || type == ColorType.Mud)
                return recipe;

            var pigments = Paints[(int)type].pigments;
            var hue = pigments & Hues;
            var modifier = pigments & Modifiers;
            if (hue != 0 && modifier != 0)
            {
                recipe.Add(ByPigments[hue]);
                recipe.Add(ByPigments[modifier]);
                return recipe;
            }

            foreach (var pigment in new[] { R, B, Y, W, K })
                if ((pigments & pigment) != 0)
                    recipe.Add(ByPigments[pigment]);
            return recipe;
        }

        /// <summary>Upper-case name for UI text, e.g. "SKY BLUE".</summary>
        public static string DisplayName(this ColorType type)
        {
            var name = type.ToString();
            var text = new System.Text.StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                    text.Append(' ');
                text.Append(char.ToUpperInvariant(name[i]));
            }
            return text.ToString();
        }

        /// <summary>
        /// TMP rich-text name tinted with the colour itself. Very dark colours
        /// are lifted a little so they stay readable on dark panels.
        /// </summary>
        public static string RichName(this ColorType type)
        {
            var color = type.ToUnityColor();
            var brightness = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (brightness < 0.6f)
                color = Color.Lerp(color, Color.white, (0.6f - brightness) / 0.6f * 0.55f);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{type.DisplayName()}</color>";
        }

        /// <summary>"RED + WHITE = PINK" in rich text, or just the name for pure pigments.</summary>
        public static string RichRecipe(this ColorType type)
        {
            var recipe = type.Recipe();
            if (recipe.Count < 2)
                return type.RichName();

            var parts = new string[recipe.Count];
            for (var i = 0; i < recipe.Count; i++)
                parts[i] = recipe[i].RichName();
            return $"{string.Join(" + ", parts)} = {type.RichName()}";
        }
    }
}
