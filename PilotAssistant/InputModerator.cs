using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using UI;
    using Utility;
    using AppLauncher;
    using Presets;

    internal enum MonitorList
    {
        Altitude,
        Pitch,
        AoA,
        VertSpeed,

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class InputModerator : MonoBehaviour
    {
        internal static List<Monitor> Monitors = new List<Monitor>();
        
        public void Start()
        {
            Monitors.Add(new Monitor(0, 1000000, 100, 0, 0, "Alt"));
            Monitors.Add(new Monitor(-30, 30, 2, 0, 0, "Pitch"));
            Monitors.Add(new Monitor(-10, 10, 2, 0, 0, "AoA"));
            Monitors.Add(new Monitor(-100, 100, 10, 0, 0, "VSpd"));

            // register vessel
            FlightData.thisVessel = FlightGlobals.ActiveVessel;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            // Init colours
            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, GUI);
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
            Monitors.Clear();
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.ball = null;
            FlightData.thisVessel.OnFlyByWire -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void Update()
        {
        }

        public void FixedUpdate()
        {
        }

        public void GUI()
        {
            ModeratorMainWindow.Draw();
        }

        private void vesselController(FlightCtrlState c)
        {
            FlightData.updateAttitude();

            c.pitch -= Monitors[(int)MonitorList.Altitude].response(FlightData.thisVessel.altitude)
                - Monitors[(int)MonitorList.Pitch].response(FlightData.pitch);
        }
    }
}
