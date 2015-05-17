using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.Toolbar
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
                PilotAssistantFlightCore.bDisplayOptions = true;
            else if (Input.GetMouseButtonUp(1))
            {
                PilotAssistantFlightCore.bDisplayAssistant = true;
                setBtnState(PilotAssistantFlightCore.bDisplayOptions);
            }
        }

        private void OnToggleFalse()
        {
            if (Input.GetMouseButtonUp(0))
                PilotAssistantFlightCore.bDisplayOptions = false;
            else if (Input.GetMouseButtonUp(1))
            {
                PilotAssistantFlightCore.bDisplayAssistant = true;
                setBtnState(PilotAssistantFlightCore.bDisplayOptions);
            }
        }

        public static void setBtnState(bool state, bool click = false)
        {
            if (state)
                instance.btnLauncher.SetTrue(click);
            else
                instance.btnLauncher.SetFalse(click);
        }
    }
}
