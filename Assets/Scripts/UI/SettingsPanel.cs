using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>Sound, music and vibration switches saved in Progress.</summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Button soundButton;
        [SerializeField] private TMP_Text soundLabel;
        [SerializeField] private Button musicButton;
        [SerializeField] private TMP_Text musicLabel;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private TMP_Text vibrationLabel;

        [SerializeField] private Color onColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color offColor = new Color(0.9f, 0.3f, 0.4f);

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
        }

        private void OnEnable() => Refresh();

        private void Apply()
        {
            AudioManager.ApplySettingsNow();
            Refresh();
        }

        private void Refresh()
        {
            Show(soundButton, soundLabel, "SOUND", Progress.SoundOn);
            Show(musicButton, musicLabel, "MUSIC", Progress.MusicOn);
            Show(vibrationButton, vibrationLabel, "VIBRATION", Progress.VibrationOn);
        }

        private void Show(Button button, TMP_Text label, string name, bool on)
        {
            if (label != null) label.text = $"{name}: {(on ? "ON" : "OFF")}";
            if (button != null && button.targetGraphic != null) button.targetGraphic.color = on ? onColor : offColor;
        }
    }
}
