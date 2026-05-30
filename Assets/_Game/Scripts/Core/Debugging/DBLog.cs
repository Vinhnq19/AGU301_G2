using System.Collections.Generic;
using UnityEngine;

namespace DungeonBuilder.Core.Debugging
{
    public static class DBLog
    {
        public static bool Enabled = true;
        public static float DefaultInterval = 0.5f;

        private static readonly Dictionary<string, float> LastLogTimeByKey = new();

        public static void Info(string key, string message, float interval = -1f, Object context = null)
        {
            if (!Enabled || IsThrottled(key, interval))
            {
                return;
            }

            Debug.Log($"[DB][{Time.frameCount}][{Time.time:0.00}] {message}", context);
        }

        public static void Warning(string key, string message, float interval = -1f, Object context = null)
        {
            if (!Enabled || IsThrottled(key, interval))
            {
                return;
            }

            Debug.LogWarning($"[DB][{Time.frameCount}][{Time.time:0.00}] {message}", context);
        }

        private static bool IsThrottled(string key, float interval)
        {
            float effectiveInterval = interval >= 0f ? interval : DefaultInterval;
            if (effectiveInterval <= 0f)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (LastLogTimeByKey.TryGetValue(key, out float lastLogTime) && now - lastLogTime < effectiveInterval)
            {
                return true;
            }

            LastLogTimeByKey[key] = now;
            return false;
        }
    }
}
