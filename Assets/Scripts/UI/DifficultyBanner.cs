using System.Collections;
using ColorMelt.Core;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Hard and Boss levels open with a ribbon ("HARD LEVEL", reward xN)
    /// that pops in, holds and flies away, and keep the level pill in the
    /// HUD tinted with their colour and icon for the whole level.
    /// </summary>
    public class DifficultyBanner : MonoBehaviour
    {
        [SerializeField] private LevelController level;

        [Header("Banner")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private Image ribbon;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField, Min(0.1f)] private float holdTime = 1.4f;

        [Header("Hard")]
        [SerializeField] private Sprite hardRibbon;
        [SerializeField] private Sprite hardIcon;
        [SerializeField] private Color hardPill = new Color(1f, 0.49f, 0.6f);

        [Header("Boss")]
        [SerializeField] private Sprite bossRibbon;
        [SerializeField] private Sprite bossIcon;
        [SerializeField] private Color bossPill = new Color(0.68f, 0.53f, 1f);

        [Header("HUD level pill")]
        [SerializeField] private Image levelPill;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image levelPillIcon;

        private void Awake()
        {
            if (banner != null) banner.gameObject.SetActive(false);
            if (levelPillIcon != null) levelPillIcon.gameObject.SetActive(false);
        }

        private void OnEnable() => level.Started += OnStarted;
        private void OnDisable() => level.Started -= OnStarted;

        private void OnStarted()
        {
            var difficulty = level.Level.difficulty;
            if (difficulty == LevelDifficulty.Normal) return;

            var boss = difficulty == LevelDifficulty.Boss;
            var pillColor = boss ? bossPill : hardPill;
            if (levelPill != null) levelPill.color = pillColor;
            if (levelText != null) levelText.color = Color.white;
            if (levelPillIcon != null)
            {
                levelPillIcon.sprite = boss ? bossIcon : hardIcon;
                levelPillIcon.gameObject.SetActive(true);
            }

            if (banner == null) return;
            if (ribbon != null) ribbon.sprite = boss ? bossRibbon : hardRibbon;
            if (icon != null) icon.sprite = boss ? bossIcon : hardIcon;
            if (title != null) title.text = Localization.Get("difficulty." + difficulty);
            if (subtitle != null)
                subtitle.text = Localization.Format("difficulty.reward",
                    GameConfig.Instance.RewardMultiplier(difficulty).ToString("0.#"));
            StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            // Wait for the scene transition's paint to drain, or nobody sees the banner.
            while (SceneFader.IsTransitioning) yield return null;
            yield return new WaitForSecondsRealtime(0.1f);

            banner.gameObject.SetActive(true);
            var group = banner.GetComponent<CanvasGroup>();
            if (group == null) group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var rest = banner.anchoredPosition;
            AudioManager.PlayPop();
            Haptics.Medium();

            const float popTime = 0.35f;
            for (var time = 0f; time < popTime; time += Time.unscaledDeltaTime)
            {
                var t = time / popTime;
                group.alpha = Mathf.Clamp01(t * 3f);
                banner.localScale = Vector3.one * EaseOutBack(t);
                banner.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-8f, 0f, t));
                yield return null;
            }
            banner.localScale = Vector3.one;
            banner.localRotation = Quaternion.identity;

            for (var time = 0f; time < holdTime; time += Time.unscaledDeltaTime)
            {
                banner.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(time * 6f));
                yield return null;
            }

            const float leaveTime = 0.3f;
            for (var time = 0f; time < leaveTime; time += Time.unscaledDeltaTime)
            {
                var t = time / leaveTime;
                group.alpha = 1f - t;
                banner.anchoredPosition = rest + Vector2.up * (220f * t * t);
                banner.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, t);
                yield return null;
            }

            banner.anchoredPosition = rest;
            banner.gameObject.SetActive(false);
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
