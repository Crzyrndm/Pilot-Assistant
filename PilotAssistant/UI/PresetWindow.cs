using System;
using System.Collections.Generic;
using UnityEngine;
using PilotAssistant.Presets;

namespace PilotAssistant.UI
{
    static class PresetWindow
    {
        internal static string newPresetName = "";
        internal static bool showPresets = false;
        internal static Rect presetWindow = new Rect(0, 0, 200, 10);

        internal static void Draw()
        {
            presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "", GUILayout.Width(200), GUILayout.MaxHeight(500));
        }

        private static void displayPresetWindow(int id)
        {
            if (PresetManager.activePreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.activePreset.name));
                if (PresetManager.activePreset.name != "default")
                {
                    if (GUILayout.Button("Update Preset"))
                    {
                        PresetManager.activePreset.Update(PilotAssistant.controllers);
                        PresetManager.saveCFG();
                    }
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                if (newPresetName != "")
                {
                    foreach (Preset p in PresetManager.PresetList)
                    {
                        if (newPresetName == p.name)
                            return;
                    }

                    PresetManager.PresetList.Add(new Preset(PilotAssistant.controllers, newPresetName));
                    newPresetName = "";
                    PresetManager.activePreset = PresetManager.PresetList[PresetManager.PresetList.Count - 1];
                    PresetManager.saveCFG();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Default Tuning"))
            {
                PresetManager.loadPreset(PresetManager.defaultTuning);
                PresetManager.activePreset = PresetManager.defaultTuning;
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (Preset p in PresetManager.PresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                {
                    PresetManager.loadPreset(p);
                    PresetManager.activePreset = p;
                }
                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    if (PresetManager.activePreset == p)
                        PresetManager.activePreset = null;
                    PresetManager.PresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
