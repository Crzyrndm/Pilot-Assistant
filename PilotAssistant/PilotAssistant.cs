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
        private static PID_Controller[] controllers = new PID_Controller[7];

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
            FlightData.thisVessel = FlightGlobals.ActiveVessel;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            // Init UI
            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, GUI);
        }

        public static PID_Controller GetController(PIDList id)
        {
            // Make accessing controllers a bit cleaner
            return controllers[(int)id];
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnFlyByWire -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            PresetManager.SavePresetsToFile();
            isHdgActive = false;
            isVertActive = false;
            //controllers.Clear();
            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = null; 
        }

        public void Update()
        {
            keyPressChanges();
        }

        public void FixedUpdate()
        {
        }

        public void GUI()
        {
            if (!AppLauncher.AppLauncherInstance.bDisplayAssistant)
                return;
            PAMainWindow.Draw();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            FlightData.updateAttitude();

            if (isPaused)
                return;
            
            // Heading Control
            if (isHdgActive)
            {
                if (!isWingLvlActive)
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
                    GetController(PIDList.Aileron).SetPoint = 0;
                    GetController(PIDList.Rudder).SetPoint = 0;
                }
                state.roll = (float)Functions.Clamp(GetController(PIDList.Aileron).Response(FlightData.roll) + state.roll, -1, 1);
                state.yaw = (float)GetController(PIDList.Rudder).Response(FlightData.yaw);
            }

            if (isVertActive)
            {
                // Set requested vertical speed
                if (isAltitudeHoldActive)
                    GetController(PIDList.VertSpeed).SetPoint = -GetController(PIDList.Altitude).Response(FlightData.thisVessel.altitude);

                GetController(PIDList.Elevator).SetPoint = -GetController(PIDList.VertSpeed).Response(FlightData.thisVessel.verticalSpeed);
                state.pitch = (float)-GetController(PIDList.Elevator).Response(FlightData.AoA);
            }
        }

        public static bool IsPaused() { return isPaused; }
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
        }
        
        public static void SetVertActive()
        {
            // Set vertical control on, use vertical speed value in GUI
            double newSpd = PAMainWindow.GetTargetVerticalSpeed();
            GetController(PIDList.VertSpeed).SetPoint = newSpd;
            isVertActive = true;
            isWingLvlActive = false;
        }
        
        public static void SetAltitudeHoldActive()
        {
            // Set vertical control on, use altitude value in GUI
            double newAlt = PAMainWindow.GetTargetAltitude();
            GetController(PIDList.Altitude).SetPoint = newAlt;
            isVertActive = true;
            isAltitudeHoldActive = true;
        }
        
        public static void ToggleHdg()
        {
            isHdgActive = !isHdgActive;
            if (isHdgActive)
            {
                // Set heading control on, use current heading
                GetController(PIDList.HdgBank).SetPoint = FlightData.heading;
                GetController(PIDList.HdgYaw).SetPoint = FlightData.heading; // added
                PAMainWindow.SetTargetHeading(FlightData.heading);
                //FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                //SurfSAS.ActivitySwitch(false);
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
                GetController(PIDList.HdgBank).SetPoint = FlightData.heading;
                GetController(PIDList.HdgYaw).SetPoint = FlightData.heading;
            }
        }
        
        public static void ToggleVert()
        {
            isVertActive = !isVertActive;
            if (isVertActive)
            {
                if (isAltitudeHoldActive)
                {
                    GetController(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                    PAMainWindow.SetTargetAltitude(FlightData.thisVessel.altitude);
                }
                else
                {
                    GetController(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                    PAMainWindow.SetTargetVerticalSpeed(FlightData.thisVessel.verticalSpeed);
                }
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
                GetController(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                PAMainWindow.SetTargetAltitude(FlightData.thisVessel.altitude);
            }
            else
            {
                GetController(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                PAMainWindow.SetTargetVerticalSpeed(FlightData.thisVessel.verticalSpeed);
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

        private void keyPressChanges()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.Map)
            {
                //bHdgWasActive = false; // reset heading/vert lock on unpausing
                //bVertWasActive = false;
                isPaused = !isPaused;
                if (!isPaused)
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                
                if (isPaused)
                    Messaging.statusMessage(0);
                else
                    Messaging.statusMessage(1);
            }

            if (GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                if (!isPaused && !FlightData.thisVessel.ctrlState.killRot && !SurfSAS.ActivityCheck())
                {
                    isPaused = true;
                    Messaging.statusMessage(2);
                }
                else if (isPaused && (FlightData.thisVessel.ctrlState.killRot || SurfSAS.ActivityCheck()))
                {
                    isPaused = false;
                    Messaging.statusMessage(3);
                }
            }

            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.X))
            {
                GetController(PIDList.VertSpeed).SetPoint = 0;
                isAltitudeHoldActive = false;
                //bWasAltitudeHold = false;
                isWingLvlActive = true;
                PAMainWindow.SetTargetVerticalSpeed(0.0);
                Messaging.statusMessage(4);
            }

            if (!isPaused)
            {
                double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1;
                bool bFineControl = FlightInputHandler.fetch.precisionMode;
                if (GameSettings.YAW_LEFT.GetKey() && isHdgActive)
                {
                    //double hdg = double.Parse(PAMainWindow.targetHeading);
                    double hdg = PAMainWindow.GetTargetHeading();
                    hdg -= bFineControl ? 0.04 / scale : 0.4 * scale;
                    if (hdg < 0)
                        hdg += 360;
                    GetController(PIDList.HdgBank).SetPoint = hdg;
                    GetController(PIDList.HdgYaw).SetPoint = hdg;
                    //PAMainWindow.targetHeading = hdg.ToString();
                    PAMainWindow.SetTargetHeading(hdg);
                }
                else if (GameSettings.YAW_RIGHT.GetKey() && isHdgActive)
                {
                    //double hdg = double.Parse(PAMainWindow.targetHeading);
                    double hdg = PAMainWindow.GetTargetHeading();
                    hdg += bFineControl ? 0.04 / scale : 0.4 * scale;
                    if (hdg > 360)
                        hdg -= 360;
                    GetController(PIDList.HdgBank).SetPoint = hdg;
                    GetController(PIDList.HdgYaw).SetPoint = hdg;
                    //PAMainWindow.targetHeading = hdg.ToString();
                    PAMainWindow.SetTargetHeading(hdg);
                }

                if (GameSettings.PITCH_DOWN.GetKey() && isVertActive)
                {
                    //double vert = double.Parse(PAMainWindow.targetVert);
                    double vert = PAMainWindow.GetTargetVerticalSpeed();
                    if (isAltitudeHoldActive)
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
                    //PAMainWindow.targetVert = vert.ToString();
                    PAMainWindow.SetTargetVerticalSpeed(vert);
                }
                if (GameSettings.PITCH_UP.GetKey() && isVertActive)
                {
                    //double vert = double.Parse(PAMainWindow.targetVert);
                    double vert = PAMainWindow.GetTargetVerticalSpeed();
                    if (isAltitudeHoldActive)
                    {
                        vert += bFineControl ? 0.4 / scale : 4 * scale;
                        GetController(PIDList.Altitude).SetPoint = vert;
                    }
                    else
                    {
                        vert += bFineControl ? 0.04 / scale : 0.4 * scale;
                        GetController(PIDList.VertSpeed).SetPoint = vert;
                    }
                    //PAMainWindow.targetVert = vert.ToString();
                    PAMainWindow.SetTargetVerticalSpeed(vert);
                }
            }
        }
    }
}
