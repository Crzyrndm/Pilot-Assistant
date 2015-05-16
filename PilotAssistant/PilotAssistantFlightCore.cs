using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    /* Flight core calls Unity functions of all flight scene classes. This improves control over execution order
     * which has previously been a slight annoyance.
     * 
     * It also simplifies management of event subscriptions and the like
     * */

    using Utility;

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistantFlightCore : MonoBehaviour
    {
        static PilotAssistantFlightCore instance;
        public static PilotAssistantFlightCore Instance
        {
            get
            {
                return instance;
            }
        }

        bool bHideUI = false;

        public void Awake()
        {
            instance = this;
        }

        public void Start()
        {
            FlightData.thisVessel = FlightGlobals.ActiveVessel;

            PilotAssistant.Instance.Start();
            SurfSAS.Instance.Start();
            Stock_SAS.Instance.Start();
            BindingManager.Instance.Start();
            AppLauncherFlight.Instance.Start(); // must be the last to start as it loads settings and assigns it to the others
            
            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(onPostAutoPilotUpdate);

            GameEvents.onHideUI.Add(hideUI);
            GameEvents.onShowUI.Add(showUI);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpRateChanged);
        }

        public void Update()
        {
            PilotAssistant.Instance.Update();
            SurfSAS.Instance.Update();
        }

        void vesselSwitch(Vessel v)
        {
            //if (FlightData.thisVessel == null)
            //    return;
            FlightData.thisVessel.OnPreAutopilotUpdate -= new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate -= new FlightInputCallback(onPostAutoPilotUpdate);

            FlightData.thisVessel = v;

            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(onPreAutoPilotUpdate);
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(onAutoPilotUpdate);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(onPostAutoPilotUpdate);

            PresetManager.loadCraftAsstPreset();
            PresetManager.loadCraftSASPreset();
            Stock_SAS.Instance.vesselSwitch();
        }

        void warpRateChanged()
        {
            //if (FlightData.thisVessel == null)
            //    return;
            PilotAssistant.Instance.warpHandler();
            SurfSAS.Instance.warpHandler();
        }

        //public void FixedUpdate()
        //{
        //}

        void onPreAutoPilotUpdate(FlightCtrlState state)
        {
            FlightData.updateAttitude();
        }

        void onAutoPilotUpdate(FlightCtrlState state)
        {
            SurfSAS.Instance.SurfaceSAS(state);
        }

        void onPostAutoPilotUpdate(FlightCtrlState state)
        {
            PilotAssistant.Instance.vesselController(state);
        }

        public void OnGUI()
        {
            if (bHideUI)
                return;

            GUI.skin = GeneralUI.UISkin;
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            PilotAssistant.Instance.drawGUI();
            SurfSAS.Instance.drawGUI();
            Stock_SAS.Instance.drawGUI();
            AppLauncherFlight.Instance.Draw();
            BindingManager.Instance.Draw();
        }

        void hideUI()
        {
            bHideUI = true;
        }

        void showUI()
        {
            bHideUI = false;
        }

        public void OnDestroy()
        {
            PilotAssistant.Instance.OnDestroy();
            SurfSAS.Instance.OnDestroy();
            Stock_SAS.Instance.OnDestroy();
            AppLauncherFlight.Instance.OnDestroy();
            BindingManager.Instance.OnDestroy();

            GameEvents.onHideUI.Remove(hideUI);
            GameEvents.onShowUI.Remove(showUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpRateChanged);

            instance = null;
        }
    }
}
