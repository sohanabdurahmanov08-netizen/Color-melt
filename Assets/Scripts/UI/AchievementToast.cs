using System.Collections.Generic;
using ColorMelt.Meta;
using UnityEngine;

namespace ColorMelt.UI
{
    /// <summary>
    /// Announces new achievements with a toast. Unlocks reported in the same
    /// frame (a win can finish several tasks) become one message.
    /// </summary>
    public class AchievementToast : MonoBehaviour
    {
        private static AchievementToast _instance;
        private readonly List<AchievementData> _pending = new List<AchievementData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            Achievements.Unlocked -= OnUnlocked;
            Achievements.Unlocked += OnUnlocked;
        }

        private static void OnUnlocked(IReadOnlyList<AchievementData> unlocked)
        {
            if (_instance == null)
            {
                var host = new GameObject(nameof(AchievementToast));
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<AchievementToast>();
            }
            _instance._pending.AddRange(unlocked);
            _instance.enabled = true;
        }

        private void LateUpdate()
        {
            if (_pending.Count > 0)
            {
                Toast.Show(_pending.Count == 1
                    ? Localization.Format("ach.toast", Localization.Get("ach." + _pending[0].id))
                    : Localization.Format("ach.toast_many", _pending.Count));
                AudioManager.PlayPop();
                _pending.Clear();
            }
            enabled = false;
        }
    }
}
