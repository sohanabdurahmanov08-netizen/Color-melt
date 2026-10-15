using System.Collections;
using ColorMelt.Core;
using ColorMelt.UI;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// A coloured block on a channel. Idles with a soft bob, shakes when the
    /// wrong paint reaches it and melts with a squash, splash and sound.
    ///
    /// A layered block shows its inner colour as a smaller core sticking out
    /// of the top. When the outer layer melts the shell splashes away and the
    /// core swells to full size, now a plain block of the inner colour.
    /// </summary>
    public class BlockView : MonoBehaviour
    {
        [SerializeField] private Renderer colorRenderer;

        [Header("Melt")]
        [SerializeField, Min(0.05f)] private float meltDuration = 0.35f;
        [SerializeField] private AudioClip meltSound;
        [SerializeField, Range(0f, 1f)] private float meltVolume = 0.8f;
        [SerializeField] private Material splashMaterial;
        [SerializeField, Min(0)] private int splashParticles = 28;

        [Header("Layers")]
        [Tooltip("Size of the inner core relative to the block.")]
        [SerializeField, Range(0.3f, 0.9f)] private float coreScale = 0.62f;
        [Tooltip("How far the core sticks out above the block's top, in block heights.")]
        [SerializeField, Range(0f, 0.4f)] private float coreRise = 0.14f;
        [SerializeField, Min(0.05f)] private float peelDuration = 0.3f;

        [Header("Idle")]
        [SerializeField, Min(0f)] private float bobHeight = 0.05f;
        [SerializeField, Min(0f)] private float bobSpeed = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private ColorType _outer;
        private ColorType _inner;
        private ColorType _color;
        private Renderer _core;
        private Vector3 _restPosition;
        private Vector3 _baseScale;
        private float _phase;
        private bool _melting;
        private bool _configured;
        private Coroutine _nudge;
        private Coroutine _animation;

        public ColorType Color => _color;
        public bool IsMelted { get; private set; }
        public bool IsPeeled => _inner != ColorType.None && _color == _inner;

        public void Configure(ColorType color, ColorType inner = ColorType.None)
        {
            if (!_configured)
            {
                _restPosition = transform.localPosition;
                _baseScale = transform.localScale;
                _phase = Random.value * Mathf.PI * 2f;
                _configured = true;
            }

            if (colorRenderer == null)
                colorRenderer = GetComponentInChildren<Renderer>();

            _outer = color;
            _inner = inner;
            if (inner != ColorType.None) CreateCore();
            SetState(peeled: false, melted: false);
        }

        /// <summary>Shows the block as the model has it (start of level or undo).</summary>
        public void SetState(bool peeled, bool melted)
        {
            if (_nudge != null) StopCoroutine(_nudge);
            if (_animation != null) StopCoroutine(_animation);
            _nudge = _animation = null;
            _melting = false;

            IsMelted = melted;
            gameObject.SetActive(!melted);
            transform.localPosition = _restPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = _baseScale;

            var layered = _inner != ColorType.None;
            _color = layered && peeled ? _inner : _outer;
            Paint(colorRenderer, _color);
            if (_core != null)
            {
                _core.gameObject.SetActive(layered && !peeled);
                Paint(_core, _inner);
            }
        }

        private void CreateCore()
        {
            if (_core != null || colorRenderer == null) return;

            var sourceMesh = colorRenderer.GetComponent<MeshFilter>();
            if (sourceMesh == null) return;

            var core = new GameObject("Core", typeof(MeshFilter), typeof(MeshRenderer));
            core.transform.SetParent(colorRenderer.transform, false);
            core.GetComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
            var renderer = core.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = colorRenderer.sharedMaterials;
            renderer.shadowCastingMode = colorRenderer.shadowCastingMode;
            renderer.receiveShadows = colorRenderer.receiveShadows;

            // The block mesh is a unit cube centred on its pivot: sit the core
            // inside it with its top poking out.
            core.transform.localScale = Vector3.one * coreScale;
            core.transform.localPosition = Vector3.up * (0.5f - coreScale * 0.5f + coreRise);
            _core = renderer;
        }

        private static void Paint(Renderer target, ColorType color)
        {
            if (target == null) return;
            var block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color.ToUnityColor());
            block.SetColor(ColorId, color.ToUnityColor());
            target.SetPropertyBlock(block);
        }

        private void Update()
        {
            if (_melting || IsMelted || _nudge != null) return;
            transform.localPosition = _restPosition +
                Vector3.up * (Mathf.Sin(Time.time * bobSpeed + _phase) * bobHeight);
        }

        /// <summary>Quick "no" shake when paint of the wrong colour arrives.</summary>
        public void Nudge()
        {
            if (_melting || IsMelted) return;
            if (_nudge != null) StopCoroutine(_nudge);
            _nudge = StartCoroutine(NudgeRoutine());
        }

        private IEnumerator NudgeRoutine()
        {
            const float duration = 0.3f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var strength = 1f - time / duration;
                transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 60f) * 12f * strength, 0f);
                yield return null;
            }
            transform.localRotation = Quaternion.identity;
            _nudge = null;
        }

        /// <summary>Melts the whole block. Pitch rises with each step of a combo.</summary>
        public void Melt(float pitch = 1f)
        {
            if (_melting || IsMelted) return;
            if (_nudge != null) StopCoroutine(_nudge);
            _nudge = null;
            _animation = StartCoroutine(MeltRoutine(pitch));
        }

        /// <summary>Melts the outer layer; the core grows into a block of the inner colour.</summary>
        public void Peel(float pitch = 1f)
        {
            if (_melting || IsMelted || _inner == ColorType.None) return;
            if (_nudge != null) StopCoroutine(_nudge);
            _nudge = null;
            _animation = StartCoroutine(PeelRoutine(pitch));
        }

        private IEnumerator MeltRoutine(float pitch)
        {
            _melting = true;
            AudioManager.PlayClip(meltSound, meltVolume, pitch);
            SpawnSplash(_color);

            for (var time = 0f; time < meltDuration; time += Time.deltaTime)
            {
                var t = time / meltDuration;
                // Squash down and spread out like a drop hitting the floor.
                var squash = new Vector3(1f + 0.6f * t, 1f - t, 1f + 0.6f * t) * (1f - t * t);
                transform.localScale = Vector3.Scale(_baseScale, squash);
                yield return null;
            }

            transform.localScale = Vector3.zero;
            IsMelted = true;
            _melting = false;
            _animation = null;
            gameObject.SetActive(false);
        }

        private IEnumerator PeelRoutine(float pitch)
        {
            _melting = true;
            AudioManager.PlayClip(meltSound, meltVolume, pitch);
            SpawnSplash(_outer);

            // The shell squashes away around the core...
            var half = peelDuration * 0.5f;
            for (var time = 0f; time < half; time += Time.deltaTime)
            {
                var t = time / half;
                var shell = Mathf.Lerp(1f, coreScale, t);
                transform.localScale = _baseScale * shell;
                if (_core != null) _core.transform.localScale = Vector3.one * (coreScale / shell);
                yield return null;
            }

            // ...and the core takes its place, swelling with a little overshoot.
            _color = _inner;
            Paint(colorRenderer, _inner);
            if (_core != null) _core.gameObject.SetActive(false);
            for (var time = 0f; time < half; time += Time.deltaTime)
            {
                var t = time / half;
                var overshoot = 1f + 0.18f * Mathf.Sin(t * Mathf.PI);
                transform.localScale = _baseScale * (Mathf.Lerp(coreScale, 1f, t) * overshoot);
                yield return null;
            }

            transform.localScale = _baseScale;
            if (_core != null) _core.transform.localScale = Vector3.one * coreScale;
            _melting = false;
            _animation = null;
        }

        private void SpawnSplash(ColorType color)
        {
            if (splashMaterial == null || splashParticles <= 0) return;

            var splash = new GameObject("Splash");
            splash.transform.position = transform.position;
            var particles = splash.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var size = transform.lossyScale.x;
            var main = particles.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * size, 6f * size);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * size, 0.35f * size);
            main.startColor = color.ToUnityColor();
            main.gravityModifier = 1.5f * size;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)splashParticles) });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f * size;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            splash.GetComponent<ParticleSystemRenderer>().sharedMaterial = splashMaterial;
            particles.Play();
        }
    }
}
