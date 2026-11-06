using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Reads the pause key (Esc by default) directly from its bindings. The game switches that key's
    /// controls off whenever a menu or the laptop is open, so asking Rewired for "UI Pause" returns
    /// nothing there. Reading the bindings keeps working after the player rebinds the key.
    /// Only one feature may act on a given press.
    /// </summary>
    internal static class EscKey
    {
        private const string PauseAction = "UI Pause";

        private static readonly List<ActionElementMap> buffer = new List<ActionElementMap>();
        private static int checkedFrame = -1;
        private static bool pressed;

        /// <summary>True once per press: the first caller in a frame gets it, later callers don't.</summary>
        internal static bool Consume(Player player)
        {
            if (checkedFrame != Time.frameCount)
            {
                checkedFrame = Time.frameCount;
                pressed = Read(player);
            }
            bool result = pressed;
            pressed = false;
            return result;
        }

        private static bool Read(Player player)
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
    }
}
