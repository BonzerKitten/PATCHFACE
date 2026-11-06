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
                Setup(equipment._ammo);
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

        private void Setup(Text source)
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

            Plugin.Log.LogInfo("HUD: ammo counter replaced with the ammo and health line (melee shows " + (infinity == "INF" ? "INF, the font has no infinity sign" : "the infinity sign") + ")");
        }
    }
}
