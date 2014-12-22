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

        internal static void Draw()
        {
            windowRect = GUILayout.Window(78934857, windowRect, drawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
        }

        private static void drawPresetWindow(int id)
        {
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
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.boldLabelStyle);
                if (p != PresetManager.GetDefaultSASTuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        SurfSAS.UpdatePreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                SurfSAS.RegisterNewPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.buttonStyle))
            {
                SurfSAS.LoadPreset(PresetManager.GetDefaultSASTuning());
            }

            List<SASPreset> allPresets = PresetManager.GetAllSASPresets();
            foreach (SASPreset p in allPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.buttonStyle))
                {
                    SurfSAS.LoadPreset(p);
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    PresetManager.RemovePreset(p);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private static void DrawStockPreset()
        {
            if (PresetManager.GetActiveStockSASPreset() != null)
            {
                SASPreset p = PresetManager.GetActiveStockSASPreset();
                GUILayout.Label(string.Format("Active Preset: {0}", p.GetName()), GeneralUI.boldLabelStyle);
                if (p != PresetManager.GetDefaultStockSASTuning())
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        SurfSAS.UpdateStockPreset();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                SurfSAS.RegisterNewStockPreset(newPresetName);
                newPresetName = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.buttonStyle))
            {
                SurfSAS.LoadStockPreset(PresetManager.GetDefaultStockSASTuning());
            }

            List<SASPreset> allStockPresets = PresetManager.GetAllStockSASPresets();
            foreach (SASPreset p in allStockPresets)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.GetName(), GeneralUI.buttonStyle))
                {
                    SurfSAS.LoadStockPreset(p);
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
