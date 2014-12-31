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
        private static FlightData flightData;
        private static PID_Controller[] controllers = new PID_Controller[7];

        // Whether PA has been paused by the user, does not account for SAS being turned on.
        // Use SurfSAS.CheckSAS() as well. 
        private static bool isPaused = false;
        // RollController
        private static bool isHdgActive = false;
        // PitchController
        private static bool isVertActive = false;
        // Altitude / vertical speed
        private static bool isAltitudeHoldActive = false;
        // Wing leveller / Heading control
        private static bool isWingLvlActive = false;

        public void Start()
        {
            PID_Controller headingBankController = new PID.PID_Controller(2, 0.1, 0, -30, 30, -0.5, 0.5);
            PID_Controller headingYawController = new PID.PID_Controller(0, 0, 0.01, -2, 2, -0.5, 0.5);
            PID_Controller aileronController = new PID.PID_Controller(0.02, 0.005, 0.01, -1, 1, -0.4, 0.4);
            PID_Controller rudderController = new PID.PID_Controller(0.1, 0.08, 0.05, -1, 1, -0.4, 0.4);
            PID_Controller altitudeToClimbRate = new PID.PID_Controller(0.15, 0.01, 0, -50, 50, -0.01, 0.01);
            PID_Controller aoaController = new PID.PID_Controller(2, 0.8, 2, -10, 10, -5, 5);
            PID_Controller elevatorController = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.4, 0.4);
            controllers[(int)PIDList.HdgBank] = headingBankController;
            controllers[(int)PIDList.HdgYaw] = headingYawController;
            controllers[(int)PIDList.Aileron] = aileronController;
            controllers[(int)PIDList.Rudder] = rudderController;
            controllers[(int)PIDList.Altitude] = altitudeToClimbRate;
            controllers[(int)PIDList.VertSpeed] = aoaController;
            controllers[(int)PIDList.Elevator] = elevatorController;

            // PID inits
            aileronController.InMax = 180;
            aileronController.InMin = -180;
            altitudeToClimbRate.InMin = 0;

            // Set up a default preset that can be easily returned to
            PresetManager.InitDefaultPATuning(controllers);

            // register vessel
            flightData = new FlightData(FlightGlobals.ActiveVessel);
            flightData.Vessel.OnAutopilotUpdate += new FlightInputCallback(VesselController);
            GameEvents.onVesselChange.Add(VesselSwitch);

            RenderingManager.AddToPostDrawQueue(5, DrawGUI);
        }

        public static PID_Controller GetController(PIDList id)
        {
            // Make accessing controllers a bit cleaner
            return controllers[(int)id];
        }

        private void VesselSwitch(Vessel v)
        {
            flightData.Vessel.OnAutopilotUpdate -= new FlightInputCallback(VesselController);
            flightData.Vessel = v;
            flightData.Vessel.OnAutopilotUpdate += new FlightInputCallback(VesselController);
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, DrawGUI);
            GameEvents.onVesselChange.Remove(VesselSwitch);
            PresetManager.SavePresetsToFile();
            isHdgActive = false;
            isVertActive = false;

            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = null;

            flightData.Vessel.OnAutopilotUpdate -= new FlightInputCallback(VesselController);
        }

        public void Update()
        {
            KeyPressChanges();
        }

        public void DrawGUI()
        {
            PAMainWindow.Draw(AppLauncher.AppLauncherInstance.bDisplayAssistant);
        }

        private static void VesselController(FlightCtrlState state)
        {
            flightData.UpdateAttitude();

            if (isPaused || SASCheck())
                return;
            
            // Heading Control
            if (isHdgActive)
            {
                if (!isWingLvlActive)
                {
                    if (GetController(PIDList.HdgBank).SetPoint - flightData.Heading >= -180 && GetController(PIDList.HdgBank).SetPoint - flightData.Heading <= 180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading);
                    }
                    else if (GetController(PIDList.HdgBank).SetPoint - flightData.Heading < -180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading - 360);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading - 360);
                    }
                    else if (GetController(PIDList.HdgBank).SetPoint - flightData.Heading > 180)
                    {
                        GetController(PIDList.Aileron).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading + 360);
                        GetController(PIDList.HdgYaw).SetPoint = GetController(PIDList.HdgBank).Response(flightData.Heading + 360);
                    }

                    GetController(PIDList.Rudder).SetPoint = -GetController(PIDList.HdgYaw).Response(flightData.Yaw);
                }
                else
                {
                    GetController(PIDList.Aileron).SetPoint = 0;
                    GetController(PIDList.Rudder).SetPoint = 0;
                }
                state.roll = (float)Functions.Clamp(GetController(PIDList.Aileron).Response(flightData.Roll) + state.roll, -1, 1);
                state.yaw = (float)GetController(PIDList.Rudder).Response(flightData.Yaw);
            }

            if (isVertActive)
            {
                // Set requested vertical speed
                if (isAltitudeHoldActive)
                    GetController(PIDList.VertSpeed).SetPoint = -GetController(PIDList.Altitude).Response(flightData.Vessel.altitude);

                GetController(PIDList.Elevator).SetPoint = -GetController(PIDList.VertSpeed).Response(flightData.Vessel.verticalSpeed);
                state.pitch = (float)-GetController(PIDList.Elevator).Response(flightData.AoA);
            }
        }

        public static FlightData GetFlightData() { return flightData; } 
        public static bool IsPaused() { return isPaused || SASCheck(); }
        public static bool IsHdgActive() { return isHdgActive; }
        public static bool IsWingLvlActive() { return isWingLvlActive; }
        public static bool IsVertActive() { return isVertActive; }
        public static bool IsAltitudeHoldActive() { return isAltitudeHoldActive; }
        
        public static void SetHdgActive()
        {
            // Set heading control on, use values in GUI
            double newHdg = PAMainWindow.GetTargetHeading();
            GetController(PIDList.HdgBank).SetPoint = newHdg;
            GetController(PIDList.HdgYaw).SetPoint = newHdg;
            isHdgActive = true;
            SurfSAS.SetOperational(false);
            isPaused = false;
        }
        
        public static void SetVertSpeedActive()
        {
            // Set vertical control on, use vertical speed value in GUI
            double newSpd = PAMainWindow.GetTargetVerticalSpeed();
            GetController(PIDList.VertSpeed).SetPoint = newSpd;
            isVertActive = true;
            isAltitudeHoldActive = false;
            SurfSAS.SetOperational(false);
            isPaused = false;
        }
        
        public static void SetAltitudeHoldActive()
        {
            // Set vertical control on, use altitude value in GUI
            double newAlt = PAMainWindow.GetTargetAltitude();
            GetController(PIDList.Altitude).SetPoint = newAlt;
            isVertActive = true;
            isAltitudeHoldActive = true;
            SurfSAS.SetOperational(false);
            isPaused = false;
        }
        
        public static void ToggleHdg()
        {
            isHdgActive = !isHdgActive;
            if (isHdgActive)
            {
                // Set heading control on, use current heading
                GetController(PIDList.HdgBank).SetPoint = flightData.Heading;
                GetController(PIDList.HdgYaw).SetPoint = flightData.Heading; // added
                PAMainWindow.SetTargetHeading(flightData.Heading);
                SurfSAS.SetOperational(false);
                isPaused = false;
            }
            else
            {
                // Turn it off
                GetController(PIDList.HdgBank).Clear();
                GetController(PIDList.HdgYaw).Clear();
                GetController(PIDList.Aileron).Clear();
                GetController(PIDList.Rudder).Clear();
            }
        }
        
        public static void ToggleWingLvl()
        {
            isWingLvlActive = !isWingLvlActive;
            if (!isWingLvlActive)
            {
                GetController(PIDList.HdgBank).SetPoint = flightData.Heading;
                GetController(PIDList.HdgYaw).SetPoint = flightData.Heading;
            }
        }
        
        public static void ToggleVert()
        {
            isVertActive = !isVertActive;
            if (isVertActive)
            {
                if (isAltitudeHoldActive)
                {
                    GetController(PIDList.Altitude).SetPoint = flightData.Vessel.altitude;
                    PAMainWindow.SetTargetAltitude(flightData.Vessel.altitude);
                }
                else
                {
                    GetController(PIDList.VertSpeed).SetPoint = flightData.Vessel.verticalSpeed;
                    PAMainWindow.SetTargetVerticalSpeed(flightData.Vessel.verticalSpeed);
                }
                SurfSAS.SetOperational(false);
                isPaused = false;
            }
            else
            {
                // Turn it off
                GetController(PIDList.Altitude).Clear();
                GetController(PIDList.HdgBank).Clear();
                GetController(PIDList.Elevator).Clear();
            }
        }
        
        public static void ToggleAltitudeHold()
        {
            isAltitudeHoldActive = !isAltitudeHoldActive;
            if (isAltitudeHoldActive)
            {
                GetController(PIDList.Altitude).SetPoint = flightData.Vessel.altitude;
                PAMainWindow.SetTargetAltitude(flightData.Vessel.altitude);
            }
            else
            {
                GetController(PIDList.VertSpeed).SetPoint = flightData.Vessel.verticalSpeed;
                PAMainWindow.SetTargetVerticalSpeed(flightData.Vessel.verticalSpeed);
            }
        }

        public static void UpdatePreset()
        {
            PAPreset p = PresetManager.GetActivePAPreset();
            if (p != null)
                p.Update(controllers);
            PresetManager.SavePresetsToFile();
        }
        
        public static void RegisterNewPreset(string name)
        {
            PresetManager.RegisterPAPreset(controllers, name);
        }
        
        public static void LoadPreset(PAPreset p)
        {
            PresetManager.LoadPAPreset(controllers, p);
        }

        private static bool SASCheck()
        {
            return SurfSAS.IsSSASOperational() || SurfSAS.IsStockSASOperational();
        }

        private static void KeyPressChanges()
        {
            // Respect current input locks
            if (InputLockManager.IsLocked(ControlTypes.ALL_SHIP_CONTROLS))
                return;
            bool mod = GameSettings.MODIFIER_KEY.GetKey();

            // Pause key
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // When active and paused, unpause.
                if ((IsHdgActive() || IsVertActive()) && (isPaused || SASCheck()))
                {
                    isPaused = false;
                    SurfSAS.SetOperational(false);
                    Messaging.PostMessage("Pilot assistant unpaused.");
                }
                // Otherwise, when active and not paused, pause.
                else if (IsHdgActive() || IsVertActive())
                {
                    isPaused = true;
                    Messaging.PostMessage("Pilot assistant paused.");
                }
            }

            // SAS activation change, only show messages when active and not paused.
            if ((GameSettings.SAS_TOGGLE.GetKeyDown() || GameSettings.SAS_HOLD.GetKeyDown() || GameSettings.SAS_HOLD.GetKeyUp())
                && !isPaused && (IsHdgActive() || IsVertActive()))
            {
                if (SASCheck())
                    Messaging.PostMessage("Pilot Assistant control handed to SAS.");
                else
                    Messaging.PostMessage("Pilot Assistant control retrieved from SAS.");
            }

            // Level wings and set vertical speed to 0. 
            if (mod && Input.GetKeyDown(KeyCode.X))
            {
                // Set controller and modes. 
                GetController(PIDList.VertSpeed).SetPoint = 0;
                isVertActive = true;
                isAltitudeHoldActive = false;
                isHdgActive = true;
                isWingLvlActive = true;
                // Update GUI
                PAMainWindow.SetTargetVerticalSpeed(0.0);
                // Make sure we are not paused and SAS is off. 
                isPaused = false;
                SurfSAS.SetOperational(false);
                Messaging.PostMessage("Pilot Assistant is levelling off.");
            }

            // Only update target when not paused.
            if (!isPaused && !SASCheck())
            {
                double scale;
                if (FlightInputHandler.fetch.precisionMode)
                    scale = mod ? 0.01 : 0.1;
                else
                    scale = mod ? 10 : 1;

                // Update heading based on user control input
                if (isHdgActive && !isWingLvlActive)
                {
                    double hdg = PAMainWindow.GetTargetHeading();
                    if (GameSettings.YAW_LEFT.GetKey())
                        hdg -= 0.4 * scale;
                    else if (GameSettings.YAW_RIGHT.GetKey())
                        hdg += 0.4 * scale;
                    else if (!GameSettings.AXIS_YAW.IsNeutral())
                        hdg += 0.4 * scale * GameSettings.AXIS_YAW.GetAxis();

                    if (hdg < 0)
                        hdg += 360;
                    else if (hdg > 360)
                        hdg -= 360;
                    GetController(PIDList.HdgBank).SetPoint = hdg;
                    GetController(PIDList.HdgYaw).SetPoint = hdg;
                    PAMainWindow.SetTargetHeading(hdg);
                }

                // Update target vertical speed based on user control input
                if (isVertActive && !isAltitudeHoldActive)
                {
                    double vert = PAMainWindow.GetTargetVerticalSpeed();
                    if (GameSettings.PITCH_DOWN.GetKey())
                        vert -= 0.4 * scale;
                    else if (GameSettings.PITCH_UP.GetKey())
                        vert += 0.4 * scale;
                    else if (!GameSettings.AXIS_PITCH.IsNeutral())
                        vert += 0.4 * scale * GameSettings.AXIS_PITCH.GetAxis();

                    GetController(PIDList.VertSpeed).SetPoint = vert;
                    PAMainWindow.SetTargetVerticalSpeed(vert);
                }

                // Update target altitude based on user control input
                if (isVertActive && isAltitudeHoldActive)
                {
                    double alt = PAMainWindow.GetTargetAltitude();
                    if (GameSettings.PITCH_DOWN.GetKey())
                        alt -= 4 * scale;
                    else if (GameSettings.PITCH_UP.GetKey())
                        alt += 4 * scale;
                    else if (!GameSettings.AXIS_PITCH.IsNeutral())
                        alt += 4 * scale * GameSettings.AXIS_PITCH.GetAxis();

                    if (alt < 0)
                        alt = 0;
                    GetController(PIDList.Altitude).SetPoint = alt;
                    PAMainWindow.SetTargetAltitude(alt);
                }
            }
        }
    }
}
