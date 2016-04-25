using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.Toolbar
{
    using Utility;
    using KSP.UI.Screens;

    public class AppLauncherFlight
    {
        private static ApplicationLauncherButton btnLauncher;
        
        public static void Awake()
        {
            if (btnLauncher == null)
                btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT,
                                                                                GameDatabase.Instance.GetTexture("Pilot Assistant/Icon/AppLauncherIcon", false));
        }

        private static void OnToggleTrue()
        {
            PilotAssistantFlightCore.bDisplayAssistant = true;
        }

        private static void OnToggleFalse()
        {
            PilotAssistantFlightCore.bDisplayAssistant = false;
        }

        public static void setBtnState(bool state, bool click = false)
        {
            if (state)
                btnLauncher.SetTrue(click);
            else
                btnLauncher.SetFalse(click);
        }
    }
}
