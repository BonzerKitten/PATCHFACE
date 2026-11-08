using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PatchFace
{
    [BepInPlugin(Guid, ModName, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "patchface.pigface";
        public const string ModName = "PatchFace";
        public const string Version = "1.1.0";

        // Dragging a slider changes the value every frame; write the file once things settle.
        private const float SaveDelay = 0.5f;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ToggleSprint;
        internal static ConfigEntry<bool> ToggleAim;
        internal static ConfigEntry<float> MusicVolumeSetting;
        internal static ConfigEntry<float> FieldOfViewSetting;

        private readonly AmmoHealthHud hud = new AmmoHealthHud();
        private bool configDirty;
        private float lastConfigChange;

        // Features that already reported an error, so a broken feature logs once instead of every frame.
        private readonly HashSet<string> failedFeatures = new HashSet<string>();

        // Cached so calling them through Run() doesn't allocate every frame.
        private Action holdTogglesUpdate, musicVolumeUpdate, pauseLateUpdate, laptopLateUpdate, optionsLateUpdate, hudLateUpdate;

        private void Awake()
        {
            Log = Logger;
            ToggleSprint = Config.Bind("Controls", "ToggleSprint", true, "Press sprint once to start sprinting, press again to stop.");
            ToggleAim = Config.Bind("Controls", "ToggleAim", true, "Press aim once to aim, press again to stop.");
            MusicVolumeSetting = Config.Bind("Audio", "MusicVolume", 1f,
                new ConfigDescription("Music volume, from 0 (silent) to 1 (normal). Applied on top of the game's main volume.", new AcceptableValueRange<float>(0f, 1f)));
            FieldOfViewSetting = Config.Bind("Video", "FieldOfView", FieldOfView.DefaultWidth,
                new ConfigDescription("How wide the view is, in degrees across a 16:9 screen. 112 is the game's own view. Aiming keeps its normal zoom, and guns and hands always look as in the unmodded game.",
                    new AcceptableValueRange<float>(FieldOfView.MinWidth, FieldOfView.MaxWidth)));
            Config.SaveOnConfigSet = false;
            Config.SettingChanged += (sender, args) =>
            {
                configDirty = true;
                lastConfigChange = Time.unscaledTime;
            };

            holdTogglesUpdate = HoldToggles.Update;
            musicVolumeUpdate = MusicVolume.Update;
            pauseLateUpdate = PauseResume.LateUpdate;
            laptopLateUpdate = LaptopEscape.LateUpdate;
            optionsLateUpdate = OptionsInjector.LateUpdate;
            hudLateUpdate = hud.LateUpdate;

            // One hook at a time, so a game update that breaks one hook leaves the others working.
            Harmony harmony = new Harmony(Guid);
            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                {
                    continue;
                }
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Log.LogError("Could not hook the game (" + type.FullName + "); that feature will not work. " + e);
                }
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PatchFace " + Version + " loaded. " + SettingsSummary());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.LogInfo("Scene loaded: " + scene.name);
            Run("toggles", () => HoldToggles.Reset("scene loaded: " + scene.name));
            Run("pause key", PauseResume.OnSceneLoaded);
            Run("music volume", MusicVolume.TagScene);
            Run("options screens", OptionsInjector.OnSceneLoaded);
        }

        private void Update()
        {
            Run("toggles", holdTogglesUpdate);
            Run("music volume", musicVolumeUpdate);
            if (configDirty && Time.unscaledTime - lastConfigChange > SaveDelay)
            {
                SaveConfig();
            }
        }

        private void LateUpdate()
        {
            Run("pause key", pauseLateUpdate);
            Run("laptop Esc", laptopLateUpdate);
            Run("options screens", optionsLateUpdate);
            Run("HUD", hudLateUpdate);
        }

        private void OnApplicationQuit()
        {
            if (configDirty)
            {
                SaveConfig();
            }
        }

        /// <summary>Runs one feature; an error is logged once and the other features keep working.</summary>
        private void Run(string feature, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (failedFeatures.Add(feature))
                {
                    Log.LogError("Error in " + feature + " (further errors from it are not logged): " + e);
                }
            }
        }

        private void SaveConfig()
        {
            configDirty = false;
            try
            {
                Config.Save();
                Log.LogInfo("Settings saved. " + SettingsSummary());
            }
            catch (Exception e)
            {
                Log.LogError("Could not save settings: " + e.Message);
            }
        }

        private static string SettingsSummary()
        {
            return "Music volume " + MusicVolumeSetting.Value.ToString("0.00") + ", field of view " + FieldOfViewSetting.Value.ToString("0") + ", toggle sprint " + (ToggleSprint.Value ? "on" : "off") + ", toggle aim " + (ToggleAim.Value ? "on" : "off");
        }
    }
}
