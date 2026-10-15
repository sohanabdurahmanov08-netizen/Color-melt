using UnityEngine;

namespace ColorMelt.Meta
{
    /// <summary>
    /// Haptic feedback in three strengths. On Android a short one-shot with
    /// an amplitude is used (API 26+), so a light tap stays light; iOS has no
    /// strength control without a plugin, so only Heavy buzzes there.
    /// Respects the vibration setting.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Picking up paint, pouring.</summary>
        public static void Light() => Vibrate(12, 50);

        /// <summary>A block melting.</summary>
        public static void Medium() => Vibrate(25, 130);

        /// <summary>Combos, a win, a booster firing.</summary>
        public static void Heavy() => Vibrate(55, 255);

        /// <summary>Strength that grows with the combo step (1 = first melt of a pour).</summary>
        public static void Chain(int step)
        {
            if (step <= 1) Medium();
            else Vibrate(20 + 10 * Mathf.Min(step, 5), Mathf.Min(255, 130 + 40 * step));
        }

        public static void Impact() => Heavy();

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk;

        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (!Progress.VibrationOn) return;
            try
            {
                if (_vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                }

                if (_sdk >= 26)
                {
                    using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", milliseconds,
                               Mathf.Clamp(amplitude, 1, 255)))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception)
            {
                // Also keeps the VIBRATE permission in the Android manifest.
                Handheld.Vibrate();
            }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        private static void Vibrate(long milliseconds, int amplitude)
        {
            if (Progress.VibrationOn && amplitude >= 200)
                Handheld.Vibrate();
        }
#else
        private static void Vibrate(long milliseconds, int amplitude)
        {
        }
#endif
    }
}
