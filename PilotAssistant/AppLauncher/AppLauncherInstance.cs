using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.AppLauncher
{
    using Utility;
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class AppLauncherInstance : MonoBehaviour
    {
        private static ApplicationLauncherButton btnLauncher;
        private static Rect windowRect = new Rect(Screen.width - 180, 40, 30, 30);

        private const int WINDOW_ID = 0984653;

        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;

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
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(
                OnToggleTrue, OnToggleFalse,
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
                windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawOptionsWindow, "", GeneralUI.OptionsWindowStyle, GUILayout.Width(0), GUILayout.Height(0));
            }
        }

        private void DrawOptionsWindow(int id)
        {
            bool tmpToggle = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", GeneralUI.ToggleButtonStyle);
            if (tmpToggle != bDisplayAssistant)
            {
                bDisplayAssistant = !bDisplayAssistant;
                btnLauncher.toggleButton.SetFalse();
            }

            tmpToggle = GUILayout.Toggle(bDisplaySAS, "SAS Systems", GeneralUI.ToggleButtonStyle);
            if (tmpToggle != bDisplaySAS)
            {
                bDisplaySAS = !bDisplaySAS;
                btnLauncher.toggleButton.SetFalse();
            }
        }
    }
}
