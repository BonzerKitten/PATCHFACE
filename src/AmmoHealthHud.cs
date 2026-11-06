using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace PatchFace
{
    /// <summary>
    /// Replaces the ammo counter with one line: "AMMO 12 / 36  |  HEALTH 100".
    /// We hide the game's ammo text and show a copy of it (same spot, font, size and color).
    /// The game keeps writing ammo into its hidden text, and we read it from there.
    /// </summary>
    internal sealed class AmmoHealthHud
    {
        private const string GameEmptyText = "empty";

        private Text gameText;
        private Text ourText;
        private string infinity = "∞";

        internal void LateUpdate()
        {
            Equipment equipment = Equipment.Instance;
            PlayerHealth health = PlayerHealth.Instance;
            if (equipment == null || equipment._ammo == null || health == null)
            {
                return;
            }
            if (ourText == null || gameText != equipment._ammo)
            {
                Setup(equipment._ammo, health);
            }

            gameText.enabled = false;
            ourText.gameObject.SetActive(gameText.gameObject.activeSelf);

            string ammo = gameText.text;
            if (string.IsNullOrEmpty(ammo))
            {
                ammo = infinity; // melee weapons and bare hands
            }
            else if (ammo == GameEmptyText)
            {
                ammo = "0 / 0";
            }
            int healthNumber = Mathf.Max(0, Mathf.CeilToInt(health._currentHealth));
            string line = "AMMO " + ammo + "  |  HEALTH " + healthNumber;
            if (ourText.text != line)
            {
                ourText.text = line;
            }
        }

        private void Setup(Text source, PlayerHealth health)
        {
            if (ourText != null)
            {
                Object.Destroy(ourText.gameObject);
            }
            gameText = source;
            GameObject copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = "PatchFace ammo and health";
            copy.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
            ourText = copy.GetComponent<Text>();
            ourText.enabled = true;
            ourText.horizontalOverflow = HorizontalWrapMode.Overflow;

            Font font = source.font;
            if (font != null && !font.HasCharacter('∞'))
            {
                infinity = "INF";
            }

            string components = string.Join(", ", System.Array.ConvertAll(source.GetComponents<Component>(), c => c.GetType().Name));
            Plugin.Log.LogInfo("HUD set up. Ammo text components: " + components + ". Font: " + (font != null ? font.name : "none") + ", infinity shown as '" + infinity + "', alignment " + source.alignment);

            Text hiddenHealth = Traverse.Create(health).Field("healthText").GetValue<Text>();
            Plugin.Log.LogInfo(hiddenHealth == null
                ? "Game's own health text: not assigned"
                : "Game's own health text: '" + hiddenHealth.name + "' active " + hiddenHealth.gameObject.activeInHierarchy + ", enabled " + hiddenHealth.enabled + ", text '" + hiddenHealth.text + "'");
        }
    }
}
