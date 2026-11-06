using System.Collections.Generic;
using System.Text;
using Rewired;
using Rewired.UI.ControlMapper;
using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Lets the pause key also close the pause menu. When the game pauses it switches off the
    /// controls that the pause key belongs to, so the game itself never sees the second press.
    /// We read the key's bindings directly (so rebinding still works) and close the menu the same
    /// way the game does, which also steps back from the Options screen first.
    /// </summary>
    internal static class PauseResume
    {
        private const string PauseAction = "UI Pause";

        private static readonly List<ActionElementMap> buffer = new List<ActionElementMap>();
        private static bool menuWasOpen;
        private static bool bindingsLogged;

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
            if (open && !bindingsLogged)
            {
                bindingsLogged = true;
                LogBindings(input._player);
            }
            // Only react if the menu was already open last frame, so the press that opened it doesn't also close it.
            if (open && menuWasOpen && PausePressed(input._player) && !ControlsScreenOpen())
            {
                options.ToggleMenu();
                open = options.menuOpen;
            }
            menuWasOpen = open;
        }

        private static bool PausePressed(Player player)
        {
            foreach (ControllerMap map in player.controllers.maps.GetAllMaps())
            {
                Controller controller = map.controller;
                if (controller == null)
                {
                    continue;
                }
                buffer.Clear();
                map.GetElementMapsWithAction(PauseAction, false, buffer);
                foreach (ActionElementMap element in buffer)
                {
                    if (element.elementType == ControllerElementType.Button && controller.GetButtonDownById(element.elementIdentifierId))
                    {
                        return true;
                    }
                }
            }
            return false;
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

        private static void LogBindings(Player player)
        {
            StringBuilder sb = new StringBuilder("Pause key bindings:");
            foreach (ControllerMap map in player.controllers.maps.GetAllMaps())
            {
                buffer.Clear();
                map.GetElementMapsWithAction(PauseAction, false, buffer);
                foreach (ActionElementMap element in buffer)
                {
                    InputMapCategory category = ReInput.mapping.GetMapCategory(map.categoryId);
                    sb.Append(" [" + map.controllerType + " " + element.elementIdentifierName
                        + ", category '" + (category != null ? category.name : "?") + "', map enabled " + map.enabled + "]");
                }
            }
            Plugin.Log.LogInfo(sb.ToString());
        }
    }
}
