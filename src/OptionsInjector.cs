using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PatchFace
{
    /// <summary>
    /// Adds the mod's settings to both of the game's Options screens (main menu and pause menu):
    /// a MUSIC slider under VOLUME, a FIELD OF VIEW slider under the last sensitivity slider
    /// (the second page has no room left), and TOGGLE SPRINT / TOGGLE AIM checkboxes on the second page.
    /// Each control is a copy of one of the game's own, so it looks and behaves the same.
    /// The copies get fresh events, so they never call the game's original handlers
    /// (a copied volume slider would otherwise also change the main volume).
    /// Values live in the mod's settings file, not in the game's save.
    /// </summary>
    internal static class OptionsInjector
    {
        private const string MusicRowName = "PatchFace music slider";
        private const string FovRowName = "PatchFace field of view slider";
        private const string SprintRowName = "PatchFace sprint toggle";
        private const string AimRowName = "PatchFace aim toggle";

        private static readonly List<KeyValuePair<Slider, ConfigEntry<float>>> sliders = new List<KeyValuePair<Slider, ConfigEntry<float>>>();
        private static readonly List<KeyValuePair<Toggle, ConfigEntry<bool>>> toggles = new List<KeyValuePair<Toggle, ConfigEntry<bool>>>();

        internal static void OnSceneLoaded()
        {
            sliders.RemoveAll(s => s.Key == null);
            toggles.RemoveAll(t => t.Key == null);

            foreach (MainMenu menu in Resources.FindObjectsOfTypeAll<MainMenu>())
            {
                if (menu.gameObject.scene.IsValid())
                {
                    Inject("main menu", menu._volumeSlider, menu.joystickScopeSensSlider, menu._crosshairToggle);
                }
            }
            foreach (PlayerOptions options in Resources.FindObjectsOfTypeAll<PlayerOptions>())
            {
                if (options.gameObject.scene.IsValid())
                {
                    Traverse fields = Traverse.Create(options);
                    Inject("pause menu", fields.Field("volumeSlider").GetValue<Slider>(), fields.Field("joystickScopeSensSlider").GetValue<Slider>(), fields.Field("crosshairToggle").GetValue<Toggle>());
                }
            }
        }

        /// <summary>Keeps every copy showing the current value (both screens share the same settings).</summary>
        internal static void LateUpdate()
        {
            foreach (KeyValuePair<Slider, ConfigEntry<float>> pair in sliders)
            {
                if (pair.Key != null && Mathf.Abs(pair.Key.value - pair.Value.Value) > 0.0005f)
                {
                    pair.Key.SetValueWithoutNotify(pair.Value.Value);
                }
            }
            foreach (KeyValuePair<Toggle, ConfigEntry<bool>> pair in toggles)
            {
                if (pair.Key != null && pair.Key.isOn != pair.Value.Value)
                {
                    pair.Key.SetIsOnWithoutNotify(pair.Value.Value);
                }
            }
        }

        private static void Inject(string screen, Slider volume, Slider lastSensitivity, Toggle crosshair)
        {
            if (volume == null)
            {
                Plugin.Log.LogWarning("Options (" + screen + "): volume slider not found, MUSIC slider not added");
            }
            else if (AddSlider(volume, MusicRowName, "MUSIC", 0f, 1f, false, Plugin.MusicVolumeSetting))
            {
                Plugin.Log.LogInfo("Options (" + screen + "): added MUSIC slider under VOLUME");
            }
            if (lastSensitivity == null)
            {
                Plugin.Log.LogWarning("Options (" + screen + "): sensitivity sliders not found, FIELD OF VIEW slider not added");
            }
            else if (AddSlider(lastSensitivity, FovRowName, "FIELD OF VIEW", FieldOfView.MinWidth, FieldOfView.MaxWidth, true, Plugin.FieldOfViewSetting))
            {
                Plugin.Log.LogInfo("Options (" + screen + "): added FIELD OF VIEW slider under the sensitivity sliders");
            }
            if (crosshair == null)
            {
                Plugin.Log.LogWarning("Options (" + screen + "): crosshair checkbox not found, toggle checkboxes not added");
            }
            else
            {
                bool sprintAdded = AddToggle(crosshair, SprintRowName, "TOGGLE SPRINT", Plugin.ToggleSprint);
                bool aimAdded = AddToggle(crosshair, AimRowName, "TOGGLE AIM", Plugin.ToggleAim);
                if (sprintAdded || aimAdded)
                {
                    Plugin.Log.LogInfo("Options (" + screen + "): added TOGGLE SPRINT and TOGGLE AIM checkboxes");
                }
            }
        }

        /// <summary>Adds a copy of the template's row right under it. Returns true if added (false if it was already there).</summary>
        private static bool AddSlider(Slider template, string rowName, string label, float min, float max, bool wholeNumbers, ConfigEntry<float> setting)
        {
            Transform row = template.transform.parent;
            Transform column = row != null ? row.parent : null;
            if (column == null || column.Find(rowName) != null)
            {
                return false;
            }
            GameObject copy = Object.Instantiate(row.gameObject, column);
            copy.name = rowName;
            copy.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);

            SetLabel(copy, label);
            Slider slider = copy.GetComponentInChildren<Slider>(true);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(setting.Value);
            slider.onValueChanged.AddListener(value => setting.Value = value);
            UseAutomaticNavigation(slider);
            foreach (SliderNumber number in copy.GetComponentsInChildren<SliderNumber>(true))
            {
                number._sliderToMatch = slider;
            }
            sliders.Add(new KeyValuePair<Slider, ConfigEntry<float>>(slider, setting));
            return true;
        }

        /// <summary>Returns true if the checkbox was added (false if it was already there).</summary>
        private static bool AddToggle(Toggle template, string rowName, string label, ConfigEntry<bool> setting)
        {
            Transform column = template.transform.parent;
            if (column == null || column.Find(rowName) != null)
            {
                return false;
            }
            GameObject copy = Object.Instantiate(template.gameObject, column);
            copy.name = rowName;
            Transform back = column.Find("back");
            copy.transform.SetSiblingIndex(back != null ? back.GetSiblingIndex() : column.childCount - 1);

            SetLabel(copy, label);
            FindClickSound(template.onValueChanged, out AudioSource clickSource, out AudioClip clickClip);
            Toggle toggle = copy.GetComponent<Toggle>();
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(setting.Value);
            toggle.onValueChanged.AddListener(on =>
            {
                setting.Value = on;
                if (clickSource != null && clickClip != null && clickSource.isActiveAndEnabled)
                {
                    clickSource.PlayOneShot(clickClip);
                }
            });
            UseAutomaticNavigation(toggle);
            toggles.Add(new KeyValuePair<Toggle, ConfigEntry<bool>>(toggle, setting));
            return true;
        }

        /// <summary>The row's label is its first text that isn't a slider's number display.</summary>
        private static void SetLabel(GameObject row, string text)
        {
            foreach (TMP_Text label in row.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponent<SliderNumber>() == null)
                {
                    label.text = text;
                    return;
                }
            }
        }

        private static void UseAutomaticNavigation(Selectable selectable)
        {
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            selectable.navigation = navigation;
        }

        /// <summary>The game's checkboxes play a click through AudioSource.PlayOneShot set up in the editor; reuse it.</summary>
        private static void FindClickSound(UnityEventBase e, out AudioSource source, out AudioClip clip)
        {
            source = null;
            clip = null;
            for (int i = 0; i < e.GetPersistentEventCount(); i++)
            {
                if (e.GetPersistentMethodName(i) != "PlayOneShot" || !(e.GetPersistentTarget(i) is AudioSource target))
                {
                    continue;
                }
                IList calls = Traverse.Create(e).Field("m_PersistentCalls").Field("m_Calls").GetValue() as IList;
                if (calls == null || i >= calls.Count)
                {
                    return;
                }
                clip = Traverse.Create(calls[i]).Field("m_Arguments").Field("m_ObjectArgument").GetValue() as AudioClip;
                source = target;
                return;
            }
        }
    }
}
