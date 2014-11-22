using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using AppLauncher;
    using UI;

    internal enum SASList
    {
        Pitch,
        Yaw,
        Roll
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class AtmoSAS : MonoBehaviour
    {
        internal List<PID_Controller> SASControllers = new List<PID_Controller>();

        internal bool bInit = false;

        internal bool bArmed = false;
        internal bool bActive = false;
        internal bool bAtmosphere = false;

        public void Initialise()
        {
            // register vessel
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            if (FlightData.thisVessel.VesselSAS.pidLockedPitch != null)
            {
                PIDclamp c = FlightData.thisVessel.VesselSAS.pidLockedPitch;
                PID_Controller pitch = new PID.PID_Controller(c.kp, c.ki, c.kd, -1, 1, -1, 1, c.clamp);
                SASControllers.Add(pitch);

                c = FlightData.thisVessel.VesselSAS.pidLockedYaw;
                PID_Controller yaw = new PID.PID_Controller(c.kp, c.ki, c.kd, -1, 1, -1, 1, c.clamp);
                SASControllers.Add(yaw);

                c = FlightData.thisVessel.VesselSAS.pidLockedRoll;
                PID_Controller roll = new PID.PID_Controller(c.kp, c.ki, c.kd, -1, 1, -1, 1, c.clamp);
                SASControllers.Add(roll);

                bInit = true;
            }
        }

        public void Update()
        {
            if (!bInit)
                Initialise();

            // SAS activated by user
            if (bArmed && !bActive && GameSettings.SAS_TOGGLE.GetKeyDown() && !FlightData.thisVessel.ctrlState.killRot)
            {
                bActive = true;
                FlightData.thisVessel.ctrlState.killRot = false;
            }
            else if (bActive && (GameSettings.SAS_TOGGLE.GetKeyDown() || FlightData.thisVessel.ctrlState.killRot))
            {
                bActive = false;
                if (GameSettings.SAS_TOGGLE.GetKeyDown())
                    FlightData.thisVessel.ctrlState.killRot = false;
            }

            // Atmospheric mode tracks horizon, don't want in space
            if (FlightData.thisVessel.staticPressure > 0)
                bAtmosphere = true;
            else
                bAtmosphere = false;
        }

        public void FixedUpdate()
        {
            updateTarget();
            if (bInit && bActive)
            {
                updateTarget();
            }
        }

        private void updateTarget()
        {
            SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
            SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
        }
    }
}
