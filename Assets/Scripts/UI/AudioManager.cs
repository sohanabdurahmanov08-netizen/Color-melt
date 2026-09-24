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
        private static AudioManager _instance;
        private AudioLibrary _library;
        private AudioSource _effects;
        private AudioSource _music;

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

        private void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null) _effects.PlayOneShot(clip, volume);
        }
    }
}
