using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using AppLauncher;
    using UI;
    using Presets;

    internal enum SASList
    {
        Pitch,
        Roll,
        Yaw
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class SurfSAS : MonoBehaviour
    {
        private static PID_Controller[] controllers = new PID_Controller[3]; 

        private static bool initialized = false;
        // Current mode
        private static bool ssasMode = false;
        // Whether SSAS is active
        private static bool isSSASActive = false;
        // Used to monitor the use of SAS_HOLD key
        private static bool ssasHoldKey = false;
        // Used to monitor user input, and pause SSAS on a per axis basis
        private static bool[] isPaused = new bool[3]; 

        private static float activationFadeRoll = 1;
        private static float activationFadePitch = 1;
        private static float activationFadeYaw = 1;

        public void Initialize()
        {
            // register vessel if not already
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // grab stock PID values
            if (FlightData.thisVessel.Autopilot.SAS.pidLockedPitch != null)
            {
                PresetManager.InitDefaultStockSASTuning();

                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                controllers[(int)SASList.Pitch] = pitch;
                controllers[(int)SASList.Roll] = roll;
                controllers[(int)SASList.Yaw] = yaw;

                PresetManager.InitDefaultSASTuning(controllers);
                
                initialized = true;
                isPaused[0] = isPaused[1] = isPaused[2] = false;
            }

            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, GUI);
        }

        public void OnDestroy()
        {
            initialized = false;
            ssasMode = false;
            isSSASActive = false;

            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = null;

            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
        }

        public void Update()
        {
            if (!initialized)
                Initialize();

            if (ssasMode)
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] = false;
            
            if (ssasMode && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                isSSASActive = !isSSASActive;
                // If the change made SSAS operational, update target
                if (IsSSASOperational())
                    updateTarget();
            }

            // Allow for temporarily enabling/disabling SAS
            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                ssasHoldKey = true;
                // If the change made SSAS operational, update target
                if (IsSSASOperational())
                    updateTarget();
            }
            if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                ssasHoldKey = false;
                // If the change made SSAS operational, update target
                if (IsSSASOperational())
                    updateTarget();
            }

            if (ssasMode && FlightData.thisVessel.staticPressure == 0)
            {
                ssasMode = false;
                // Try to seamlessly switch to stock SAS
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] = isSSASActive;
                isSSASActive = false;
            }

            PauseManager(); // manage activation of SAS axes depending on user input
        }

        public void GUI()
        {
            if (!AppLauncher.AppLauncherInstance.bDisplaySAS)
                return;
            SASMainWindow.Draw();
        }

        public void FixedUpdate()
        {
            if (IsSSASOperational())
            {
                FlightData.updateAttitude();

                float pitchResponse = -1 * (float)GetController(SASList.Pitch).Response(FlightData.pitch);

                float yawResponse = 0;
                if (GetController(SASList.Yaw).SetPoint - FlightData.heading >= -180 && GetController(SASList.Yaw).SetPoint - FlightData.heading <= 180)
                    yawResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading);
                else if (GetController(SASList.Yaw).SetPoint - FlightData.heading < -180)
                    yawResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading - 360);
                else if (GetController(SASList.Yaw).SetPoint - FlightData.heading > 180)
                    yawResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading + 360);

                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!IsPaused(SASList.Pitch)) 
                {
                    FlightData.thisVessel.ctrlState.pitch = (pitchResponse * (float)Math.Cos(rollRad) - yawResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                    if (activationFadePitch > 1)
                        activationFadePitch *= 0.98f; // ~100 physics frames
                    else
                        activationFadePitch = 1;
                }

                if (!IsPaused(SASList.Yaw)) 
                {
                    FlightData.thisVessel.ctrlState.yaw = (pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                    if (activationFadeYaw > 1)
                        activationFadeYaw *= 0.98f; // ~100 physics frames
                    else
                        activationFadeYaw = 1;
                }

                if (!IsPaused(SASList.Roll)) 
                {
                    if (GetController(SASList.Roll).SetPoint - FlightData.roll >= -180 && GetController(SASList.Roll).SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll + 360) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll - 360) / activationFadeRoll;

                    if (activationFadeRoll > 1)
                        activationFadeRoll *= 0.98f; // ~100 physics frames
                    else
                        activationFadeRoll = 1;
                }
            }
        }

        public static PID_Controller GetController(SASList id)
        {
            return controllers[(int)id];
        }

        public static void ToggleSSASMode()
        {
            ssasMode = !ssasMode;
            if (ssasMode)
            {
                // If SAS is active, make SSAS active
                isSSASActive = FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS];
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]
                    = false;
                if (isSSASActive)
                    updateTarget();
            }
            else
            {
                // If SSAS is active, make SAS active
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]
                    = isSSASActive;
                isSSASActive = false;
            }
                
        }

        public static void ToggleActive()
        {
            if (ssasMode)
            {
                SetActive(!isSSASActive);
            }
            else
            {
                SetActive(!FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]);
            }
        }

        public static void SetActive(bool active)
        {
            if (ssasMode)
            {
                // If only just switched on, update target
                if (!isSSASActive && active)
                    updateTarget();
                isSSASActive = active;
            }
            else
            {
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]
                    = active;
            }
        }

        public static bool IsSSASMode() { return ssasMode; }

        private static bool IsPaused(SASList id)
        {
            return isPaused[(int)id];
        }

        private static void SetPaused(SASList id, bool val)
        {
            isPaused[(int)id] = val;
        }

        public static void UpdatePreset()
        {
            SASPreset p = PresetManager.GetActiveSASPreset();
            if (p != null)
                p.Update(controllers);
            PresetManager.SavePresetsToFile();
        }
        
        public static void RegisterNewPreset(string name)
        {
            PresetManager.RegisterSASPreset(controllers, name);
        }
        
        public static void LoadPreset(SASPreset p)
        {
            PresetManager.LoadSASPreset(controllers, p);
        }
        
        public static void UpdateStockPreset()
        {
            SASPreset p = PresetManager.GetActiveStockSASPreset();
            if (p != null)
                p.UpdateStock(Utility.FlightData.thisVessel.Autopilot.SAS);
            PresetManager.SavePresetsToFile();
        }
        
        public static void RegisterNewStockPreset(string name)
        {
            PresetManager.RegisterStockSASPreset(name);
        }
        
        public static void LoadStockPreset(SASPreset p)
        {
            PresetManager.LoadStockSASPreset(p);
        }

        public static void updateTarget()
        {
            GetController(SASList.Pitch).SetPoint = FlightData.pitch;
            GetController(SASList.Yaw).SetPoint = FlightData.heading;
            GetController(SASList.Roll).SetPoint = FlightData.roll;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;
        }

        private static void PauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown() || GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            {
                SetPaused(SASList.Pitch, true);
                SetPaused(SASList.Yaw, true);
            }
            if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp() || GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Pitch, false);
                SetPaused(SASList.Yaw, false);
                if (IsSSASOperational())
                {
                    GetController(SASList.Pitch).SetPoint = FlightData.pitch;
                    GetController(SASList.Yaw).SetPoint = FlightData.heading;
                }

                activationFadePitch = 10;
                activationFadeYaw = 10;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                SetPaused(SASList.Roll, true);
            if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Roll, false);
                if (IsSSASOperational())
                    GetController(SASList.Roll).SetPoint = FlightData.roll;

                activationFadeRoll = 10;
            }
        }


        public static bool IsSSASOperational()
        {
            // ssasHoldKey toggles the main state, i.e. active --> off, off --> active
            return (isSSASActive != ssasHoldKey) && ssasMode;
        }

        public static bool IsStockSASOperational()
        {
            return FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS];
        }
    }
}
