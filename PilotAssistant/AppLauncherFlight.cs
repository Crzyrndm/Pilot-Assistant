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

        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;

        void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(this.OnAppLauncherReady);
            window = new Rect(10, 50, 30, 30);

            RenderingManager.AddToPostDrawQueue(5, Draw);
        }

        void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, Draw);

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

        private void Draw()
        {
            GeneralUI.Styles();

            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                btnLauncher.SetFalse();

            bool temp = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", Utility.GeneralUI.toggleButton);
            if (temp != bDisplayAssistant)
            {
                bDisplayAssistant = temp;
            }
            temp = GUILayout.Toggle(bDisplaySAS, "SAS Systems", Utility.GeneralUI.toggleButton);
            if (temp != bDisplaySAS)
            {
                bDisplaySAS = temp;
            }
            if (GUILayout.Button("Update Defaults"))
            {
                PresetManager.updateDefaults();
            }

            GUI.DragWindow();
        }
    }
}
