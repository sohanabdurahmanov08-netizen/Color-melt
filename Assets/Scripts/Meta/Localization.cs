using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>Languages the game ships with, in the column order of the string table.</summary>
    public enum Language
    {
        English,
        Russian,
        Uzbek
    }

    /// <summary>
    /// Game text in the player's language. Strings live in
    /// Resources/Localization/Strings.txt, a tab-separated table with one
    /// column per language that can be edited in any spreadsheet. The choice
    /// is saved; the first launch follows the device language.
    ///
    /// Plural strings use suffixed keys: English .one/.other, Russian
    /// .one/.few/.many, Uzbek .other.
    /// </summary>
    public static class Localization
    {
        private const string TableResource = "Localization/Strings";
        private const string LanguageKey = "cm.language";

        /// <summary>Language names written in the language itself, for the picker.</summary>
        public static readonly string[] NativeNames = { "English", "Русский", "O‘zbek" };

        private static readonly string[] Codes = { "en", "ru", "uz" };

        private static Dictionary<string, string[]> _table;
        private static Language? _current;

        /// <summary>Raised after the language changes, so visible text can refresh.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                if (_current == null)
                    _current = PlayerPrefs.HasKey(LanguageKey)
                        ? (Language)Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey), 0, Codes.Length - 1)
                        : DeviceLanguage();
                return _current.Value;
            }
        }

        public static void SetLanguage(Language language)
        {
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            PlayerPrefs.Save();
            if (_current == language) return;
            _current = language;
            Changed?.Invoke();
        }

        /// <summary>Text for a key in the current language, falling back to English, then the key.</summary>
        public static string Get(string key)
        {
            if (!TryGet(key, out var text))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"Localization: missing key '{key}'.");
#endif
                return key;
            }
            return text;
        }

        public static bool TryGet(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key) || !Table.TryGetValue(key, out var row)) return false;
            var index = (int)Current;
            text = index < row.Length && !string.IsNullOrEmpty(row[index]) ? row[index] : row[0];
            return !string.IsNullOrEmpty(text);
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        /// <summary>
        /// Count-dependent text, e.g. Plural("coins", 5) = "5 coins" / "5 монет".
        /// The chosen form receives the count as {0}.
        /// </summary>
        public static string Plural(string key, int count)
        {
            foreach (var form in PluralForms(count))
                if (TryGet(key + "." + form, out var text))
                    return string.Format(text, count);
            return Get(key);
        }

        private static IEnumerable<string> PluralForms(int count)
        {
            var n = Math.Abs(count);
            switch (Current)
            {
                case Language.Russian:
                    if (n % 10 == 1 && n % 100 != 11) yield return "one";
                    else if (n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14)) yield return "few";
                    else yield return "many";
                    break;
                case Language.English:
                    yield return n == 1 ? "one" : "other";
                    break;
            }
            yield return "other";
            yield return "many";
        }

        private static Language DeviceLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Russian:
                case SystemLanguage.Belarusian:
                case SystemLanguage.Ukrainian:
                    return Language.Russian;
            }

            // Uzbek has no SystemLanguage value; the culture may still know it.
            var culture = CultureInfo.CurrentUICulture.Name;
            return culture.StartsWith("uz", StringComparison.OrdinalIgnoreCase) ? Language.Uzbek : Language.English;
        }

        private static Dictionary<string, string[]> Table => _table ??= Load();

        private static Dictionary<string, string[]> Load()
        {
            var table = new Dictionary<string, string[]>();
            var asset = Resources.Load<TextAsset>(TableResource);
            if (asset == null)
            {
                Debug.LogError($"Localization: Resources/{TableResource} not found.");
                return table;
            }

            int[] columns = null;
            foreach (var rawLine in asset.text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var cells = line.Split('\t');
                if (columns == null)
                {
                    // Header: key, then language codes in any order.
                    columns = new int[Codes.Length];
                    for (var language = 0; language < Codes.Length; language++)
                        columns[language] = Array.IndexOf(cells, Codes[language]);
                    continue;
                }

                var row = new string[Codes.Length];
                for (var language = 0; language < Codes.Length; language++)
                {
                    var column = columns[language];
                    row[language] = column > 0 && column < cells.Length ? cells[column].Replace("\\n", "\n") : null;
                }
                table[cells[0].Trim()] = row;
            }
            return table;
        }

#if UNITY_EDITOR
        /// <summary>Editor: forget the loaded table so edits to Strings.txt show up.</summary>
        [UnityEditor.InitializeOnEnterPlayMode]
        private static void ResetOnEnterPlayMode()
        {
            _table = null;
            _current = null;
        }
#endif
    }
}
