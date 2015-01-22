using System;
using System.Collections.Generic;
using UnityEngine;

using PilotAssistant.Presets;
using PilotAssistant.Utility;

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

            if (PresetManager.Instance.activePAPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activePAPreset.name));
                if (PresetManager.Instance.activePAPreset.name != "Default")
                {
                    if (GUILayout.Button("Update Preset"))
                        updatePreset();
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                newPreset();
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
            {
                PresetManager.loadPAPreset(PresetManager.Instance.defaultPATuning);
                PresetManager.Instance.activePAPreset = PresetManager.Instance.defaultPATuning;
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (PresetPA p in PresetManager.Instance.PAPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                {
                    PresetManager.loadPAPreset(p);
                    PresetManager.Instance.activePAPreset = p;
                    Messaging.postMessage("Loaded preset " + p.name);
                }
                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    Messaging.postMessage("Deleted preset " + p.name);
                    if (PresetManager.Instance.activePAPreset == p)
                        PresetManager.Instance.activePAPreset = null;
                    PresetManager.Instance.PAPresetList.Remove(p);
                    PresetManager.saveToFile();
                }
                GUILayout.EndHorizontal();
            }
        }

        private static void newPreset()
        {
            if (newPresetName != "")
            {
                foreach (PresetPA p in PresetManager.Instance.PAPresetList)
                {
                    if (newPresetName == p.name)
                    {
                        Messaging.postMessage("Failed to add preset with duplicate name");
                        return;
                    }
                }

                if (PresetManager.Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName))
                    PresetManager.Instance.craftPresetList[FlightData.thisVessel.vesselName].PresetPA = new PresetPA(PilotAssistant.controllers, newPresetName);
                else
                {
                    PresetManager.Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                        new CraftPreset(FlightData.thisVessel.vesselName, new PresetPA(PilotAssistant.controllers, newPresetName), PresetManager.Instance.activeSASPreset, PresetManager.Instance.activeStockSASPreset));
                }

                PresetManager.Instance.PAPresetList.Add(new PresetPA(PilotAssistant.controllers, newPresetName));
                newPresetName = "";
                PresetManager.Instance.activePAPreset = PresetManager.Instance.PAPresetList[PresetManager.Instance.PAPresetList.Count - 1];
                PresetManager.saveToFile();
            }
            else
            {
                Messaging.postMessage("Failed to add preset with no name");
            }
        }

        private static void updatePreset()
        {
            PresetManager.Instance.activePAPreset.Update(PilotAssistant.controllers);

            if (PresetManager.Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName))
                PresetManager.Instance.craftPresetList[FlightData.thisVessel.vesselName].PresetPA = new PresetPA(PilotAssistant.controllers, newPresetName);
            else
            {
                PresetManager.Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                    new CraftPreset(FlightData.thisVessel.vesselName, new PresetPA(PilotAssistant.controllers, newPresetName), PresetManager.Instance.activeSASPreset, PresetManager.Instance.activeStockSASPreset));
            }

            PresetManager.saveToFile();
            Messaging.postMessage(PresetManager.Instance.activePAPreset.name + " updated");
        }
    }
}
