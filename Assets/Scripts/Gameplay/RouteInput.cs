using System;
using System.Collections.Generic;
using ColorMelt.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// Two-tap control: tap a channel to pick up its paint, tap a pulsing
    /// neighbour to pour into it. Tapping the picked channel again sends its
    /// paint back home; tapping empty space cancels.
    ///
    /// Channels are hit-tested in screen space against their renderer bounds,
    /// so the thin models need no colliders and stay easy to hit.
    /// </summary>
    public class RouteInput : MonoBehaviour
    {
        [SerializeField] private LevelController level;
        [Tooltip("Farthest a tap may land from a channel's centre line and still pick it, as a share of " +
                 "screen width. Between two channels the nearer one always wins.")]
        [SerializeField, Range(0.02f, 0.3f)] private float tapReach = 0.13f;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private RouteView _selected;
        private Move? _hint;
        private Func<RouteView, bool> _canPick;
        private Action<RouteView> _onPicked;

        /// <summary>Optional gate, e.g. the tutorial allowing only one channel.</summary>
        public Func<RouteView, bool> TapFilter { get; set; }

        public event Action<RouteView> Selected;
        public event Action Deselected;
        /// <summary>Pick mode started (true) or ended (false).</summary>
        public event Action<bool> PickingChanged;

        public RouteView SelectedRoute => _selected;

        /// <summary>True while the next tap picks a channel for a booster.</summary>
        public bool IsPicking => _onPicked != null;

        private void OnEnable()
        {
            level.Poured += OnPoured;
            level.Won += OnLevelEnded;
            level.Lost += OnLevelEnded;
        }

        private void OnDisable()
        {
            level.Poured -= OnPoured;
            level.Won -= OnLevelEnded;
            level.Lost -= OnLevelEnded;
        }

        private void OnPoured(Move move) => ClearHint();

        private void OnLevelEnded(LevelResult _) => OnLevelEnded();

        private void OnLevelEnded()
        {
            ClearHint();
            CancelPick();
            Select(null);
        }

        /// <summary>
        /// The next tap on a channel accepted by canPick calls onPicked
        /// instead of picking up paint. Tapping elsewhere cancels.
        /// </summary>
        public void BeginPick(Func<RouteView, bool> canPick, Action<RouteView> onPicked)
        {
            Select(null);
            _hint = null;
            _canPick = canPick;
            _onPicked = onPicked;
            RefreshHighlights();
            PickingChanged?.Invoke(true);
        }

        public void CancelPick()
        {
            if (_onPicked == null) return;
            _onPicked = null;
            _canPick = null;
            RefreshHighlights();
            PickingChanged?.Invoke(false);
        }

        private void Update()
        {
            if (level.Routes.Count == 0 || !TryGetPress(out var screenPosition)) return;
            if (IsOverButton(screenPosition)) return;
            // The camera pad only moves the camera; it never picks a channel.
            if (level.Pan != null && level.Pan.Contains(screenPosition)) return;

            HandleTap(FindRouteAt(screenPosition));
        }

        public void HandleTap(RouteView tapped)
        {
            if (level.IsBusy) return;

            if (IsPicking)
            {
                if (tapped == null)
                {
                    CancelPick();
                }
                else if (_canPick == null || _canPick(tapped))
                {
                    var picked = _onPicked;
                    CancelPick();
                    picked(tapped);
                }
                return;
            }

            if (tapped != null && TapFilter != null && !TapFilter(tapped)) return;

            if (tapped == null)
            {
                Select(null);
                return;
            }

            if (_selected == null)
            {
                Select(tapped);
                Meta.Haptics.Light();
                return;
            }

            if (tapped == _selected)
            {
                // A second tap on the picked channel returns its paint home.
                var from = _selected.Index;
                Select(null);
                if (level.Model.TargetOf(from) != from)
                    level.TryPour(from, from);
                return;
            }

            if (level.CanPour(_selected.Index, tapped.Index))
            {
                var from = _selected.Index;
                Select(null);
                level.TryPour(from, tapped.Index);
                return;
            }

            // Not reachable from the current pick: pick the new channel instead.
            Select(tapped);
        }

        /// <summary>Drops the picked-up paint without pouring it.</summary>
        public void ClearSelection() => Select(null);

        public void ShowHint(Move move)
        {
            Select(null);
            _hint = move;
            RefreshHighlights();
        }

        public void ClearHint()
        {
            _hint = null;
            RefreshHighlights();
        }

        private void Select(RouteView route)
        {
            var previous = _selected;
            _selected = route;
            RefreshHighlights();

            if (route != null) Selected?.Invoke(route);
            else if (previous != null) Deselected?.Invoke();
        }

        private void RefreshHighlights()
        {
            foreach (var route in level.Routes)
            {
                var highlight = ChannelHighlight.None;
                if (IsPicking)
                {
                    if (_canPick == null || _canPick(route))
                        highlight = ChannelHighlight.Hint;
                }
                else if (_selected != null)
                {
                    if (route == _selected)
                        highlight = ChannelHighlight.Selected;
                    else if (level.CanPour(_selected.Index, route.Index))
                        highlight = ChannelHighlight.Target;
                }
                else if (_hint.HasValue)
                {
                    if (route.Index == _hint.Value.from)
                        highlight = ChannelHighlight.Hint;
                    else if (route.Index == _hint.Value.to)
                        highlight = ChannelHighlight.Target;
                }

                route.Channel.SetHighlight(highlight);
            }
        }

        private RouteView FindRouteAt(Vector2 screenPosition)
        {
            var cam = level.Camera;
            if (cam == null) return null;

            RouteView best = null;
            var bestDistance = tapReach * Screen.width;
            foreach (var route in level.Routes)
            {
                var distance = route.ScreenDistance(cam, screenPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = route;
                }
            }

            return best;
        }

        // Only real buttons swallow a tap; transparent panels must not block
        // the board.
        private bool IsOverButton(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            _uiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, _uiHits);
            foreach (var hit in _uiHits)
            {
                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable() && selectable.isActiveAndEnabled)
                    return true;
                // Open windows (win/lose/pause) block the board entirely.
                var group = hit.gameObject.GetComponentInParent<CanvasGroup>();
                if (group != null && group.blocksRaycasts && group.alpha > 0.01f)
                    return true;
            }

            return false;
        }

        private static bool TryGetPress(out Vector2 position)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }
    }
}
