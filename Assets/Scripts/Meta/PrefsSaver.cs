using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Batches PlayerPrefs writes. PlayerPrefs.Save writes to disk and can
    /// stall a frame on phones, and melts pay coins several times per pour,
    /// so frequent changes call Request and are saved at most once per
    /// interval, plus right away when the app is paused, loses focus or quits.
    /// </summary>
    public sealed class PrefsSaver : MonoBehaviour
    {
        private const float Interval = 1.5f;

        private static PrefsSaver _instance;
        private static bool _dirty;
        private float _due;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (_instance != null) return;
            var host = new GameObject(nameof(PrefsSaver)) { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<PrefsSaver>();
        }

        /// <summary>Saves PlayerPrefs soon, together with other changes made meanwhile.</summary>
        public static void Request()
        {
            if (_instance == null)
            {
                // Edit mode, or before the first scene: nothing to batch with.
                PlayerPrefs.Save();
                return;
            }
            if (!_dirty) _instance._due = Time.unscaledTime + Interval;
            _dirty = true;
        }

        /// <summary>Writes pending changes now.</summary>
        public static void Flush()
        {
            if (!_dirty) return;
            _dirty = false;
            PlayerPrefs.Save();
        }

        private void LateUpdate()
        {
            if (_dirty && Time.unscaledTime >= _due) Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void OnDestroy()
        {
            Flush();
            if (_instance == this) _instance = null;
        }
    }
}
