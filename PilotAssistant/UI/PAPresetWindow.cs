using System;
using System.Collections.Generic;
using UnityEngine;
using PilotAssistant.Presets;

namespace PilotAssistant.UI
{
    using Utility;

    static class PAPresetWindow
    {
        private static string newPresetName = "";
        internal static Rect windowRect = new Rect(0, 0, 200, 10);

        private const string TEXT_FIELD_GROUP = "PAPresetWindow";

        internal static void Draw(bool show)
        {
            if (show)
            {
                windowRect = GUILayout.Window(34245, windowRect, DrawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
            }
            else
            {
                GeneralUI.ClearLocks(TEXT_FIELD_GROUP);
            }
        }

        private static void DrawPresetWindow(int id)
        {
            // Start a text field group.
            GeneralUI.StartTextFieldGroup(TEXT_FIELD_GROUP);
            
            if (PresetManager.GetActivePAPreset() != null)
            {
                PAPreset p = PresetManager.GetActivePAPreset();
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.boldLabelStyle);
                if (p != PresetManager.GetDefaultPATuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        PilotAssistant.UpdatePreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            GeneralUI.TextFieldNext(TEXT_FIELD_GROUP);
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                PilotAssistant.RegisterNewPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.buttonStyle))
            {
                PilotAssistant.LoadPreset(PresetManager.GetDefaultPATuning());
            }

            List<PAPreset> allPresets = PresetManager.GetAllPAPresets();
            foreach (PAPreset p in allPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.buttonStyle))
                {
                    PilotAssistant.LoadPreset(p);
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    PresetManager.RemovePreset(p);
                }
                GUILayout.EndHorizontal();
            }

            // Autolock vessel controls on focus.
            GeneralUI.AutolockTextFieldGroup(TEXT_FIELD_GROUP, ControlTypes.ALL_SHIP_CONTROLS | ControlTypes.TIMEWARP);
            
            GUILayout.EndVertical();
        }
    }
}
