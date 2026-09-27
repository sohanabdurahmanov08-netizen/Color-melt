using System.Collections.Generic;
using ColorMelt.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMelt.UI
{
    /// <summary>
    /// Living background for a result window: slowly turning light rays, a
    /// breathing glow, paint drips that run down from the top edge when the
    /// window opens, and falling paint drops. Runs only while the window is
    /// open, so a closed window costs nothing per frame.
    /// </summary>
    public class WindowDecor : MonoBehaviour
    {
        private static readonly ColorType[] DropColours =
        {
            ColorType.Red, ColorType.Orange, ColorType.Yellow, ColorType.Green, ColorType.Blue,
            ColorType.Purple, ColorType.Pink, ColorType.SkyBlue, ColorType.Mint
        };

        [SerializeField] private UIWindow window;

        [Header("Rays and glow")]
        [SerializeField] private RectTransform rays;
        [SerializeField] private float raySpeed = 10f;
        [SerializeField] private RectTransform glow;

        [Header("Drips")]
        [Tooltip("Paint band along the top edge; pivot at the top so it grows downwards.")]
        [SerializeField] private RectTransform drips;
        [SerializeField, Min(0.05f)] private float dripTime = 0.9f;

        [Header("Falling drops")]
        [SerializeField] private RectTransform dropArea;
        [SerializeField] private Sprite dropSprite;
        [SerializeField, Min(0)] private int dropCount = 16;
        [Tooltip("Random paint colours; otherwise every drop uses Drop Colour.")]
        [SerializeField] private bool colourfulDrops = true;
        [SerializeField] private Color dropColour = Color.white;
        [SerializeField] private Vector2 dropSpeed = new Vector2(250f, 520f);
        [SerializeField] private Vector2 dropSize = new Vector2(34f, 64f);

        private readonly List<Drop> _drops = new List<Drop>();
        private float _openTime;

        private sealed class Drop
        {
            public RectTransform rect;
            public float speed;
            public float sway;
            public float phase;
        }

        private void Awake()
        {
            if (window == null) window = GetComponent<UIWindow>();
            if (window != null)
            {
                window.Opened += OnOpened;
                window.Closed += OnClosed;
            }
            enabled = false;
        }

        private void OnDestroy()
        {
            if (window == null) return;
            window.Opened -= OnOpened;
            window.Closed -= OnClosed;
        }

        private void OnOpened()
        {
            _openTime = Time.unscaledTime;
            if (_drops.Count == 0) CreateDrops();
            for (var index = 0; index < _drops.Count; index++)
                Respawn(_drops[index], true);
            if (drips != null) drips.localScale = new Vector3(1f, 0f, 1f);
            enabled = true;
        }

        private void OnClosed() => enabled = false;

        private void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var age = Time.unscaledTime - _openTime;

            if (rays != null)
                rays.Rotate(0f, 0f, -raySpeed * dt);
            if (glow != null)
                glow.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(age * 2.2f));
            if (drips != null)
            {
                // Paint runs down fast, then keeps stretching a little.
                var t = Mathf.Clamp01(age / dripTime);
                var stretch = 1f - Mathf.Pow(1f - t, 3f);
                drips.localScale = new Vector3(1f, stretch * (1f + 0.04f * Mathf.Sin(age * 1.3f)), 1f);
            }

            if (dropArea == null) return;
            var bottom = -dropArea.rect.height * 0.5f - dropSize.y;
            foreach (var drop in _drops)
            {
                var position = drop.rect.anchoredPosition;
                position.y -= drop.speed * dt;
                position.x += Mathf.Sin(age * 2f + drop.phase) * drop.sway * dt;
                drop.rect.anchoredPosition = position;
                if (position.y < bottom) Respawn(drop, false);
            }
        }

        private void CreateDrops()
        {
            if (dropArea == null || dropSprite == null) return;
            for (var index = 0; index < dropCount; index++)
            {
                var image = new GameObject("Drop", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.transform.SetParent(dropArea, false);
                image.sprite = dropSprite;
                image.raycastTarget = false;
                image.preserveAspect = true;
                image.color = colourfulDrops ? DropColours[index % DropColours.Length].ToUnityColor() : dropColour;
                _drops.Add(new Drop { rect = image.rectTransform });
            }
        }

        private void Respawn(Drop drop, bool anywhere)
        {
            var area = dropArea.rect;
            var size = Random.Range(dropSize.x, dropSize.y);
            drop.rect.sizeDelta = new Vector2(size, size * 1.5f);
            drop.speed = Random.Range(dropSpeed.x, dropSpeed.y);
            drop.sway = Random.Range(10f, 40f);
            drop.phase = Random.value * Mathf.PI * 2f;
            var top = area.height * 0.5f + size * 1.5f;
            var y = anywhere ? Random.Range(-area.height * 0.5f, top) : top;
            drop.rect.anchoredPosition = new Vector2(Random.Range(-0.46f, 0.46f) * area.width, y);
        }
    }
}
