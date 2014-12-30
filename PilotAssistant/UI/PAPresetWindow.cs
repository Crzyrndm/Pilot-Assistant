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
        private static Rect windowRect = new Rect(0, 0, 200, 10);

        private const string TEXT_FIELD_GROUP = "PAPresetWindow";

        public static void Draw(bool show)
        {
            if (show)
            {
                GUI.skin = HighLogic.Skin;
                windowRect = GUILayout.Window(34245, windowRect, DrawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
            }
            else
            {
                GeneralUI.ClearLocks(TEXT_FIELD_GROUP);
            }
        }

        public static void Reposition(float x, float y)
        {
            windowRect.x = x;
            windowRect.y = y;
        }

        private static void DrawPresetWindow(int id)
        {
            // Start a text field group.
            GeneralUI.StartTextFieldGroup(TEXT_FIELD_GROUP);
            
            if (PresetManager.GetActivePAPreset() != null)
            {
                PAPreset p = PresetManager.GetActivePAPreset();
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.BoldLabelStyle);
                if (p != PresetManager.GetDefaultPATuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.ButtonStyle))
                    {
                        PilotAssistant.UpdatePreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            GeneralUI.TextFieldNext(TEXT_FIELD_GROUP);
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.ButtonStyle, GUILayout.Width(25)))
            {
                PilotAssistant.RegisterNewPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.GUISectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.BoldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.ButtonStyle))
            {
                PilotAssistant.LoadPreset(PresetManager.GetDefaultPATuning());
            }

            List<PAPreset> allPresets = PresetManager.GetAllPAPresets();
            foreach (PAPreset p in allPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.ButtonStyle))
                {
                    PilotAssistant.LoadPreset(p);
                }
                if (GUILayout.Button("x", GeneralUI.ButtonStyle, GUILayout.Width(25)))
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
