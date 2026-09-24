using System.Collections;
using ColorMelt.Gameplay;
using ColorMelt.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Victory window: stars pop in one by one, the coin reward counts up and
    /// can be doubled once through a rewarded ad.
    /// </summary>
    public class WinScreenUI : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [SerializeField] private UIWindow window;
        [SerializeField, Min(0f)] private float showDelay = 0.6f;

        [Header("Stars")]
        [SerializeField] private Image[] stars;
        [SerializeField] private Sprite starOn;
        [SerializeField] private Sprite starOff;

        [Header("Reward")]
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button doubleButton;

        [Header("Navigation")]
        [SerializeField] private Button nextButton;
        [SerializeField] private Button menuButton;

        private LevelResult _result;

        private void Awake()
        {
            nextButton?.onClick.AddListener(() =>
            {
                if (GameSession.HasNextLevel) GameSession.NextLevel();
                else GameSession.ToMenu();
            });
            menuButton?.onClick.AddListener(GameSession.ToMenu);
            doubleButton?.onClick.AddListener(DoubleReward);
        }

        private void OnEnable() => level.Won += OnWon;
        private void OnDisable() => level.Won -= OnWon;

        private void OnWon(LevelResult result)
        {
            _result = result;
            StartCoroutine(ShowRoutine());
        }

        private IEnumerator ShowRoutine()
        {
            yield return new WaitForSeconds(showDelay);

            foreach (var star in stars)
            {
                star.sprite = starOff;
                star.transform.localScale = Vector3.one;
            }
            if (rewardText != null) rewardText.text = "+0";
            if (doubleButton != null) doubleButton.gameObject.SetActive(false);

            window.Open();
            AudioManager.PlayWin();
            yield return new WaitForSecondsRealtime(0.35f);

            for (var index = 0; index < stars.Length && index < _result.stars; index++)
            {
                yield return PopStar(stars[index]);
                yield return new WaitForSecondsRealtime(0.12f);
            }

            yield return CountReward(0, _result.reward);

            if (doubleButton != null && RewardedAds.IsReady)
            {
                doubleButton.gameObject.SetActive(true);
                doubleButton.GetComponent<UIButtonFeedback>()?.Punch(6f);
            }
        }

        private IEnumerator PopStar(Image star)
        {
            star.sprite = starOn;
            AudioManager.PlayPop();
            const float duration = 0.3f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                var t = time / duration;
                star.transform.localScale = Vector3.one * (t < 0.6f ? Mathf.Lerp(0.2f, 1.35f, t / 0.6f) : Mathf.Lerp(1.35f, 1f, (t - 0.6f) / 0.4f));
                yield return null;
            }
            star.transform.localScale = Vector3.one;
        }

        private IEnumerator CountReward(int from, int to)
        {
            if (rewardText == null) yield break;
            const float duration = 0.5f;
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                rewardText.text = "+" + Mathf.RoundToInt(Mathf.Lerp(from, to, time / duration));
                yield return null;
            }
            rewardText.text = "+" + to;
        }

        private void DoubleReward()
        {
            doubleButton.interactable = false;
            RewardedAds.Show("win_double", () =>
            {
                Progress.AddCoins(_result.reward);
                doubleButton.gameObject.SetActive(false);
                StartCoroutine(CountReward(_result.reward, _result.reward * 2));
            }, () => doubleButton.interactable = true);
        }
    }
}
