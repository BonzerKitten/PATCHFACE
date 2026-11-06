using HarmonyLib;
using Rewired;
using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Turns the game's hold-to-sprint and hold-to-aim into press-to-toggle.
    /// Every game script asks Rewired "is Sprint/Zoom held?" through Player.GetButton(string),
    /// so we answer that question with our own on/off state instead.
    /// ("Zoom" is the game's internal name for aim / right click.)
    ///
    /// Sprint ends when you stop or stop moving forward, crouch, aim, jump, climb or ride a zipline.
    /// Pressing sprint while aiming stops aiming. Toggles survive the pause menu, and reset on
    /// cutscenes, death and loading.
    /// </summary>
    internal static class HoldToggles
    {
        private const string SprintAction = "Sprint";
        private const string AimAction = "Zoom";
        private const string JumpAction = "Jump";
        private const string ForwardAxis = "MoveVertical";

        // Lets you press sprint a moment before you start moving forward.
        private const float SprintStartGrace = 0.25f;

        private static bool sprintOn;
        private static bool aimOn;
        private static float sprintStartedAt;

        private static bool MenuOpen()
        {
            Equipment equipment = Equipment.Instance;
            return equipment != null && equipment._playerOptions != null && equipment._playerOptions.menuOpen;
        }

        private static bool Dead()
        {
            PlayerHealth health = PlayerHealth.Instance;
            return health != null && health._currentHealth <= 0f;
        }

        /// <summary>True while the player is in control: not in a menu, cutscene or dead.</summary>
        private static bool PlayerInControl()
        {
            PlayerInput input = PlayerInput.Instance;
            return input != null && input._playerEnabled && !input._disableAllControls && !MenuOpen() && !Dead();
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
            PlayerMovement movement = PlayerMovement.Instance;
            if (input == null || input._player == null || movement == null)
            {
                return;
            }
            if (MenuOpen())
            {
                return; // keep toggles as they are while paused
            }
            if (!input._playerEnabled || input._disableAllControls)
            {
                Reset("controls disabled");
                return;
            }
            if (Dead())
            {
                Reset("player dead");
                return;
            }

            Player player = input._player;
            bool toggleSprint = Plugin.ToggleSprint.Value;
            bool toggleAim = Plugin.ToggleAim.Value;

            if (toggleAim && player.GetButtonDown(AimAction))
            {
                aimOn = !aimOn;
            }

            if (toggleSprint && player.GetButtonDown(SprintAction))
            {
                if (sprintOn)
                {
                    sprintOn = false;
                }
                else if (!movement._crouching)
                {
                    sprintOn = true;
                    sprintStartedAt = Time.time;
                    aimOn = false; // sprint while aiming: stop aiming and run
                }
            }

            if (sprintOn)
            {
                string reason = SprintEndReason(input, movement, player, toggleAim);
                if (reason != null)
                {
                    sprintOn = false;
                    Plugin.Log.LogInfo("Sprint ended: " + reason);
                }
            }
        }

        private static string SprintEndReason(PlayerInput input, PlayerMovement movement, Player player, bool toggleAim)
        {
            if (player.GetAxisRaw(ForwardAxis) <= 0f && Time.time - sprintStartedAt > SprintStartGrace)
            {
                return "stopped or not moving forward";
            }
            if (movement._crouching)
            {
                return "crouched";
            }
            if (toggleAim ? aimOn : player.GetButton(AimAction))
            {
                return "aiming";
            }
            if (player.GetButtonDown(JumpAction))
            {
                return "jumped";
            }
            if (movement._isMantling)
            {
                return "climbing";
            }
            if (input._onZipline)
            {
                return "zipline";
            }
            return null;
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
