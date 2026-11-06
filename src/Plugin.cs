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
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> ToggleSprint;
        internal static ConfigEntry<bool> ToggleAim;

        private AudioScanner audioScanner;

        private void Awake()
        {
            Log = Logger;
            ToggleSprint = Config.Bind("Controls", "ToggleSprint", true, "Press sprint once to start sprinting, press again to stop.");
            ToggleAim = Config.Bind("Controls", "ToggleAim", true, "Press aim once to aim, press again to stop.");

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            audioScanner = new AudioScanner();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("PatchFace loaded. Patched methods: " + string.Join(", ", Harmony.GetAllPatchedMethods()));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HoldToggles.Reset("scene loaded: " + scene.name);
            audioScanner.DumpScene("scene loaded: " + scene.name);
        }

        private void Update()
        {
            HoldToggles.Update();
            audioScanner.Update();
            if (Input.GetKeyDown(KeyCode.F9))
            {
                audioScanner.DumpScene("F9 pressed");
            }
        }
    }
}
