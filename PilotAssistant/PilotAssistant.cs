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

    [Flags]
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
        internal static bool bWasWingLeveller = false;

        public void Start()
        {
            PID_Controller HeadingBankController = new PID.PID_Controller(2, 0.1, 0, -30, 30, -0.5, 0.5);
            controllers.Add(HeadingBankController);
            PID_Controller HeadingYawController = new PID.PID_Controller(0, 0, 0.01, -2, 2, -0.5, 0.5);
            controllers.Add(HeadingYawController);
            PID_Controller AileronController = new PID.PID_Controller(0.02, 0.005, 0.01, -1, 1, -0.4, 0.4);
            controllers.Add(AileronController);
            PID_Controller RudderController = new PID.PID_Controller(0.1, 0.08, 0.05, -1, 1, -0.4, 0.4);
            controllers.Add(RudderController);
            PID_Controller AltitudeToClimbRate = new PID.PID_Controller(0.15, 0.01, 0, -50, 50, -0.01, 0.01);
            controllers.Add(AltitudeToClimbRate);
            PID_Controller AoAController = new PID.PID_Controller(2, 0.8, 2, -10, 10, -5, 5);
            controllers.Add(AoAController);
            PID_Controller ElevatorController = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.4, 0.4);
            controllers.Add(ElevatorController);

            // PID inits
            AileronController.InMax = 180;
            AileronController.InMin = -180;
            AltitudeToClimbRate.InMin = 0;

            // Set up a default preset that can be easily returned to
            PresetManager.defaultPATuning = new PresetPA(controllers, "Default");

            if (PresetManager.activePAPreset == null)
                PresetManager.activePAPreset = PresetManager.defaultPATuning;
            else if (PresetManager.activePAPreset != PresetManager.defaultPATuning)
            {
                PresetManager.loadPAPreset(PresetManager.activePAPreset);
                Messaging.statusMessage(5);
            }
            
            // register vessel
            FlightData.thisVessel = FlightGlobals.ActiveVessel;
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            // Init UI
            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, drawGUI);
        }

        public static PID_Controller GetController(PIDList id)
        {
            return controllers[(int)id];
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(vesselController);
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, drawGUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            PresetManager.saveToFile();
            bHdgActive = false;
            bVertActive = false;
            controllers.Clear();
        }

        public void Update()
        {
            keyPressChanges();

            if (IsPaused())
                return;

            if (bHdgActive != bHdgWasActive)
                hdgToggle();

            if (bVertActive != bVertWasActive)
                vertToggle();

            if (bAltitudeHold != bWasAltitudeHold)
                altToggle();

            if (bWingLeveller != bWasWingLeveller)
                wingToggle();
        }

        public void FixedUpdate()
        {
        }

        public void drawGUI()
        {
            if (!AppLauncher.AppLauncherFlight.bDisplayAssistant)
                return;

            if (GeneralUI.UISkin == null)
                GeneralUI.UISkin = UnityEngine.GUI.skin;

            PAMainWindow.Draw();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            FlightData.updateAttitude();

            if (IsPaused())
                return;
            
            // Heading Control
            if (bHdgActive)
            {
                if (!bWingLeveller && (FlightData.thisVessel.latitude < 88 && FlightData.thisVessel.latitude > -88))
                {
                    if (GetController(PIDList.HdgBank).SetPoint - FlightData.heading >= -180 && GetController(PIDList.HdgBank).SetPoint - FlightData.heading <= 180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading);
                    }
                    else if (GetController(PIDList.HdgBank).SetPoint - FlightData.heading < -180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading - 360);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading - 360);
                    }
                    else if (GetController(PIDList.HdgBank).SetPoint - FlightData.heading > 180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading + 360);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(FlightData.heading + 360);
                    }

                    GetController(PIDList.Rudder).SetPoint = -GetController(PIDList.HdgYaw).Response(FlightData.yaw);
                }
                else
                {
                    bWasWingLeveller = true;
                    GetController(PIDList.Aileron).SetPoint = 0;
                    GetController(PIDList.Rudder).SetPoint = 0;
                }
                state.roll = (float)Functions.Clamp(GetController(PIDList.Aileron).Response(FlightData.roll) + state.roll, -1, 1);
                state.yaw = (float)GetController(PIDList.Rudder).Response(FlightData.yaw);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    GetController(PIDList.VertSpeed).SetPoint = -GetController(PIDList.Altitude).Response(FlightData.thisVessel.altitude);

                GetController(PIDList.Elevator).SetPoint = -GetController(PIDList.VertSpeed).Response(FlightData.thisVessel.verticalSpeed);
                state.pitch = (float)-GetController(PIDList.Elevator).Response(FlightData.AoA);
            }
        }


        public static bool IsPaused() { return bPause || SASMonitor(); }

        private void hdgToggle()
        {
            bHdgWasActive = bHdgActive;
            if (bHdgActive)
            {
                GetController(PIDList.HdgBank).SetPoint = FlightData.heading;
                PAMainWindow.targetHeading = FlightData.heading.ToString("N2");

                bPause = false;
            }
            else
            {
                GetController(PIDList.HdgBank).Clear();
                GetController(PIDList.HdgYaw).Clear();
                GetController(PIDList.Aileron).Clear();
                GetController(PIDList.Rudder).Clear();
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
                    PAMainWindow.targetVert = controllers[(int)PIDList.Altitude].SetPoint.ToString("N1");
                }
                else
                {
                    controllers[(int)PIDList.VertSpeed].SetPoint = FlightData.thisVessel.verticalSpeed;
                    PAMainWindow.targetVert = controllers[(int)PIDList.VertSpeed].SetPoint.ToString("N3");
                }

                bPause = false;
            }
            else
            {
                GetController(PIDList.Altitude).Clear();
                GetController(PIDList.HdgBank).Clear();
                GetController(PIDList.Elevator).Clear();
            }
        }

        private void altToggle()
        {
            bWasAltitudeHold = bAltitudeHold;
            if (bAltitudeHold)
            {
                controllers[(int)PIDList.Altitude].SetPoint = FlightData.thisVessel.altitude;
                PAMainWindow.targetVert = controllers[(int)PIDList.Altitude].SetPoint.ToString("N1");
            }
            else
            {
                controllers[(int)PIDList.VertSpeed].SetPoint = FlightData.thisVessel.verticalSpeed;
                PAMainWindow.targetVert = controllers[(int)PIDList.VertSpeed].SetPoint.ToString("N2");
            }
        }

        private void wingToggle()
        {
            bWasWingLeveller = bWingLeveller;
            if (!bWingLeveller)
            {
                PilotAssistant.controllers[(int)PIDList.HdgBank].SetPoint = FlightData.heading;
                PilotAssistant.controllers[(int)PIDList.HdgYaw].SetPoint = FlightData.heading;
                PAMainWindow.targetHeading = controllers[(int)PIDList.HdgBank].SetPoint.ToString("N2");
            }
        }

        private void keyPressChanges()
        {
            if (InputLockManager.IsLocked(ControlTypes.ALL_SHIP_CONTROLS))
                return;

            bool mod = GameSettings.MODIFIER_KEY.GetKey();

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bHdgWasActive = false; // reset heading/vert lock on unpausing
                bVertWasActive = false;
                bPause = !bPause;
                if (!bPause)
                {
                    SurfSAS.setStockSAS(false);
                    SurfSAS.ActivitySwitch(false);
                }
                
                if (bPause)
                    Messaging.statusMessage(0);
                else
                    Messaging.statusMessage(1);
            }

            if (GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bHdgWasActive = false; // reset heading/vert lock on unpausing
                bVertWasActive = false;
                if (!bPause && FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] && SurfSAS.ActivityCheck())
                {
                    // prepare to rest these on SAS off
                    Messaging.statusMessage(2);
                }
                else if (bPause && (!FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] || !SurfSAS.ActivityCheck()))
                {
                    Messaging.statusMessage(3);
                }
            }

            if (mod && Input.GetKeyDown(KeyCode.X))
            {
                GetController(PIDList.VertSpeed).SetPoint = 0;
                bAltitudeHold = false;
                bWasAltitudeHold = false;
                bWingLeveller = true;
                PAMainWindow.targetVert = "0";
                Messaging.statusMessage(4);
            }

            if (!IsPaused())
            {
                double scale = mod ? 10 : 1;
                bool bFineControl = FlightInputHandler.fetch.precisionMode;
                if (bHdgActive)
                {
                    if (GameSettings.YAW_LEFT.GetKey())
                    {
                        double hdg = double.Parse(PAMainWindow.targetHeading);
                        hdg -= bFineControl ? 0.04 / scale : 0.4 * scale;
                        if (hdg < 0)
                            hdg += 360;
                        GetController(PIDList.HdgBank).SetPoint = hdg;
                        GetController(PIDList.HdgYaw).SetPoint = hdg;
                        PAMainWindow.targetHeading = hdg.ToString();
                    }
                    else if (GameSettings.YAW_RIGHT.GetKey())
                    {
                        double hdg = double.Parse(PAMainWindow.targetHeading);
                        hdg += bFineControl ? 0.04 / scale : 0.4 * scale;
                        if (hdg > 360)
                            hdg -= 360;
                        GetController(PIDList.HdgBank).SetPoint = hdg;
                        GetController(PIDList.HdgYaw).SetPoint = hdg;
                        PAMainWindow.targetHeading = hdg.ToString();
                    }
                }

                if (bVertActive)
                {
                    if (GameSettings.PITCH_DOWN.GetKey())
                    {
                        double vert = double.Parse(PAMainWindow.targetVert);
                        if (bAltitudeHold)
                        {
                            vert -= bFineControl ? 0.4 / scale : 4 * scale;
                            if (vert < 0)
                                vert = 0;
                            GetController(PIDList.Altitude).SetPoint = vert;
                        }
                        else
                        {
                            vert -= bFineControl ? 0.04 / scale : 0.4 * scale;
                            GetController(PIDList.VertSpeed).SetPoint = vert;
                        }
                        PAMainWindow.targetVert = vert.ToString();
                    }
                    else if (GameSettings.PITCH_UP.GetKey())
                    {
                        double vert = double.Parse(PAMainWindow.targetVert);
                        if (bAltitudeHold)
                        {
                            vert += bFineControl ? 0.4 / scale : 4 * scale;
                            GetController(PIDList.Altitude).SetPoint = vert;
                        }
                        else
                        {
                            vert += bFineControl ? 0.04 / scale : 0.4 * scale;
                            GetController(PIDList.VertSpeed).SetPoint = vert;
                        }
                        PAMainWindow.targetVert = vert.ToString();
                    }
                }
            }
        }

        internal static bool SASMonitor()
        {
            return (FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] || SurfSAS.ActivityCheck());
        }
    }
}
