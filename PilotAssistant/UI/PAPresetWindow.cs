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

        internal static void Draw()
        {
            windowRect = GUILayout.Window(34245, windowRect, drawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
        }

        private static void drawPresetWindow(int id)
        {
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
                    //ScreenMessages.PostScreenMessage("Loaded preset " + p.name);
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    PresetManager.RemovePreset(p);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }
}
