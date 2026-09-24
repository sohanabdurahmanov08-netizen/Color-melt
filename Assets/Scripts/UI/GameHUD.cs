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
    /// In-game HUD: level number, moves with a punch on every pour, the hint
    /// booster, restart, the pause window and praise pop-ups on melts.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private static readonly string[] Praises = { "Nice!", "Great!", "Splash!", "Awesome!", "Sweet!" };

        [SerializeField] private LevelController level;
        [SerializeField] private RouteInput input;

        [Header("Labels")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text movesText;
        [SerializeField] private string movesPrefix = "MOVES: ";
        [SerializeField] private Color lowMovesColor = new Color(1f, 0.35f, 0.35f);

        [Header("Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button hintButton;
        [SerializeField] private TMP_Text hintLabel;

        [Header("Pause window")]
        [SerializeField] private UIWindow pauseWindow;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button pauseMenuButton;

        [Header("Praise")]
        [SerializeField] private TMP_Text praiseText;

        private Color _movesColor = Color.white;
        private Coroutine _movesPunch;
        private Coroutine _praise;
        private Vector2 _praisePosition;

        private void Awake()
        {
            if (movesText != null) _movesColor = movesText.color;
            if (praiseText != null)
            {
                _praisePosition = praiseText.rectTransform.anchoredPosition;
                praiseText.gameObject.SetActive(false);
            }

            pauseButton?.onClick.AddListener(() => pauseWindow?.Open());
            resumeButton?.onClick.AddListener(() => pauseWindow?.Close());
            restartButton?.onClick.AddListener(GameSession.Restart);
            pauseRestartButton?.onClick.AddListener(GameSession.Restart);
            pauseMenuButton?.onClick.AddListener(GameSession.ToMenu);
            hintButton?.onClick.AddListener(UseHint);
        }

        private void OnEnable()
        {
            level.Started += OnStarted;
            level.MovesChanged += OnMovesChanged;
            level.BlockMelted += OnBlockMelted;
            level.Poured += OnPoured;
            Progress.HintsChanged += OnHintsChanged;
            Progress.CoinsChanged += OnHintsChanged;
        }

        private void OnDisable()
        {
            level.Started -= OnStarted;
            level.MovesChanged -= OnMovesChanged;
            level.BlockMelted -= OnBlockMelted;
            level.Poured -= OnPoured;
            Progress.HintsChanged -= OnHintsChanged;
            Progress.CoinsChanged -= OnHintsChanged;
        }

        private void OnStarted()
        {
            if (levelText != null) levelText.text = $"Level {level.LevelIndex + 1}";
            // The tutorial level teaches the moves itself.
            if (hintButton != null) hintButton.gameObject.SetActive(!level.Level.tutorial);
            RefreshHint();
        }

        private void OnMovesChanged(int moves)
        {
            if (movesText == null) return;
            movesText.text = movesPrefix + moves;
            movesText.color = moves <= 1 ? lowMovesColor : _movesColor;
            if (_movesPunch != null) StopCoroutine(_movesPunch);
            _movesPunch = StartCoroutine(Punch(movesText.rectTransform, 0.25f));
        }

        private void OnPoured(Move move) => AudioManager.PlayPour();

        private void OnBlockMelted(RouteView route, BlockView block)
        {
            if (praiseText == null) return;
            praiseText.text = $"{Praises[Random.Range(0, Praises.Length)]}\n<size=70%>+{GameConfig.Instance.coinsPerBlock}</size>";
            if (_praise != null) StopCoroutine(_praise);
            _praise = StartCoroutine(PraiseRoutine());
        }

        private void OnHintsChanged(int _) => RefreshHint();

        private void RefreshHint()
        {
            if (hintLabel == null) return;
            hintLabel.text = Progress.Hints > 0
                ? $"HINT\n<size=70%>x{Progress.Hints}</size>"
                : $"HINT\n<size=70%>{GameConfig.Instance.hintCost} coins</size>";
        }

        private void UseHint()
        {
            if (TutorialController.IsRunning || level.IsBusy) return;

            var hint = level.FindHint();
            if (!hint.HasValue)
            {
                Toast.Show(level.MovesLeft > 0 ? "No solution from here. Try restart!" : "No moves left");
                return;
            }

            if (Progress.Hints > 0)
                Progress.AddHints(-1);
            else if (!Progress.TrySpendCoins(GameConfig.Instance.hintCost))
            {
                Toast.Show("Not enough coins");
                return;
            }

            input.ShowHint(hint.Value);
        }

        private IEnumerator PraiseRoutine()
        {
            praiseText.gameObject.SetActive(true);
            var rect = praiseText.rectTransform;
            var start = _praisePosition;
            const float duration = 0.9f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var t = time / duration;
                rect.localScale = Vector3.one * (t < 0.2f ? Mathf.Lerp(0.3f, 1.15f, t / 0.2f) : Mathf.Lerp(1.15f, 1f, (t - 0.2f) / 0.8f));
                rect.anchoredPosition = start + Vector2.up * (60f * t);
                praiseText.alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                yield return null;
            }
            rect.anchoredPosition = start;
            praiseText.gameObject.SetActive(false);
            _praise = null;
        }

        private static IEnumerator Punch(RectTransform target, float duration)
        {
            for (var time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                target.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(time / duration * Mathf.PI));
                yield return null;
            }
            target.localScale = Vector3.one;
        }
    }
}
