using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Presets;
    using Utility;

    internal static class SASPresetWindow
    {
        internal static string newPresetName = "";
        internal static Rect SASPresetwindow = new Rect(550, 50, 50, 50);
        internal static bool bShowPresets = false;

        internal static void Draw()
        {
            if (bShowPresets)
            {
                SASPresetwindow = GUILayout.Window(78934857, SASPresetwindow, drawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
                SASPresetwindow.x = SASMainWindow.SASwindow.x + SASMainWindow.SASwindow.width;
                SASPresetwindow.y = SASMainWindow.SASwindow.y;
            }
        }

        private static void drawPresetWindow(int id)
        {
            if (SurfSAS.bStockSAS)
                drawStockPreset();
            else
                drawSurfPreset();
        }

        private static void drawSurfPreset()
        {
            if (PresetManager.activeSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.activeSASPreset.name), GeneralUI.boldLabelStyle);
                if (PresetManager.activeSASPreset.name != "Default")
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        PresetManager.activeSASPreset.Update(SurfSAS.SASControllers);
                        PresetManager.saveCFG();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                if (newPresetName != "")
                {
                    foreach (PresetSAS p in PresetManager.SASPresetList)
                    {
                        if (newPresetName == p.name)
                            return;
                    }

                    PresetManager.SASPresetList.Add(new PresetSAS(SurfSAS.SASControllers, newPresetName));
                    newPresetName = "";
                    PresetManager.activeSASPreset = PresetManager.SASPresetList[PresetManager.SASPresetList.Count - 1];
                    PresetManager.saveCFG();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.buttonStyle))
            {
                PresetManager.loadSASPreset(PresetManager.defaultSASTuning);
                PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
            }

            foreach (PresetSAS p in PresetManager.SASPresetList)
            {
                if (p.bStockSAS)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name, GeneralUI.buttonStyle))
                {
                    PresetManager.loadSASPreset(p);
                    PresetManager.activeSASPreset = p;
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    if (PresetManager.activeSASPreset == p)
                        PresetManager.activeSASPreset = null;
                    PresetManager.SASPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private static void drawStockPreset()
        {
            if (PresetManager.activeStockSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.activeStockSASPreset.name), GeneralUI.boldLabelStyle);
                if (PresetManager.activeStockSASPreset.name != "Stock")
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        PresetManager.activeStockSASPreset.Update(Utility.FlightData.thisVessel.VesselSAS);
                        PresetManager.saveCFG();
                    }
                }
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                if (newPresetName != "")
                {
                    foreach (PresetSAS p in PresetManager.SASPresetList)
                    {
                        if (newPresetName == p.name)
                            return;
                    }

                    PresetManager.SASPresetList.Add(new PresetSAS(Utility.FlightData.thisVessel.VesselSAS, newPresetName));
                    newPresetName = "";
                    PresetManager.activeStockSASPreset = PresetManager.SASPresetList[PresetManager.SASPresetList.Count - 1];
                    PresetManager.saveCFG();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);
            
            if (GUILayout.Button("Stock", GeneralUI.buttonStyle))
            {
                PresetManager.loadStockSASPreset(PresetManager.defaultStockSASTuning);
                PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
            }

            foreach (PresetSAS p in PresetManager.SASPresetList)
            {
                if (!p.bStockSAS)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name, GeneralUI.buttonStyle))
                {
                    PresetManager.loadStockSASPreset(p);
                    PresetManager.activeStockSASPreset = p;
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    if (PresetManager.activeStockSASPreset == p)
                        PresetManager.activeStockSASPreset = null;
                    PresetManager.SASPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }
    }
}
