using HarmonyLib;
using Rewired.UI.ControlMapper;
using UnityEngine;
using UnityEngine.UI;

namespace PatchFace
{
    /// <summary>
    /// Lets the pause key also close the pause menu. When the game pauses it switches off the
    /// controls that the pause key belongs to, so the game itself never sees the second press.
    /// On the main pause screen we press the Resume button (exactly what a mouse click does).
    /// On the Options screen we use the game's own step back to the pause screen.
    /// </summary>
    internal static class PauseResume
    {
        private static bool menuWasOpen;
        private static PlayerOptions cachedOptions;
        private static Button cachedResume;

        internal static void OnSceneLoaded()
        {
            menuWasOpen = false;
        }

        /// <summary>Runs after all game scripts, so the game cannot reopen the menu in the same frame.</summary>
        internal static void LateUpdate()
        {
            PlayerInput input = PlayerInput.Instance;
            Equipment equipment = Equipment.Instance;
            PlayerOptions options = equipment != null ? equipment._playerOptions : null;
            if (input == null || input._player == null || options == null)
            {
                menuWasOpen = false;
                return;
            }

            bool open = options.menuOpen;
            // Only react if the menu was already open last frame, so the press that opened it doesn't also close it.
            if (open && menuWasOpen && EscKey.Consume(input._player))
            {
                GameObject optionsScreen = Traverse.Create(options).Field("optionsMenu").GetValue<GameObject>();
                Button resume = ResumeButton(options);
                if (ControlsScreenOpen())
                {
                    Plugin.Log.LogInfo("Esc in pause menu: ignored, the controls screen handles its own keys");
                }
                else if (optionsScreen != null && optionsScreen.activeInHierarchy)
                {
                    options.ToggleMenu(); // the game's own "back to the pause screen"
                    Plugin.Log.LogInfo("Esc in pause menu: Options -> pause screen");
                }
                else if (resume != null && resume.isActiveAndEnabled && resume.interactable)
                {
                    resume.onClick.Invoke();
                    Plugin.Log.LogInfo("Esc in pause menu: pressed Resume");
                }
                else
                {
                    options.ToggleMenu();
                    Plugin.Log.LogWarning("Esc in pause menu: Resume button not found, closed the menu directly");
                }
                open = options.menuOpen;
            }
            menuWasOpen = open;
        }

        /// <summary>The Resume button is the one whose click calls PlayerOptions.ToggleMenu.</summary>
        private static Button ResumeButton(PlayerOptions options)
        {
            if (cachedOptions == options && cachedResume != null)
            {
                return cachedResume;
            }
            cachedOptions = options;
            cachedResume = null;
            foreach (Button button in options.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == options && button.onClick.GetPersistentMethodName(i) == nameof(PlayerOptions.ToggleMenu))
                    {
                        cachedResume = button;
                        return button;
                    }
                }
            }
            return null;
        }

        /// <summary>The key rebinding screen uses the same keys, so leave it alone while it is open.</summary>
        private static bool ControlsScreenOpen()
        {
            foreach (ControlMapper mapper in Object.FindObjectsOfType<ControlMapper>())
            {
                if (mapper.isOpen)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
