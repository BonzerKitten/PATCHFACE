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
    /// a MUSIC slider under VOLUME, and TOGGLE SPRINT / TOGGLE AIM checkboxes on the second page.
    /// Each control is a copy of one of the game's own, so it looks and behaves the same.
    /// The copies get fresh events, so they never call the game's original handlers
    /// (a copied volume slider would otherwise also change the main volume).
    /// Values live in the mod's settings file, not in the game's save.
    /// </summary>
    internal static class OptionsInjector
    {
        private const string MusicRowName = "PatchFace music slider";
        private const string SprintRowName = "PatchFace sprint toggle";
        private const string AimRowName = "PatchFace aim toggle";

        private static readonly List<Slider> musicSliders = new List<Slider>();
        private static readonly List<KeyValuePair<Toggle, ConfigEntry<bool>>> toggles = new List<KeyValuePair<Toggle, ConfigEntry<bool>>>();

        internal static void OnSceneLoaded()
        {
            musicSliders.RemoveAll(s => s == null);
            toggles.RemoveAll(t => t.Key == null);

            foreach (MainMenu menu in Resources.FindObjectsOfTypeAll<MainMenu>())
            {
                if (menu.gameObject.scene.IsValid())
                {
                    Inject(menu._volumeSlider, menu._crosshairToggle);
                }
            }
            foreach (PlayerOptions options in Resources.FindObjectsOfTypeAll<PlayerOptions>())
            {
                if (options.gameObject.scene.IsValid())
                {
                    Traverse fields = Traverse.Create(options);
                    Inject(fields.Field("volumeSlider").GetValue<Slider>(), fields.Field("crosshairToggle").GetValue<Toggle>());
                }
            }
        }

        /// <summary>Keeps every copy showing the current value (both screens share the same settings).</summary>
        internal static void LateUpdate()
        {
            float music = Plugin.MusicVolumeSetting.Value;
            foreach (Slider slider in musicSliders)
            {
                if (slider != null && Mathf.Abs(slider.value - music) > 0.0005f)
                {
                    slider.SetValueWithoutNotify(music);
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

        private static void Inject(Slider volume, Toggle crosshair)
        {
            if (volume != null)
            {
                AddMusicSlider(volume);
            }
            if (crosshair != null)
            {
                AddToggle(crosshair, SprintRowName, "TOGGLE SPRINT", Plugin.ToggleSprint);
                AddToggle(crosshair, AimRowName, "TOGGLE AIM", Plugin.ToggleAim);
            }
        }

        private static void AddMusicSlider(Slider volume)
        {
            Transform row = volume.transform.parent;
            Transform column = row != null ? row.parent : null;
            if (column == null || column.Find(MusicRowName) != null)
            {
                return;
            }
            GameObject copy = Object.Instantiate(row.gameObject, column);
            copy.name = MusicRowName;
            copy.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);

            SetLabel(copy, "MUSIC");
            Slider slider = copy.GetComponentInChildren<Slider>(true);
            slider.onValueChanged = new Slider.SliderEvent();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.SetValueWithoutNotify(Plugin.MusicVolumeSetting.Value);
            slider.onValueChanged.AddListener(value => Plugin.MusicVolumeSetting.Value = value);
            UseAutomaticNavigation(slider);
            foreach (SliderNumber number in copy.GetComponentsInChildren<SliderNumber>(true))
            {
                number._sliderToMatch = slider;
            }
            musicSliders.Add(slider);
        }

        private static void AddToggle(Toggle template, string rowName, string label, ConfigEntry<bool> setting)
        {
            Transform column = template.transform.parent;
            if (column == null || column.Find(rowName) != null)
            {
                return;
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
