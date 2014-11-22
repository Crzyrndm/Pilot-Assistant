using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;



namespace PilotAssistant
{
    using Presets;
    using Utility;
    using PID;
    using UI;

    internal enum PIDList
    {
        HdgBank,
        HdgYaw,
        Aileron,
        Rudder,
        Altitude,
        VertSpeed,
        Elevator
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistant : MonoBehaviour
    {
        internal static List<PID_Controller> controllers = new List<PID_Controller>();

        internal static bool bPause = false;

        // RollController
        internal static bool bHdgActive = false;
        internal static bool bHdgWasActive = false;
        // PitchController
        internal static bool bVertActive = false;
        internal static bool bVertWasActive = false;
        // Altitude / vertical speed
        internal static bool bAltitudeHold = false;
        internal static bool bWasAltitudeHold = false;
        // Wing leveller / Heading control
        internal static bool bWingLeveller = false;

        public void Start()
        {
            PID_Controller HeadingBankController = new PID.PID_Controller(3, 0.1, 0, -30, 30, -0.1, 0.1);
            controllers.Add(HeadingBankController);
            PID_Controller HeadingYawController = new PID.PID_Controller(0, 0, 0, -2, 2, -2, 2);
            controllers.Add(HeadingYawController);
            PID_Controller AileronController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1);
            controllers.Add(AileronController);
            PID_Controller RudderController = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.1, 0.1);
            controllers.Add(RudderController);
            PID_Controller AltitudeToClimbRate = new PID.PID_Controller(0.1, 0, 0, -30, 30, -1, 1); // P control for converting altitude hold to climb rate
            controllers.Add(AltitudeToClimbRate);
            PID_Controller AoAController = new PID.PID_Controller(3, 0.4, 1.5, -10, 10, -10, 10); // Input craft altitude, output target craft AoA
            controllers.Add(AoAController);
            PID_Controller ElevatorController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1); // Convert pitch input to control surface deflection
            controllers.Add(ElevatorController);

            // PID inits
            AileronController.InMax = 180;
            AileronController.InMin = -180;
            AltitudeToClimbRate.InMin = 0;

            // Set up a default preset that can be easily returned to
            PresetManager.defaultTuning = new Preset(controllers, "default");
            
            // register vessel
            FlightData.thisVessel = FlightGlobals.ActiveVessel;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.ball = null;
            FlightData.thisVessel.OnFlyByWire -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(vesselSwitch);
            PresetManager.saveCFG();
            bHdgActive = false;
            bVertActive = false;
        }

        public void Update()
        {
            if (bHdgActive != bHdgWasActive && !bPause)
                hdgToggle();

            if (bVertActive != bVertWasActive && !bPause)
                vertToggle();

            if (bAltitudeHold != bWasAltitudeHold && !bPause)
                altToggle();

            keyPressChanges();
        }

        public void FixedUpdate()
        {
        }

        public void OnGUI()
        {
            if (!AppLauncher.AppLauncherInstance.bDisplay)
                return;

            MainWindow.Draw();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            Utility.FlightData.updateAttitude();

            if (bPause)
                return;
            
            // Heading Control
            if (bHdgActive)
            {
                if (!bWingLeveller)
                {// Fix heading so it behaves properly traversing 0/360 degrees
                    if (controllers[(int)PIDList.HdgBank].SetPoint - FlightData.heading >= -180 && controllers[(int)PIDList.HdgBank].SetPoint - FlightData.heading <= 180)
                    {
                        controllers[(int)PIDList.Aileron].SetPoint = controllers[(int)PIDList.HdgBank].Response(FlightData.heading);
                        controllers[(int)PIDList.Rudder].SetPoint = controllers[(int)PIDList.HdgYaw].Response(FlightData.heading);
                    }
                    else if (controllers[(int)PIDList.HdgBank].SetPoint - FlightData.heading < -180)
                    {
                        controllers[(int)PIDList.Aileron].SetPoint = controllers[(int)PIDList.HdgBank].Response(FlightData.heading - 360);
                        controllers[(int)PIDList.Rudder].SetPoint = controllers[(int)PIDList.HdgYaw].Response(FlightData.heading - 360);
                    }
                    else if (controllers[(int)PIDList.HdgBank].SetPoint - FlightData.heading > 180)
                    {
                        controllers[(int)PIDList.Aileron].SetPoint = controllers[(int)PIDList.HdgBank].Response(FlightData.heading + 360);
                        controllers[(int)PIDList.Rudder].SetPoint = controllers[(int)PIDList.HdgYaw].Response(FlightData.heading + 360);
                    }
                }
                else
                {
                    controllers[(int)PIDList.Aileron].SetPoint = 0;
                    controllers[(int)PIDList.Rudder].SetPoint = 0;
                }
                state.roll = (float)Functions.Clamp(controllers[(int)PIDList.Aileron].Response(FlightData.roll) + state.roll, -1, 1);
                state.yaw = (float)controllers[(int)PIDList.Rudder].Response(FlightData.yaw);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    controllers[(int)PIDList.VertSpeed].SetPoint = -controllers[(int)PIDList.Altitude].Response(FlightData.thisVessel.altitude);

                controllers[(int)PIDList.Elevator].SetPoint = -controllers[(int)PIDList.VertSpeed].Response(FlightData.thisVessel.verticalSpeed);
                state.pitch = (float)-controllers[(int)PIDList.Elevator].Response(FlightData.AoA);
            }
        }

        private void hdgToggle()
        {
            bHdgWasActive = bHdgActive;
            if (bHdgActive)
            {
                controllers[(int)PIDList.HdgBank].SetPoint = FlightData.heading;
                MainWindow.targetHeading = FlightData.heading.ToString("N2");
            }
            else
            {
                controllers[(int)PIDList.HdgBank].Clear();
                controllers[(int)PIDList.HdgYaw].Clear();
                controllers[(int)PIDList.Aileron].Clear();
                controllers[(int)PIDList.Rudder].Clear();
            }
        }

        private void vertToggle()
        {
            bVertWasActive = bVertActive;
            if (bVertActive)
            {
                if (bAltitudeHold)
                {
                    controllers[(int)PIDList.Altitude].SetPoint = FlightData.thisVessel.altitude;
                    MainWindow.targetVert = controllers[(int)PIDList.Altitude].SetPoint.ToString("N1");
                }
                else
                {
                    controllers[(int)PIDList.VertSpeed].SetPoint = FlightData.thisVessel.verticalSpeed;
                    MainWindow.targetVert = controllers[(int)PIDList.VertSpeed].SetPoint.ToString("N3");
                }
            }
            else
            {
                controllers[(int)PIDList.Altitude].Clear();
                controllers[(int)PIDList.HdgBank].Clear();
                controllers[(int)PIDList.Elevator].Clear();
            }
        }

        private void altToggle()
        {
            bWasAltitudeHold = bAltitudeHold;
            if (bAltitudeHold)
            {
                controllers[(int)PIDList.Altitude].SetPoint = FlightData.thisVessel.altitude;
                MainWindow.targetVert = controllers[(int)PIDList.Altitude].SetPoint.ToString("N1");
            }
            else
            {
                controllers[(int)PIDList.VertSpeed].SetPoint = FlightData.thisVessel.verticalSpeed;
                MainWindow.targetVert = controllers[(int)PIDList.VertSpeed].SetPoint.ToString("N3");
            }
        }

        private void keyPressChanges()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Map)
            {
                bHdgWasActive = false; // reset heading/vert lock on unpausing
                bVertWasActive = false;
                bPause = !bPause;
            }

            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.X))
            {
                controllers[(int)PIDList.VertSpeed].SetPoint = 0;
                bAltitudeHold = false;
                bWingLeveller = true;
                MainWindow.targetVert = "0";
            }

            double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1;
            bool bFineControl = FlightInputHandler.fetch.precisionMode;
            if (GameSettings.YAW_LEFT.GetKey() && bHdgActive)
            {
                double hdg = double.Parse(MainWindow.targetHeading);
                hdg -= bFineControl ? 0.04 / scale : 0.4 * scale;
                if (hdg < 0)
                    hdg += 360;
                controllers[(int)PIDList.HdgBank].SetPoint = hdg;
                controllers[(int)PIDList.HdgYaw].SetPoint = hdg;
                MainWindow.targetHeading = hdg.ToString();
            }
            else if (GameSettings.YAW_RIGHT.GetKey() && bHdgActive)
            {
                double hdg = double.Parse(MainWindow.targetHeading);
                hdg += bFineControl ? 0.04 / scale : 0.4 * scale;
                if (hdg > 360)
                    hdg -= 360;
                controllers[(int)PIDList.HdgBank].SetPoint = hdg;
                controllers[(int)PIDList.HdgYaw].SetPoint = hdg;
                MainWindow.targetHeading = hdg.ToString();
            }

            if (GameSettings.PITCH_DOWN.GetKey() && bVertActive)
            {
                double vert = double.Parse(MainWindow.targetVert);
                if (bAltitudeHold)
                {
                    vert -= bFineControl ? 0.4 / scale : 4 * scale;
                    if (vert < 0)
                        vert = 0;
                    controllers[(int)PIDList.Altitude].SetPoint = vert;
                }
                else
                {
                    vert -= bFineControl ? 0.04 / scale : 0.4 * scale;
                    controllers[(int)PIDList.VertSpeed].SetPoint = vert;
                }
                MainWindow.targetVert = vert.ToString();
            }
            if (GameSettings.PITCH_UP.GetKey() && bVertActive)
            {
                double vert = double.Parse(MainWindow.targetVert);
                if (bAltitudeHold)
                {
                    vert += bFineControl ? 0.4 / scale : 4 * scale;
                    controllers[(int)PIDList.Altitude].SetPoint = vert;
                }
                else
                {
                    vert += bFineControl ? 0.04 / scale : 0.4 * scale;
                    controllers[(int)PIDList.VertSpeed].SetPoint = vert;
                }
                MainWindow.targetVert = vert.ToString();
            }
        }
    }
}
