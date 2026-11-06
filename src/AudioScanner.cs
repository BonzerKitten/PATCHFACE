using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.Audio;

namespace PatchFace
{
    /// <summary>
    /// Research tool: writes every audio source in the scene to PatchFace_audio.txt,
    /// and logs each long sound (likely music) the moment it starts playing.
    /// </summary>
    internal sealed class AudioScanner
    {
        private const float LongClipSeconds = 20f;
        private const float ScanInterval = 1f;

        private readonly string outPath = Path.Combine(Paths.BepInExRootPath, "PatchFace_audio.txt");
        private readonly HashSet<string> seenPlaying = new HashSet<string>();
        private float nextScan;

        internal AudioScanner()
        {
            File.WriteAllText(outPath, "PatchFace audio log, started " + DateTime.Now + "\n");
        }

        internal void Update()
        {
            if (Time.unscaledTime < nextScan)
            {
                return;
            }
            nextScan = Time.unscaledTime + ScanInterval;

            foreach (AudioSource source in UnityEngine.Object.FindObjectsOfType<AudioSource>())
            {
                if (!source.isPlaying || source.clip == null || source.clip.length < LongClipSeconds)
                {
                    continue;
                }
                string key = PathOf(source.gameObject) + "|" + source.clip.name;
                if (seenPlaying.Add(key))
                {
                    Write("NOW PLAYING  " + Describe(source));
                }
            }
        }

        internal void DumpScene(string reason)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("===== Scene dump (" + reason + ") at " + DateTime.Now.ToLongTimeString() + " =====");

            foreach (AudioMixer mixer in Resources.FindObjectsOfTypeAll<AudioMixer>())
            {
                sb.Append("Mixer '" + mixer.name + "' groups:");
                foreach (AudioMixerGroup group in mixer.FindMatchingGroups(string.Empty))
                {
                    sb.Append(" [" + group.name + "]");
                }
                sb.AppendLine();
                foreach (string param in new[] { "MasterVolume", "MasterPitch", "MusicVolume", "SFXVolume", "SfxVolume", "AmbienceVolume", "VoiceVolume" })
                {
                    if (mixer.GetFloat(param, out float value))
                    {
                        sb.AppendLine("  exposed parameter " + param + " = " + value);
                    }
                }
            }

            if (EntityMaster.Instance != null)
            {
                AudioSource combat = EntityMaster.Instance.GetComponent<AudioSource>();
                sb.AppendLine("EntityMaster combat music: " + (combat == null ? "none" : Describe(combat)) + " musicLevel=" + EntityMaster.Instance._musicLevel);
            }

            List<string> lines = new List<string>();
            foreach (AudioSource source in Resources.FindObjectsOfTypeAll<AudioSource>())
            {
                if (!source.gameObject.scene.IsValid())
                {
                    continue; // prefab asset, not in the scene
                }
                lines.Add(Describe(source));
            }
            lines.Sort(StringComparer.Ordinal);
            sb.AppendLine(lines.Count + " audio sources:");
            foreach (string line in lines)
            {
                sb.AppendLine("  " + line);
            }
            Write(sb.ToString());
        }

        private static string Describe(AudioSource s)
        {
            AudioClip clip = s.clip;
            string clipText = clip == null ? "no clip" : "'" + clip.name + "' " + clip.length.ToString("0.0") + "s";
            return PathOf(s.gameObject)
                + " | clip " + clipText
                + " | loop " + s.loop
                + " | vol " + s.volume.ToString("0.00")
                + " | 3D " + s.spatialBlend.ToString("0.00")
                + " | playOnAwake " + s.playOnAwake
                + " | playing " + s.isPlaying
                + " | active " + (s.isActiveAndEnabled)
                + " | group " + (s.outputAudioMixerGroup == null ? "none" : s.outputAudioMixerGroup.name);
        }

        private static string PathOf(GameObject go)
        {
            string path = go.name;
            Transform t = go.transform.parent;
            while (t != null)
            {
                path = t.name + "/" + path;
                t = t.parent;
            }
            return go.scene.name + ":" + path;
        }

        private void Write(string text)
        {
            File.AppendAllText(outPath, text + "\n");
        }
    }
}
