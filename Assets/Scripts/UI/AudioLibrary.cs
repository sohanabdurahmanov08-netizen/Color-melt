using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>Clips used by the UI and the game. Lives in Resources/AudioLibrary.</summary>
    [CreateAssetMenu(menuName = "Color Melt/Audio Library", fileName = "AudioLibrary")]
    public class AudioLibrary : ScriptableObject
    {
        public AudioClip click;
        public AudioClip pop;
        public AudioClip pour;
        public AudioClip win;
        public AudioClip lose;
        public AudioClip coin;
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.45f;
    }
}
