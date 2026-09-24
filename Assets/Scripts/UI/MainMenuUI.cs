using ColorMelt.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>Main menu: continue playing, level select, settings, shop.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private TMP_Text playLevelLabel;
        [SerializeField] private Button levelsButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button achievementsButton;

        [Header("Windows")]
        [SerializeField] private UIWindow levelsWindow;
        [SerializeField] private UIWindow settingsWindow;
        [SerializeField] private UIWindow shopWindow;

        [Header("Idle animation")]
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform playPulse;

        private Vector3 _titleScale;
        private Vector3 _playScale;

        private void Awake()
        {
            playButton?.onClick.AddListener(() => GameSession.PlayLevel(GameSession.ContinueLevelIndex));
            levelsButton?.onClick.AddListener(() => levelsWindow?.Open());
            settingsButton?.onClick.AddListener(() => settingsWindow?.Open());
            shopButton?.onClick.AddListener(() => shopWindow?.Open());
            achievementsButton?.onClick.AddListener(() => Toast.Show("Achievements are coming soon!"));

            if (title != null) _titleScale = title.localScale;
            if (playPulse != null) _playScale = playPulse.localScale;
        }

        private void OnEnable()
        {
            if (playLevelLabel != null)
                playLevelLabel.text = $"LEVEL {GameSession.ContinueLevelIndex + 1}";
        }

        private void Update()
        {
            // Gentle breathing keeps the menu alive and draws the eye to Play.
            var t = Time.unscaledTime;
            if (title != null)
                title.localScale = _titleScale * (1f + 0.025f * Mathf.Sin(t * 1.6f));
            if (playPulse != null)
                playPulse.localScale = _playScale * (1f + 0.04f * Mathf.Sin(t * 3.2f));
        }
    }
}
