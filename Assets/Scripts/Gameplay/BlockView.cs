using System.Collections;
using ColorMelt.Core;
using ColorMelt.UI;
using UnityEngine;

namespace ColorMelt.Gameplay
{
    /// <summary>
    /// A coloured block on a channel. Idles with a soft bob, shakes when the
    /// wrong paint reaches it and melts with a squash, splash and sound.
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

        [Header("Idle")]
        [SerializeField, Min(0f)] private float bobHeight = 0.05f;
        [SerializeField, Min(0f)] private float bobSpeed = 2.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private ColorType _color;
        private Vector3 _restPosition;
        private Vector3 _baseScale;
        private float _phase;
        private bool _melting;
        private Coroutine _nudge;

        public ColorType Color => _color;
        public bool IsMelted { get; private set; }

        public void Configure(ColorType color)
        {
            _color = color;
            _restPosition = transform.localPosition;
            _baseScale = transform.localScale;
            _phase = Random.value * Mathf.PI * 2f;

            if (colorRenderer == null)
                colorRenderer = GetComponentInChildren<Renderer>();
            if (colorRenderer == null) return;

            var block = new MaterialPropertyBlock();
            colorRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color.ToUnityColor());
            block.SetColor(ColorId, color.ToUnityColor());
            colorRenderer.SetPropertyBlock(block);
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

        public void Melt()
        {
            if (_melting || IsMelted) return;
            if (_nudge != null) StopCoroutine(_nudge);
            StartCoroutine(MeltRoutine());
        }

        private IEnumerator MeltRoutine()
        {
            _melting = true;
            AudioManager.PlayClip(meltSound, meltVolume);
            SpawnSplash();

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
            gameObject.SetActive(false);
        }

        private void SpawnSplash()
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
            main.startColor = _color.ToUnityColor();
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
