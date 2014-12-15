using System;
using System.Collections.Generic;
using UnityEngine;
using PilotAssistant.Presets;

namespace PilotAssistant.UI
{
    static class PAPresetWindow
    {
        internal static string newPresetName = "";
        internal static Rect presetWindow = new Rect(0, 0, 200, 10);

        internal static void Draw()
        {
            presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "Pilot Assistant Presets", GUILayout.Width(200), GUILayout.Height(0));
        }

        private static void displayPresetWindow(int id)
        {
            if (GUI.Button(new Rect(presetWindow.width - 16, 2, 14, 14), ""))
            {
                PAMainWindow.showPresets = false;
            }

            if (PresetManager.activePAPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.activePAPreset.name));
                if (PresetManager.activePAPreset.name != "Default")
                {
                    if (GUILayout.Button("Update Preset"))
                    {
                        PresetManager.activePAPreset.Update(PilotAssistant.controllers);
                        PresetManager.saveCFG();
                        Messaging.postMessage(PresetManager.activePAPreset.name + " updated");
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
                    foreach (PresetPA p in PresetManager.PAPresetList)
                    {
                        if (newPresetName == p.name)
                        {
                            Messaging.postMessage("Failed to add preset with duplicate name");
                            return;
                        }
                    }

                    PresetManager.PAPresetList.Add(new PresetPA(PilotAssistant.controllers, newPresetName));
                    newPresetName = "";
                    PresetManager.activePAPreset = PresetManager.PAPresetList[PresetManager.PAPresetList.Count - 1];
                    PresetManager.saveCFG();
                }
                else
                {
                    Messaging.postMessage("Failed to add preset with no name");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
            {
                PresetManager.loadPAPreset(PresetManager.defaultPATuning);
                PresetManager.activePAPreset = PresetManager.defaultPATuning;
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (PresetPA p in PresetManager.PAPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                {
                    PresetManager.loadPAPreset(p);
                    PresetManager.activePAPreset = p;
                    Messaging.postMessage("Loaded preset " + p.name);
                }
                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    Messaging.postMessage("Deleted preset " + p.name);
                    if (PresetManager.activePAPreset == p)
                        PresetManager.activePAPreset = null;
                    PresetManager.PAPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
