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
        public const string Version = "0.3.0";

        // Dragging a slider changes the value every frame; write the file once things settle.
        private const float SaveDelay = 0.5f;

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ToggleSprint;
        internal static ConfigEntry<bool> ToggleAim;
        internal static ConfigEntry<float> MusicVolumeSetting;

        private readonly AmmoHealthHud hud = new AmmoHealthHud();
        private bool configDirty;
        private float lastConfigChange;

        private void Awake()
        {
            Log = Logger;
            ToggleSprint = Config.Bind("Controls", "ToggleSprint", true, "Press sprint once to start sprinting, press again to stop.");
            ToggleAim = Config.Bind("Controls", "ToggleAim", true, "Press aim once to aim, press again to stop.");
            MusicVolumeSetting = Config.Bind("Audio", "MusicVolume", 1f,
                new ConfigDescription("Music volume, from 0 (silent) to 1 (normal). Applied on top of the game's main volume.", new AcceptableValueRange<float>(0f, 1f)));
            Config.SaveOnConfigSet = false;
            Config.SettingChanged += (sender, args) =>
            {
                configDirty = true;
                lastConfigChange = Time.unscaledTime;
            };

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PatchFace " + Version + " loaded.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HoldToggles.Reset("scene loaded: " + scene.name);
            PauseResume.OnSceneLoaded();
            MusicVolume.TagScene();
            OptionsInjector.OnSceneLoaded();
        }

        private void Update()
        {
            HoldToggles.Update();
            MusicVolume.Update();
            if (configDirty && Time.unscaledTime - lastConfigChange > SaveDelay)
            {
                SaveConfig();
            }
        }

        private void LateUpdate()
        {
            PauseResume.LateUpdate();
            LaptopEscape.LateUpdate();
            OptionsInjector.LateUpdate();
            hud.LateUpdate();
        }

        private void OnApplicationQuit()
        {
            if (configDirty)
            {
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            configDirty = false;
            Config.Save();
        }
    }
}
