using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Sound, music and vibration switches saved in Progress, plus the
    /// language picker: one button per Language, the current one highlighted.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button soundButton;
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private Button musicButton;
        [SerializeField] private TMP_Text musicLabel;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private TMP_Text vibrationLabel;

        [Tooltip("One button per Language, in enum order (English, Russian, Uzbek).")]
        [SerializeField] private Button[] languageButtons;

        [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color offColor = new Color(0.9f, 0.3f, 0.4f);
        [SerializeField] private Color idleLanguageColor = Color.white;

        private void Awake()
        {
            soundButton?.onClick.AddListener(() => { Progress.SoundOn = !Progress.SoundOn; Apply(); });
            musicButton?.onClick.AddListener(() => { Progress.MusicOn = !Progress.MusicOn; Apply(); });
            vibrationButton?.onClick.AddListener(() =>
            {
                Progress.VibrationOn = !Progress.VibrationOn;
                Apply();
                Haptics.Impact();
            });

            if (languageButtons == null) return;
            for (var index = 0; index < languageButtons.Length; index++)
            {
                var language = (Language)index;
                var button = languageButtons[index];
                if (button == null) continue;
                button.onClick.AddListener(() => Localization.SetLanguage(language));
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = Localization.NativeNames[index];
            }
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Apply()
        {
            AudioManager.ApplySettingsNow();
            Refresh();
        }

        private void Refresh()
        {
            Show(soundButton, soundLabel, "settings.sound", Progress.SoundOn);
            Show(musicButton, musicLabel, "settings.music", Progress.MusicOn);
            Show(vibrationButton, vibrationLabel, "settings.vibration", Progress.VibrationOn);

            if (languageButtons == null) return;
            for (var index = 0; index < languageButtons.Length; index++)
            {
                var button = languageButtons[index];
                if (button != null && button.targetGraphic != null)
                    button.targetGraphic.color = index == (int)Localization.Current ? onColor : idleLanguageColor;
            }
        }

        private void Show(Button button, TMP_Text label, string nameKey, bool on)
        {
            if (label != null)
                label.text = Localization.Format("settings.toggle", Localization.Get(nameKey),
                    Localization.Get(on ? "settings.on" : "settings.off"));
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = on ? onColor : offColor;
        }
    }
}
