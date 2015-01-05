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
            window = new Rect(Screen.width - 180, 40, 30, 30);
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
            GUILayout.Label("Craft Name");
            GUILayout.Label("Pilot Assistant Preset");
            GUILayout.Label("SSAS Preset");
            GUILayout.Label("Stock SAS Preset");
            GUILayout.EndHorizontal();

            foreach (CraftPreset cp in PresetManager.craftPresetList)
            {
                drawCraftPreset(cp);
            }
        }

        private void drawCraftPreset(CraftPreset cp)
        {
            GUILayout.BeginHorizontal();
            cp.Name = GUILayout.TextField(cp.Name);
            GUILayout.Label(cp.PresetPA.name);
            GUILayout.Label(cp.SSAS.name);
            GUILayout.Label(cp.Stock.name);
            GUILayout.EndHorizontal();
        }
    }
}
