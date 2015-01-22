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
    public enum PIDList
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
        static bool init = false; // create the default the first time through
        internal static PID_Controller[] controllers = new PID_Controller[7];

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
            if (!init)
                Initialise();

            PresetManager.loadAssistantPreset();
            
            // register vessel
            FlightData.thisVessel = FlightGlobals.ActiveVessel;
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            // Init UI
            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, drawGUI);
        }

        void Initialise()
        {
            controllers[(int)PIDList.HdgBank] = new PID.PID_Controller(2, 0.1, 0, -30, 30, -0.5, 0.5);
            controllers[(int)PIDList.HdgYaw] = new PID.PID_Controller(0, 0, 0.01, -2, 2, -0.5, 0.5);
            controllers[(int)PIDList.Aileron] = new PID.PID_Controller(0.02, 0.005, 0.01, -1, 1, -0.4, 0.4);
            controllers[(int)PIDList.Rudder] = new PID.PID_Controller(0.1, 0.08, 0.05, -1, 1, -0.4, 0.4);
            controllers[(int)PIDList.Altitude] = new PID.PID_Controller(0.15, 0.01, 0, -50, 50, -0.01, 0.01);
            controllers[(int)PIDList.VertSpeed] = new PID.PID_Controller(2, 0.8, 2, -10, 10, -5, 5);
            controllers[(int)PIDList.Elevator] = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.4, 0.4);

            // PID inits
            Utils.GetAsst(PIDList.Aileron).InMax = 180;
            Utils.GetAsst(PIDList.Aileron).InMin = -180;
            Utils.GetAsst(PIDList.Altitude).InMin = 0;

            // Set up a default preset that can be easily returned to
            PresetManager.Instance.defaultPATuning = new PresetPA(controllers, "Default");

            init = true;
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
            Array.Clear(controllers, 0, 7);
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
                    if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading >= -180 && Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading <= 180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading);
                    }
                    else if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading < -180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading - 360);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading - 360);
                    }
                    else if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading > 180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading + 360);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).Response(FlightData.heading + 360);
                    }

                    Utils.GetAsst(PIDList.Rudder).SetPoint = -Utils.GetAsst(PIDList.HdgYaw).Response(FlightData.yaw);
                }
                else
                {
                    bWasWingLeveller = true;
                    Utils.GetAsst(PIDList.Aileron).SetPoint = 0;
                    Utils.GetAsst(PIDList.Rudder).SetPoint = 0;
                }
                state.roll = (float)Utils.Clamp(Utils.GetAsst(PIDList.Aileron).Response(FlightData.roll) + state.roll, -1, 1);
                state.yaw = (float)Utils.GetAsst(PIDList.Rudder).Response(FlightData.yaw);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    Utils.GetAsst(PIDList.VertSpeed).SetPoint = -Utils.GetAsst(PIDList.Altitude).Response(FlightData.thisVessel.altitude);

                Utils.GetAsst(PIDList.Elevator).SetPoint = -Utils.GetAsst(PIDList.VertSpeed).Response(FlightData.thisVessel.verticalSpeed);
                state.pitch = (float)-Utils.GetAsst(PIDList.Elevator).Response(FlightData.AoA);
            }
        }


        public static bool IsPaused() { return bPause || SASMonitor(); }

        private void hdgToggle()
        {
            bHdgWasActive = bHdgActive;
            if (bHdgActive)
            {
                Utils.GetAsst(PIDList.HdgBank).SetPoint = FlightData.heading;
                PAMainWindow.targetHeading = FlightData.heading.ToString("N2");

                bPause = false;
            }
            else
            {
                Utils.GetAsst(PIDList.HdgBank).Clear();
                Utils.GetAsst(PIDList.HdgYaw).Clear();
                Utils.GetAsst(PIDList.Aileron).Clear();
                Utils.GetAsst(PIDList.Rudder).Clear();
            }
        }

        private void vertToggle()
        {
            bVertWasActive = bVertActive;
            if (bVertActive)
            {
                if (bAltitudeHold)
                {
                    Utils.GetAsst(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                    PAMainWindow.targetVert = Utils.GetAsst(PIDList.Altitude).SetPoint.ToString("N1");
                }
                else
                {
                    Utils.GetAsst(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                    PAMainWindow.targetVert = Utils.GetAsst(PIDList.VertSpeed).SetPoint.ToString("N3");
                }

                bPause = false;
            }
            else
            {
                Utils.GetAsst(PIDList.Altitude).Clear();
                Utils.GetAsst(PIDList.HdgBank).Clear();
                Utils.GetAsst(PIDList.Elevator).Clear();
            }
        }

        private void altToggle()
        {
            bWasAltitudeHold = bAltitudeHold;
            if (bAltitudeHold)
            {
                Utils.GetAsst(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                PAMainWindow.targetVert = Utils.GetAsst(PIDList.Altitude).SetPoint.ToString("N1");
            }
            else
            {
                Utils.GetAsst(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                PAMainWindow.targetVert = Utils.GetAsst(PIDList.VertSpeed).SetPoint.ToString("N2");
            }
        }

        private void wingToggle()
        {
            bWasWingLeveller = bWingLeveller;
            if (!bWingLeveller)
            {
                Utils.GetAsst(PIDList.HdgBank).SetPoint = FlightData.heading;
                Utils.GetAsst(PIDList.HdgYaw).SetPoint = FlightData.heading;
                PAMainWindow.targetHeading = Utils.GetAsst(PIDList.HdgBank).SetPoint.ToString("N2");
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
            }

            if (mod && Input.GetKeyDown(KeyCode.X))
            {
                Utils.GetAsst(PIDList.VertSpeed).SetPoint = 0;
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
                        Utils.GetAsst(PIDList.HdgBank).SetPoint = hdg;
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = hdg;
                        PAMainWindow.targetHeading = hdg.ToString();
                    }
                    else if (GameSettings.YAW_RIGHT.GetKey())
                    {
                        double hdg = double.Parse(PAMainWindow.targetHeading);
                        hdg += bFineControl ? 0.04 / scale : 0.4 * scale;
                        if (hdg > 360)
                            hdg -= 360;
                        Utils.GetAsst(PIDList.HdgBank).SetPoint = hdg;
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = hdg;
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
                            Utils.GetAsst(PIDList.Altitude).SetPoint = vert;
                        }
                        else
                        {
                            vert -= bFineControl ? 0.04 / scale : 0.4 * scale;
                            Utils.GetAsst(PIDList.VertSpeed).SetPoint = vert;
                        }
                        PAMainWindow.targetVert = vert.ToString();
                    }
                    else if (GameSettings.PITCH_UP.GetKey())
                    {
                        double vert = double.Parse(PAMainWindow.targetVert);
                        if (bAltitudeHold)
                        {
                            vert += bFineControl ? 0.4 / scale : 4 * scale;
                            Utils.GetAsst(PIDList.Altitude).SetPoint = vert;
                        }
                        else
                        {
                            vert += bFineControl ? 0.04 / scale : 0.4 * scale;
                            Utils.GetAsst(PIDList.VertSpeed).SetPoint = vert;
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
