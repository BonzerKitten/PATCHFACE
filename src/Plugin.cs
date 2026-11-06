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
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ToggleSprint;
        internal static ConfigEntry<bool> ToggleAim;
        internal static ConfigEntry<float> MusicVolumeSetting;

        private AudioScanner audioScanner;
        private readonly AmmoHealthHud hud = new AmmoHealthHud();

        private void Awake()
        {
            Log = Logger;
            ToggleSprint = Config.Bind("Controls", "ToggleSprint", true, "Press sprint once to start sprinting, press again to stop.");
            ToggleAim = Config.Bind("Controls", "ToggleAim", true, "Press aim once to aim, press again to stop.");
            MusicVolumeSetting = Config.Bind("Audio", "MusicVolume", 1f,
                new ConfigDescription("Music volume, from 0 (silent) to 1 (normal). Applied on top of the game's main volume.", new AcceptableValueRange<float>(0f, 1f)));

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            audioScanner = new AudioScanner();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PatchFace " + Version + " loaded.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HoldToggles.Reset("scene loaded: " + scene.name);
            PauseResume.OnSceneLoaded();
            MusicVolume.TagScene();
            audioScanner.DumpScene("scene loaded: " + scene.name);
        }

        private void Update()
        {
            HoldToggles.Update();
            MusicVolume.Update();
#if DEBUG
            MusicSelfTest.Update();
#endif
            audioScanner.Update();
            if (Input.GetKeyDown(KeyCode.F9))
            {
                audioScanner.DumpScene("F9 pressed");
            }
        }

        private void LateUpdate()
        {
            PauseResume.LateUpdate();
            hud.LateUpdate();
        }
    }
}
