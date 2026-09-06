using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Connects the scene's Play and Settings buttons and handles UI sounds.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public sealed class MainMenuUI : MonoBehaviour
    {
        [Header("Scenes")]
        [SerializeField] private string gameplaySceneName = "Level 1";

        [Header("Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;

        [Header("UI Sounds")]
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField, Range(0f, 1f)] private float buttonClickVolume = 1f;

        private AudioSource audioSource;
        private GameObject settingsWindow;

        private void Awake()
        {
            // Создаём AudioSource для звуков интерфейса
            audioSource = gameObject.GetComponent<AudioSource>();

            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = false;

            playButton ??= FindButton("Play");
            settingsButton ??= FindButton("Settings");

            if (playButton != null)
            {
                playButton.onClick.AddListener(PlayButtonSound);
                playButton.onClick.AddListener(StartGame);
            }
            else
            {
                Debug.LogWarning("MainMenuUI: кнопка Play не найдена.", this);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(PlayButtonSound);
                settingsButton.onClick.AddListener(ShowSettings);
            }
            else
            {
                Debug.LogWarning("MainMenuUI: кнопка Settings не найдена.", this);
            }

            settingsWindow = CreateSettingsWindow();
            settingsWindow.SetActive(false);
        }

        public void StartGame()
        {
            if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                SceneManager.LoadScene(gameplaySceneName);
            }
            else
            {
                Debug.LogError(
                    $"MainMenuUI: сцена '{gameplaySceneName}' не добавлена в Build Settings.",
                    this
                );
            }
        }

        public void ShowSettings()
        {
            settingsWindow?.SetActive(true);
        }

        public void HideSettings()
        {
            settingsWindow?.SetActive(false);
        }

        private void PlayButtonSound()
        {
            if (buttonClickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(buttonClickSound, buttonClickVolume);
            }
        }

        private Button FindButton(string objectName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.name == objectName)
                    return button;
            }

            return null;
        }

        private GameObject CreateSettingsWindow()
        {
            var overlay = CreateImage(
                "Settings Window",
                transform,
                new Color(0f, 0f, 0f, 0.72f)
            );

            Stretch(overlay.rectTransform);

            var panel = CreateImage(
                "Panel",
                overlay.transform,
                new Color(0.08f, 0.12f, 0.2f, 0.98f)
            );

            panel.rectTransform.anchorMin =
                panel.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);

            panel.rectTransform.sizeDelta = new Vector2(720f, 430f);

            CreateText(
                "Heading",
                panel.transform,
                "SETTINGS",
                52,
                new Vector2(0f, 0.72f),
                new Vector2(1f, 0.94f)
            );

            CreateText(
                "Volume label",
                panel.transform,
                "MASTER VOLUME",
                25,
                new Vector2(0.12f, 0.49f),
                new Vector2(0.88f, 0.62f)
            );

            var slider = CreateVolumeSlider(panel.transform);

            slider.SetValueWithoutNotify(
                PlayerPrefs.GetFloat("MasterVolume", AudioListener.volume)
            );

            ApplyVolume(slider.value);
            slider.onValueChanged.AddListener(ApplyVolume);

            var close = CreateButton(
                "Close",
                panel.transform,
                "CLOSE"
            );

            var closeRect = close.GetComponent<RectTransform>();

            closeRect.anchorMin = new Vector2(0.33f, 0.12f);
            closeRect.anchorMax = new Vector2(0.67f, 0.3f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;

            // Звук кнопки Close
            close.onClick.AddListener(PlayButtonSound);
            close.onClick.AddListener(HideSettings);

            return overlay.gameObject;
        }

        private Slider CreateVolumeSlider(Transform parent)
        {
            var root = new GameObject(
                "Volume slider",
                typeof(RectTransform),
                typeof(Slider)
            );

            root.transform.SetParent(parent, false);

            var rect = root.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0.12f, 0.38f);
            rect.anchorMax = new Vector2(0.88f, 0.46f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var slider = root.GetComponent<Slider>();

            var track = CreateImage(
                "Track",
                root.transform,
                new Color(0.02f, 0.03f, 0.06f, 1f)
            );

            Stretch(track.rectTransform);

            var fill = CreateImage(
                "Fill",
                root.transform,
                new Color(0.16f, 0.78f, 0.95f, 1f)
            );

            Stretch(fill.rectTransform);

            fill.rectTransform.anchorMax = new Vector2(0.7f, 1f);

            var handle = CreateImage(
                "Handle",
                root.transform,
                Color.white
            );

            handle.rectTransform.anchorMin =
                handle.rectTransform.anchorMax =
                new Vector2(0.7f, 0.5f);

            handle.rectTransform.sizeDelta = new Vector2(28f, 44f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;

            slider.minValue = 0f;
            slider.maxValue = 1f;

            return slider;
        }

        private void ApplyVolume(float volume)
        {
            AudioListener.volume = volume;

            PlayerPrefs.SetFloat("MasterVolume", volume);
            PlayerPrefs.Save();
        }

        private static Image CreateImage(
            string objectName,
            Transform parent,
            Color color
        )
        {
            var image = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            ).GetComponent<Image>();

            image.transform.SetParent(parent, false);
            image.color = color;

            return image;
        }

        private static void CreateText(
            string objectName,
            Transform parent,
            string value,
            float fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            var text = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            ).GetComponent<TextMeshProUGUI>();

            text.transform.SetParent(parent, false);

            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            text.rectTransform.anchorMin = anchorMin;
            text.rectTransform.anchorMax = anchorMax;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            string caption
        )
        {
            var image = CreateImage(
                objectName,
                parent,
                new Color(0.16f, 0.78f, 0.95f, 1f)
            );

            var button = image.gameObject.AddComponent<Button>();

            var text = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            ).GetComponent<TextMeshProUGUI>();

            text.transform.SetParent(image.transform, false);

            text.font = TMP_Settings.defaultFontAsset;
            text.text = caption;
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;

            Stretch(text.rectTransform);

            return button;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}