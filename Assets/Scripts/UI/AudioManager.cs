using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Persistent sound player: one-shot effects plus the looping music.
    /// Created on first use and kept across scenes. Honours the Sound and
    /// Music toggles in Progress.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // One-shots at another pitch need sources of their own.
        private const int PitchedVoices = 4;

        private static AudioManager _instance;
        private AudioLibrary _library;
        private AudioSource _effects;
        private AudioSource _music;
        private AudioSource[] _pitched;
        private int _nextPitched;

        private static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var root = new GameObject("Audio Manager");
                    DontDestroyOnLoad(root);
                    _instance = root.AddComponent<AudioManager>();
                }
                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Start the music as soon as the game launches.
            Instance.ApplySettings();
        }

        private void Awake()
        {
            _library = Resources.Load<AudioLibrary>("AudioLibrary");
            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;

            _pitched = new AudioSource[PitchedVoices];
            for (var index = 0; index < PitchedVoices; index++)
            {
                _pitched[index] = gameObject.AddComponent<AudioSource>();
                _pitched[index].playOnAwake = false;
            }
        }

        public static void ApplySettingsNow() => Instance.ApplySettings();

        private void ApplySettings()
        {
            AudioListener.volume = Progress.SoundOn ? 1f : 0f;

            if (_library == null || _library.music == null) return;
            _music.clip = _library.music;
            _music.volume = _library.musicVolume;
            if (Progress.MusicOn && !_music.isPlaying) _music.Play();
            else if (!Progress.MusicOn) _music.Stop();
        }

        public static void PlayClick() => Instance.Play(Instance._library != null ? Instance._library.click : null);
        public static void PlayPop() => Instance.Play(Instance._library != null ? Instance._library.pop : null, 0.7f);
        public static void PlayPour() => Instance.Play(Instance._library != null ? Instance._library.pour : null, 0.8f);
        public static void PlayWin() => Instance.Play(Instance._library != null ? Instance._library.win : null);
        public static void PlayLose() => Instance.Play(Instance._library != null ? Instance._library.lose : null);
        public static void PlayCoin() => Instance.Play(Instance._library != null ? Instance._library.coin : null, 0.7f);

        /// <summary>Plays any clip as a 2D one-shot (world positions are far from the listener).</summary>
        public static void PlayClip(AudioClip clip, float volume = 1f) => Instance.Play(clip, volume);

        /// <summary>One-shot at another pitch, e.g. melts rising with each step of a combo.</summary>
        public static void PlayClip(AudioClip clip, float volume, float pitch)
        {
            if (Mathf.Approximately(pitch, 1f))
            {
                Instance.Play(clip, volume);
                return;
            }

            var manager = Instance;
            if (clip == null) return;
            var source = manager._pitched[manager._nextPitched];
            manager._nextPitched = (manager._nextPitched + 1) % manager._pitched.Length;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        /// <summary>The pour sound at a given pitch (paint filling the win jar).</summary>
        public static void PlayPour(float pitch) =>
            PlayClip(Instance._library != null ? Instance._library.pour : null, 0.6f, pitch);

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null) _effects.PlayOneShot(clip, volume);
        }
    }
}
