using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Separate volume for music. The game's mixer has no music volume we can set, so we attach
    /// a small filter to every music source that scales its sound. This works no matter how the
    /// game fades its music (combat music fades in and out by changing the source's volume).
    /// </summary>
    internal static class MusicVolume
    {
        private const string MusicMixerGroup = "Diegetic Music";
        private const float ScanInterval = 2f;

        // Music that the game routes to the sound effects channel instead of the music channel.
        private static readonly string[] MusicClipNames = { "serenity 2 unfinished" };
        private const string ComposerTag = "(crisp)";

        private static float nextScan;
        private static readonly System.Collections.Generic.HashSet<int> skipped = new System.Collections.Generic.HashSet<int>();

        internal static void Update()
        {
            MusicVolumeFilter.Volume = Mathf.Clamp01(Plugin.MusicVolumeSetting.Value);
            if (Time.unscaledTime < nextScan)
            {
                return;
            }
            nextScan = Time.unscaledTime + ScanInterval;
            foreach (AudioSource source in Object.FindObjectsOfType<AudioSource>())
            {
                Tag(source);
            }
        }

        /// <summary>Tags music in the whole scene, including objects that are switched off for now (cutscenes).</summary>
        internal static void TagScene()
        {
            foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (source.gameObject.scene.IsValid())
                {
                    Tag(source);
                }
            }
        }

        private static bool IsMusic(AudioSource source)
        {
            if (source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name == MusicMixerGroup)
            {
                return true;
            }
            AudioClip clip = source.clip;
            if (clip == null)
            {
                return false;
            }
            if (clip.name.Contains(ComposerTag))
            {
                return true;
            }
            foreach (string name in MusicClipNames)
            {
                if (clip.name == name)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Tag(AudioSource source)
        {
            GameObject go = source.gameObject;
            if (go.GetComponent<MusicVolumeFilter>() != null || !IsMusic(source))
            {
                return;
            }
            // A filter affects everything on its object, so only tag objects that hold just this music.
            if (go.GetComponents<AudioSource>().Length != 1 || go.GetComponent<AudioListener>() != null)
            {
                if (skipped.Add(go.GetInstanceID()))
                {
                    Plugin.Log.LogWarning("Music source shares its object with other audio, not adjusted: " + go.scene.name + "/" + go.name);
                }
                return;
            }
            go.AddComponent<MusicVolumeFilter>();
            Plugin.Log.LogInfo("Music volume applies to: " + go.scene.name + "/" + go.name + " (" + (source.clip != null ? source.clip.name : "no clip") + ")");
        }
    }

    /// <summary>Scales the sound of the audio source on the same object by the music volume.</summary>
    internal sealed class MusicVolumeFilter : MonoBehaviour
    {
        // Written on the main thread, read on the audio thread.
        internal static volatile float Volume = 1f;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float volume = Volume;
            if (volume >= 0.999f)
            {
                return;
            }
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= volume;
            }
        }
    }
}
