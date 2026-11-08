using HarmonyLib;
using UnityEngine;

namespace PatchFace
{
    /// <summary>
    /// Field of view slider, in degrees across a 16:9 screen like most games show it.
    ///
    /// The game zooms by subtracting fixed degrees from its view of 80 (10 for iron sights, 20 for
    /// red dots, 60 for scopes), which breaks at any other base view: a scope would reach 0 degrees
    /// on a narrow view and barely zoom on a wide one. So the game still runs its own zoom on its
    /// own numbers ("game FOV"), and the mod only converts the result for the world camera,
    /// keeping the same zoom strength. The gun and hands camera keeps the game FOV, so weapons
    /// look exactly as in the unmodded game. At the default width nothing changes at all.
    /// </summary>
    internal static class FieldOfView
    {
        /// <summary>How wide the game's own view (80 degrees tall) looks on a 16:9 screen.</summary>
        internal const float DefaultWidth = 112f;
        internal const float MinWidth = 90f;
        internal const float MaxWidth = 130f;

        private static PlayerLook trackedLook;
        private static float gameFov;

        /// <summary>The world camera's FOV for a given game FOV: same zoom strength, wider or narrower base.</summary>
        private static float WorldFov(float game)
        {
            float width = Mathf.Clamp(Plugin.FieldOfViewSetting.Value, MinWidth, MaxWidth);
            float scale = Mathf.Tan(width * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(DefaultWidth * 0.5f * Mathf.Deg2Rad);
            float halfGame = Mathf.Clamp(game, 1f, 170f) * 0.5f * Mathf.Deg2Rad;
            return Mathf.Clamp(2f * Mathf.Atan(Mathf.Tan(halfGame) * scale) * Mathf.Rad2Deg, 1f, 179f);
        }

        private static bool IsTracked(PlayerLook look)
        {
            return look != null && look == trackedLook && look._playerCam != null;
        }

        /// <summary>
        /// PlayerLook.Zoom runs every frame during play and moves the camera FOV toward the aim or
        /// normal view. Before it runs, put the game FOV back on the camera so its math sees its own numbers.
        /// </summary>
        [HarmonyPatch(typeof(PlayerLook), nameof(PlayerLook.Zoom))]
        private static class ZoomPatch
        {
            private static void Prefix(PlayerLook __instance)
            {
                if (IsTracked(__instance))
                {
                    __instance._playerCam.fieldOfView = gameFov;
                }
            }

            private static void Postfix(PlayerLook __instance)
            {
                if (__instance._playerCam == null)
                {
                    return;
                }
                if (__instance != trackedLook)
                {
                    trackedLook = __instance;
                    Plugin.Log.LogInfo("Field of view: player camera found, view " + Plugin.FieldOfViewSetting.Value.ToString("0") + " degrees wide");
                }
                gameFov = __instance._playerCam.fieldOfView;
                __instance._playerCam.fieldOfView = WorldFov(gameFov);
            }
        }

        /// <summary>
        /// The gun and hands camera normally copies the world camera's FOV every frame.
        /// Give it the game FOV instead, so weapons look and line up as in the unmodded game.
        /// </summary>
        [HarmonyPatch(typeof(MatchCameraZoom), nameof(MatchCameraZoom.Update))]
        private static class GunCameraPatch
        {
            private static bool Prefix(MatchCameraZoom __instance)
            {
                if (__instance._cameraToSet == null || !IsTracked(trackedLook) || __instance._cameraToMatch != trackedLook._playerCam)
                {
                    return true;
                }
                __instance._cameraToSet.fieldOfView = gameFov - __instance._offset;
                return false;
            }
        }

        /// <summary>
        /// Leaving the laptop, crafting station or mirror sets the camera to 80, after which the game
        /// would slowly slide to the player's view. Go straight to the player's view instead.
        /// </summary>
        [HarmonyPatch(typeof(PlayerLookAt), nameof(PlayerLookAt.DetachCamera))]
        private static class LeaveStationPatch
        {
            private static void Postfix()
            {
                PlayerLook look = PlayerLook.Instance;
                if (IsTracked(look))
                {
                    look._playerCam.fieldOfView = WorldFov(gameFov);
                }
            }
        }
    }
}
