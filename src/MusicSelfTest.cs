#if DEBUG
using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Debug-only check: alternates music between full and silent every second on the main menu
    /// and logs the loudness of everything the player hears. If the filter works, the silent
    /// seconds are clearly quieter.
    /// </summary>
    internal static class MusicSelfTest
    {
        private const float Duration = 16f;
        private static readonly float[] samples = new float[2048];
        private static float startedAt = -1f;
        private static int lastSecond = -1;
        private static float sum;
        private static int count;

        internal static void Update()
        {
            if (startedAt < 0f)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "player_apt")
                {
                    return;
                }
                startedAt = Time.unscaledTime + 3f; // let the scene settle
            }
            float t = Time.unscaledTime - startedAt;
            if (t < 0f || t > Duration)
            {
                return;
            }
            int second = (int)t;
            bool musicOn = second % 2 == 0;
            MusicVolumeFilter.Volume = musicOn ? 1f : 0f;

            AudioListener.GetOutputData(samples, 0);
            float frame = 0f;
            foreach (float s in samples)
            {
                frame += s * s;
            }
            sum += frame / samples.Length;
            count++;

            if (second != lastSecond)
            {
                if (lastSecond >= 0 && count > 0)
                {
                    bool wasOn = lastSecond % 2 == 0;
                    Plugin.Log.LogInfo("SELFTEST second " + lastSecond + " music " + (wasOn ? "ON " : "OFF") + " loudness " + Mathf.Sqrt(sum / count).ToString("0.0000"));
                }
                lastSecond = second;
                sum = 0f;
                count = 0;
            }
        }
    }
}
#endif
