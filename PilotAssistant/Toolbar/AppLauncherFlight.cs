using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.Toolbar
{
    using Utility;

    public class AppLauncherFlight
    {
        private static ApplicationLauncherButton btnLauncher;
        
        public static void Awake()
        {
            if (btnLauncher == null)
                btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse, null, null, null, null,
                                        ApplicationLauncher.AppScenes.FLIGHT, GameDatabase.Instance.GetTexture("Pilot Assistant/Icon/AppLauncherIcon", false));
        }

        private static void OnToggleTrue()
        {
            if (Input.GetMouseButtonUp(0))
                PilotAssistantFlightCore.bDisplayOptions = true;
            else if (Input.GetMouseButtonUp(1))
            {
                PilotAssistantFlightCore.bDisplayAssistant = true;
                setBtnState(PilotAssistantFlightCore.bDisplayOptions);
            }
        }

        private static void OnToggleFalse()
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
                btnLauncher.SetTrue(click);
            else
                btnLauncher.SetFalse(click);
        }
    }
}
