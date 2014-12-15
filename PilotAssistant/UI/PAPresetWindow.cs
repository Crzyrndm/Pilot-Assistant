using System;
using System.Collections.Generic;
using UnityEngine;
using PilotAssistant.Presets;

namespace PilotAssistant.UI
{
    using Utility;

    static class PAPresetWindow
    {
        internal static string newPresetName = "";
        internal static Rect windowRect = new Rect(0, 0, 200, 10);

        internal static void Draw()
        {
            windowRect = GUILayout.Window(34245, windowRect, drawPresetWindow, "Presets", GUILayout.Width(200), GUILayout.Height(0));
        }

        private static void drawPresetWindow(int id)
        {
            if (PresetManager.activePAPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.activePAPreset.name), GeneralUI.boldLabelStyle);
                if (PresetManager.activePAPreset.name != "Default")
                {
                    if (GUILayout.Button("Update Preset", GeneralUI.buttonStyle))
                    {
                        // TODO: Disable for now, fix later
                        //PresetManager.activePAPreset.Update(PilotAssistant.controllers);
                        PresetManager.saveCFG();
                        ScreenMessages.PostScreenMessage(PresetManager.activePAPreset.name + " updated");
                    }
                }
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GeneralUI.buttonStyle, GUILayout.Width(25)))
            {
                if (newPresetName != "")
                {
                    foreach (PresetPA p in PresetManager.PAPresetList)
                    {
                        if (newPresetName == p.name)
                        {
                            ScreenMessages.PostScreenMessage("Failed to add preset with duplicate name");
                            return;
                        }
                    }

                    // TODO: Disable for now, fix later
                    //PresetManager.PAPresetList.Add(new PresetPA(PilotAssistant.controllers, newPresetName));
                    newPresetName = "";
                    PresetManager.activePAPreset = PresetManager.PAPresetList[PresetManager.PAPresetList.Count - 1];
                    PresetManager.saveCFG();
                }
                else
                {
                    ScreenMessages.PostScreenMessage("Failed to add preset with no name");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GeneralUI.guiSectionStyle);
            GUILayout.Label("Available presets: ", GeneralUI.boldLabelStyle);

            if (GUILayout.Button("Default", GeneralUI.buttonStyle))
            {
                PresetManager.loadPAPreset(PresetManager.defaultPATuning);
                PresetManager.activePAPreset = PresetManager.defaultPATuning;
            }

            foreach (PresetPA p in PresetManager.PAPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name, GeneralUI.buttonStyle))
                {
                    PresetManager.loadPAPreset(p);
                    PresetManager.activePAPreset = p;
                    ScreenMessages.PostScreenMessage("Loaded preset " + p.name);
                }
                if (GUILayout.Button("x", GeneralUI.buttonStyle, GUILayout.Width(25)))
                {
                    ScreenMessages.PostScreenMessage("Deleted preset " + p.name);
                    if (PresetManager.activePAPreset == p)
                        PresetManager.activePAPreset = null;
                    PresetManager.PAPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }
    }
}
