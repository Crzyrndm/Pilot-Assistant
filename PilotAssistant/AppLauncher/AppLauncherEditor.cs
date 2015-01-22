using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

namespace PilotAssistant.AppLauncher
{
    using Presets;
    using Utility;

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class AppLauncherEditor : MonoBehaviour
    {
        private static ApplicationLauncherButton btnLauncher;
        private static Rect window;

        internal static bool bDisplayEditor = false;

        void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(this.OnAppLauncherReady);
            window = new Rect(500, 40, 30, 30);
        }

        void OnDestroy()
        {
            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
            btnLauncher = null;
        }

        private void OnAppLauncherReady()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("Pilot Assistant/Icons/AppLauncherIcon", false));
        }

        void OnGameSceneChange(GameScenes scene)
        {
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            bDisplayEditor = true;
        }

        private void OnToggleFalse()
        {
            bDisplayEditor = false;
        }

        private void OnGUI()
        {
            Utility.GeneralUI.Styles();
            if (bDisplayEditor)
            {
                window = GUILayout.Window(0984658, window, managerWindow, "Pilot Assistant Craft Preset Manager", GUILayout.Width(0), GUILayout.Height(0));
            }
        }

        private void managerWindow(int id)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Craft Name", GUILayout.Width(200));
            GUILayout.Label("Pilot Assistant Preset", GUILayout.Width(200));
            GUILayout.Label("SSAS Preset", GUILayout.Width(200));
            GUILayout.Label("Stock SAS Preset", GUILayout.Width(200));
            GUILayout.EndHorizontal();

            foreach (KeyValuePair<string, CraftPreset> cp in PresetManager.Instance.craftPresetList)
            {
                drawCraftPreset(cp.Value);
            }

            GUI.DragWindow();
        }

        private void drawCraftPreset(CraftPreset cp)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(cp.Name, GUILayout.Width(200));
            GUILayout.Label(cp.PresetPA.name, GUILayout.Width(200));
            GUILayout.Label(cp.SSAS.name, GUILayout.Width(200));
            GUILayout.Label(cp.Stock.name, GUILayout.Width(200));
            GUILayout.EndHorizontal();
        }
    }
}
