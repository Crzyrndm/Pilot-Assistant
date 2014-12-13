using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.AppLauncher
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AppLauncherInstance : MonoBehaviour
    {
        private static ApplicationLauncherButton btnLauncher;
        private static Rect window = new Rect(Screen.width - 180, 40, 30, 30);

        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        public static bool bDisplayModerator = false;

        private void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(this.OnAppLauncherReady);
        }

        private void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnAppLauncherReady()
        {
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("Pilot Assistant/Icons/AppLauncherIcon", false));
        }

        private void OnGameSceneChange(GameScenes scene)
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
            GUI.skin = HighLogic.Skin;
            if (bDisplayOptions)
            {
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.MaxWidth(200));
            }
        }

        private void optionsWindow(int id)
        {
            if (GUILayout.Button("Pilot Assistant"))
            {
                bDisplayAssistant = !bDisplayAssistant;
                btnLauncher.toggleButton.SetFalse();
            }
            /*if (GUILayout.Button("Input Moderator"))
            {
                bDisplayModerator = !bDisplayModerator;
                btnLauncher.toggleButton.SetFalse();
            }*/
            if (GUILayout.Button("SAS Systems"))
            {
                bDisplaySAS = !bDisplaySAS;
                btnLauncher.toggleButton.SetFalse();
            }
        }
    }
}
