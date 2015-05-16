using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;

    public class AppLauncherFlight
    {
        static AppLauncherFlight instance;
        public static AppLauncherFlight Instance
        {
            get
            {
                if (instance == null)
                    instance = new AppLauncherFlight();
                return instance;
            }
        }
        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        private ApplicationLauncherButton btnLauncher;
        public Rect window;

        public static bool bDisplayBindings = false;
        public static bool bDisplayOptions = false;
        public static bool bDisplayAssistant = false;
        public static bool bDisplaySAS = false;
        public static bool bDisplaySSAS = false;

        
        public void Start()
        {
            OnAppLauncherReady();
        }

        public void OnDestroy()
        {
            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
            btnLauncher = null;
            instance = null;
        }

        private void OnAppLauncherReady()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("Pilot Assistant/Icon/AppLauncherIcon", false));
        }

        void OnGameSceneChange(GameScenes scene)
        {
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            if (Input.GetMouseButtonUp(0))
                bDisplayOptions = true;
            else if (Input.GetMouseButtonUp(1))
            {
                bDisplayAssistant = true;
                if (bDisplayOptions)
                    btnLauncher.SetTrue(false);
                else
                    btnLauncher.SetFalse(false);
            }
        }

        private void OnToggleFalse()
        {
            if (Input.GetMouseButtonUp(0))
                bDisplayOptions = false;
            else if (Input.GetMouseButtonUp(1))
            {
                bDisplayAssistant = true;
                if (bDisplayOptions)
                    btnLauncher.SetTrue(false);
                else
                    btnLauncher.SetFalse(false);
            }
        }

        public void Draw()
        {
            if (bDisplayOptions)
                window = GUILayout.Window(0984653, window, optionsWindow, "", GUILayout.Width(0), GUILayout.Height(0));
        }

        private void optionsWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                btnLauncher.SetFalse();

            bDisplayAssistant = GUILayout.Toggle(bDisplayAssistant, "Pilot Assistant", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySAS = GUILayout.Toggle(bDisplaySAS, "Stock SAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplaySSAS = GUILayout.Toggle(bDisplaySSAS, "SSAS", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            bDisplayBindings = GUILayout.Toggle(bDisplayBindings, "Keybindings", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (GUILayout.Button("Update Defaults"))
                PresetManager.updateDefaults();

            GUI.DragWindow();
        }
    }
}
