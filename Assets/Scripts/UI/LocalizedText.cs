using ColorMelt.Meta;
using TMPro;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Shows a string from the localization table on a TextMeshPro label and
    /// updates it when the player switches language. For fixed text such as
    /// titles and button captions; code-driven text uses Localization directly.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Key in Resources/Localization/Strings.txt, e.g. btn.play.")]
        [SerializeField] private string key;

        private TMP_Text _text;

        public string Key
        {
            get => key;
            set
            {
                key = value;
                Apply();
            }
        }

        private void OnEnable()
        {
            Localization.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Localization.Changed -= Apply;

        private void Apply()
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_text == null) _text = GetComponent<TMP_Text>();
            _text.text = Localization.Get(key);
        }
    }
}
