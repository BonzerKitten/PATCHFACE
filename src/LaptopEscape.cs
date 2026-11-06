using UnityEngine;
using UnityEngine.UI;

namespace PatchFace
{
    /// <summary>
    /// Esc on the laptop (and the other "look at" screens: crafting station, mirror) closes the
    /// topmost open window by pressing its own X button. With no window open, Esc leaves the screen
    /// the same way the game's exit button does.
    ///
    /// A window's X button is a button named "x_button" or "exit..." inside the window, whose click
    /// switches that window off. The deepest such open window is the one on top (for example the
    /// mission details inside the world map).
    /// </summary>
    internal static class LaptopEscape
    {
        internal static void LateUpdate()
        {
            PlayerLook look = PlayerLook.Instance;
            PlayerInput input = PlayerInput.Instance;
            if (look == null || input == null || input._player == null || look._camState != PlayerLook.CameraState.Menu)
            {
                return;
            }
            if (!EscKey.Consume(input._player))
            {
                return;
            }
            PlayerLookAt station = AttachedStation();
            if (station == null)
            {
                return;
            }

            Button close = TopCloseButton(station);
            if (close != null)
            {
                GameObject window = WindowItCloses(close);
                close.onClick.Invoke();
                Plugin.Log.LogInfo("Esc at " + station.name + ": closed '" + window.name + "' with its '" + close.name + "' button");
            }
            else
            {
                station.DetachCamera();
                Plugin.Log.LogInfo("Esc at " + station.name + ": left the screen");
            }
        }

        private static PlayerLookAt AttachedStation()
        {
            foreach (PlayerLookAt station in Object.FindObjectsOfType<PlayerLookAt>())
            {
                if (station.attached)
                {
                    return station;
                }
            }
            return null;
        }

        private static Button TopCloseButton(PlayerLookAt station)
        {
            Button best = null;
            int bestDepth = -1;
            foreach (Button button in station.GetComponentsInChildren<Button>(false))
            {
                if (!button.interactable || !LooksLikeClose(button.name))
                {
                    continue;
                }
                GameObject window = WindowItCloses(button);
                if (window == null)
                {
                    continue;
                }
                int depth = Depth(window.transform);
                if (depth > bestDepth)
                {
                    best = button;
                    bestDepth = depth;
                }
            }
            return best;
        }

        private static bool LooksLikeClose(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("x_button") || n.StartsWith("exit");
        }

        /// <summary>The open window (an ancestor of the button) that the button's click switches off, if any.</summary>
        private static GameObject WindowItCloses(Button button)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) != "SetActive")
                {
                    continue;
                }
                if (button.onClick.GetPersistentTarget(i) is GameObject window && window.activeInHierarchy && button.transform.IsChildOf(window.transform))
                {
                    return window;
                }
            }
            return null;
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                depth++;
            }
            return depth;
        }
    }
}
