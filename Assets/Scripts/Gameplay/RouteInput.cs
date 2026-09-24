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
        [Tooltip("Extra tap area around each channel, as a fraction of screen height.")]
        [SerializeField, Range(0f, 0.2f)] private float tapPadding = 0.04f;

        private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        private RouteView _selected;
        private Move? _hint;

        /// <summary>Optional gate, e.g. the tutorial allowing only one channel.</summary>
        public Func<RouteView, bool> TapFilter { get; set; }

        public event Action<RouteView> Selected;
        public event Action Deselected;

        public RouteView SelectedRoute => _selected;

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
            Select(null);
        }

        private void Update()
        {
            if (level.Routes.Count == 0 || !TryGetPress(out var screenPosition)) return;
            if (IsOverButton(screenPosition)) return;

            HandleTap(FindRouteAt(screenPosition));
        }

        public void HandleTap(RouteView tapped)
        {
            if (level.IsBusy) return;
            if (tapped != null && TapFilter != null && !TapFilter(tapped)) return;

            if (tapped == null)
            {
                Select(null);
                return;
            }

            if (_selected == null)
            {
                Select(tapped);
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
                if (_selected != null)
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

            var padding = tapPadding * Screen.height;
            RouteView best = null;
            var bestScore = float.MaxValue;

            foreach (var route in level.Routes)
            {
                if (!route.TryGetScreenRect(cam, out var rect)) continue;

                var dx = Mathf.Max(rect.xMin - screenPosition.x, 0f, screenPosition.x - rect.xMax);
                var dy = Mathf.Max(rect.yMin - screenPosition.y, 0f, screenPosition.y - rect.yMax);
                var distance = new Vector2(dx, dy).magnitude;
                if (distance > padding) continue;

                // Channels stand side by side, so overlapping padded rects
                // are resolved by the distance to each channel's centre line.
                var centreDistance = rect.width >= rect.height
                    ? Mathf.Abs(screenPosition.y - rect.center.y)
                    : Mathf.Abs(screenPosition.x - rect.center.x);
                var score = distance * 1000f + centreDistance;
                if (score < bestScore)
                {
                    bestScore = score;
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
