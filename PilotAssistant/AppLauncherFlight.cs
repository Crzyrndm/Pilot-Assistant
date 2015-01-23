using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AppLauncherFlight : MonoBehaviour
    {
        private ApplicationLauncherButton btnLauncher;
        private Rect window;
        private Rect settingRect;

        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        private static bool bDisplaySettings = false;

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
            bDisplayOptions = true;
        }

        private void OnToggleFalse()
        {
            bDisplayOptions = false;
        }

        private void OnGUI()
        {
            GeneralUI.Styles();
            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
            if (bDisplaySettings)
                settingRect = GUILayout.Window(7549384, settingRect, settingsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            bool temp = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", Utility.GeneralUI.toggleButton);
            if (temp != bDisplayAssistant)
            {
                bDisplayAssistant = temp;
                btnLauncher.toggleButton.SetFalse();
            }
            temp = GUILayout.Toggle(bDisplaySAS, "SAS Systems", Utility.GeneralUI.toggleButton);
            if (temp != bDisplaySAS)
            {
                bDisplaySAS = temp;
                btnLauncher.toggleButton.SetFalse();
            }
            temp = GUILayout.Toggle(bDisplaySettings, "Settings", Utility.GeneralUI.toggleButton);
            if (temp != bDisplaySettings)
            {
                bDisplaySettings = temp;
                btnLauncher.toggleButton.SetFalse();
            }
        }

        private void settingsWindow(int id)
        {

        }
    }
}
