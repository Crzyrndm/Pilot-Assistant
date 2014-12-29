using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Presets;
    using Utility;

    internal static class SASPresetWindow
    {
        private static string newPresetName = "";
        internal static Rect windowRect = new Rect(550, 50, 50, 50);

        private const string TEXT_FIELD_GROUP = "SASPresetWindow";

        internal static void Draw(bool show)
        {
            if (show)
            {
                GUI.skin = HighLogic.Skin;
                windowRect = GUILayout.Window(78934857, windowRect, drawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
            }
            else
                GeneralUI.ClearLocks(TEXT_FIELD_GROUP);
        }

        private static void drawPresetWindow(int id)
        {
            // Start a text field group.
            GeneralUI.StartTextFieldGroup(TEXT_FIELD_GROUP);
            if (SurfSAS.IsSSASMode())
                DrawSurfPreset();
            else
                DrawStockPreset();
        }

        private static void DrawSurfPreset()
        {
            if (PresetManager.GetActiveSASPreset() != null)
            {
                SASPreset p = PresetManager.GetActiveSASPreset();
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.BoldLabelStyle);
                if (p != PresetManager.GetDefaultSASTuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.ButtonStyle))
                    {
                        SurfSAS.UpdatePreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            GeneralUI.TextFieldNext(TEXT_FIELD_GROUP);
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.ButtonStyle, GUILayout.Width(25)))
            {
                SurfSAS.RegisterNewPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.GUISectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.BoldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.ButtonStyle))
            {
                SurfSAS.LoadPreset(PresetManager.GetDefaultSASTuning());
            }

            List<SASPreset> allPresets = PresetManager.GetAllSASPresets();
            foreach (SASPreset p in allPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.ButtonStyle))
                {
                    SurfSAS.LoadPreset(p);
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

        private static void DrawStockPreset()
        {
            if (PresetManager.GetActiveStockSASPreset() != null)
            {
                SASPreset p = PresetManager.GetActiveStockSASPreset();
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.BoldLabelStyle);
                if (p != PresetManager.GetDefaultStockSASTuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.ButtonStyle))
                    {
                        SurfSAS.UpdateStockPreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            GeneralUI.TextFieldNext(TEXT_FIELD_GROUP);
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.ButtonStyle, GUILayout.Width(25)))
            {
                SurfSAS.RegisterNewStockPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.GUISectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.BoldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.ButtonStyle))
            {
                SurfSAS.LoadStockPreset(PresetManager.GetDefaultStockSASTuning());
            }

            List<SASPreset> allStockPresets = PresetManager.GetAllStockSASPresets();
            foreach (SASPreset p in allStockPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.ButtonStyle))
                {
                    SurfSAS.LoadStockPreset(p);
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
