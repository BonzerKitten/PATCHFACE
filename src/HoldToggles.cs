using HarmonyLib;
using Rewired;

namespace PatchFace
{
    /// <summary>
    /// Turns the game's hold-to-sprint and hold-to-aim into press-to-toggle.
    /// Every game script asks Rewired "is Sprint/Zoom held?" through Player.GetButton(string),
    /// so we answer that question with our own on/off state instead.
    /// ("Zoom" is the game's internal name for aim / right click.)
    /// </summary>
    internal static class HoldToggles
    {
        private const string SprintAction = "Sprint";
        private const string AimAction = "Zoom";

        private static bool sprintOn;
        private static bool aimOn;

        /// <summary>True while the player is in control: not in a menu, cutscene or dead.</summary>
        private static bool PlayerInControl()
        {
            PlayerInput input = PlayerInput.Instance;
            if (input == null || !input._playerEnabled || input._disableAllControls)
            {
                return false;
            }
            Equipment equipment = Equipment.Instance;
            if (equipment != null && equipment._playerOptions != null && equipment._playerOptions.menuOpen)
            {
                return false;
            }
            PlayerHealth health = PlayerHealth.Instance;
            return health == null || health._currentHealth > 0f;
        }

        internal static void Reset(string reason)
        {
            if (sprintOn || aimOn)
            {
                Plugin.Log.LogInfo("Toggles reset (" + reason + ")");
            }
            sprintOn = false;
            aimOn = false;
        }

        internal static void Update()
        {
            PlayerInput input = PlayerInput.Instance;
            if (input == null || input._player == null)
            {
                return;
            }
            if (!input._playerEnabled || input._disableAllControls)
            {
                Reset("controls disabled");
                return;
            }
            PlayerHealth health = PlayerHealth.Instance;
            if (health != null && health._currentHealth <= 0f)
            {
                Reset("player dead");
                return;
            }
            if (!PlayerInControl())
            {
                return;
            }

            Player player = input._player;
            if (Plugin.ToggleSprint.Value && player.GetButtonDown(SprintAction))
            {
                sprintOn = !sprintOn;
                Plugin.Log.LogInfo("Sprint toggle: " + (sprintOn ? "ON" : "OFF"));
            }
            if (Plugin.ToggleAim.Value && player.GetButtonDown(AimAction))
            {
                aimOn = !aimOn;
                Plugin.Log.LogInfo("Aim toggle: " + (aimOn ? "ON" : "OFF"));
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetButton), typeof(string))]
        private static class GetButtonPatch
        {
            private static void Postfix(string actionName, ref bool __result)
            {
                if (actionName == SprintAction)
                {
                    if (Plugin.ToggleSprint.Value && PlayerInControl())
                    {
                        __result = sprintOn;
                    }
                }
                else if (actionName == AimAction)
                {
                    if (Plugin.ToggleAim.Value && PlayerInControl())
                    {
                        __result = aimOn;
                    }
                }
            }
        }
    }
}
