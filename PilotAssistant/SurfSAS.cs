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
        // Used to monitor the use of SAS_TOGGLE key for SSAS.
        private static bool ssasToggleKey = false;
        // Used to monitor the use of SAS_HOLD key for SSAS.
        private static bool ssasHoldKey = false;
        // Used to selectively enable SSAS on a per axis basis
        private static bool[] isSSASAxisEnabled = { true, true, true };
        // Used to monitor user input, and pause SSAS on a per axis basis
        private static bool[] isPaused = { false, false, false };

        private static float activationFadeRoll = 1;
        private static float activationFadePitch = 1;
        private static float activationFadeYaw = 1;

        // rollState: false = surface mode, true = vector mode
        private static bool rollState = false; 
        private static Vector3d rollTarget = Vector3d.zero;

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

                GeneralUI.InitColors();
                RenderingManager.AddToPostDrawQueue(5, GUI);
                FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(DoSSAS);
            }
        }

        public void OnDestroy()
        {
            initialized = false;
            ssasMode = false;
            ssasToggleKey = false;
            ssasHoldKey = false;

            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = null;

            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(DoSSAS);
        }

        public void Update()
        {
            if (!initialized)
                Initialize();

            if (ssasMode)
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] = false;
            
            if (ssasMode && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                ssasToggleKey = !ssasToggleKey;
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
                // Try to seamlessly switch to stock SAS
                ToggleSSASMode();
            }

            PauseManager(); // manage activation of SAS axes depending on user input
        }

        public void GUI()
        {
            SASMainWindow.Draw(AppLauncher.AppLauncherInstance.bDisplaySAS);
        }

        public void DoSSAS(FlightCtrlState state)
        {
            if (IsSSASOperational())
            {
                FlightData.updateAttitude();

                float vertResponse = 0;
                if (IsSSASAxisEnabled(SASList.Pitch))
                    vertResponse = -1 * (float)GetController(SASList.Pitch).Response(FlightData.pitch);

                float hrztResponse = 0;
                if (IsSSASAxisEnabled(SASList.Yaw))
                {
                    if (GetController(SASList.Yaw).SetPoint - FlightData.heading >= -180 && GetController(SASList.Yaw).SetPoint - FlightData.heading <= 180)
                        hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading);
                    else if (GetController(SASList.Yaw).SetPoint - FlightData.heading < -180)
                        hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading - 360);
                    else if (GetController(SASList.Yaw).SetPoint - FlightData.heading > 180)
                        hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(FlightData.heading + 360);
                }

                double rollRad = Math.PI / 180 * FlightData.roll;

                if ((!IsPaused(SASList.Pitch) && IsSSASAxisEnabled(SASList.Pitch)) ||
                    (!IsPaused(SASList.Yaw) && IsSSASAxisEnabled(SASList.Yaw)))
                {
                    FlightData.thisVessel.ctrlState.pitch = (vertResponse * (float)Math.Cos(rollRad) - hrztResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                    if (activationFadePitch > 1)
                        activationFadePitch *= 0.98f; // ~100 physics frames
                    else
                        activationFadePitch = 1;
                
                    FlightData.thisVessel.ctrlState.yaw = (vertResponse * (float)Math.Sin(rollRad) + hrztResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                    if (activationFadeYaw > 1)
                        activationFadeYaw *= 0.98f; // ~100 physics frames
                    else
                        activationFadeYaw = 1;
                }

                rollResponse();

            }
        }

        private void rollResponse()
        {
            if (!IsPaused(SASList.Roll) && IsSSASAxisEnabled(SASList.Roll))
            {
                bool rollStateWas = rollState;
                // switch tracking modes
                if (rollState) // currently in vector mode
                {
                    if (FlightData.pitch < 25 && FlightData.pitch > -25)
                        rollState = false; // fall back to surface mode
                }
                else // surface mode
                {
                    if (FlightData.pitch > 30 || FlightData.pitch < -30)
                        rollState = true; // go to vector mode
                }

                // Above 30 degrees pitch, rollTarget should always lie on the horizontal plane of the vessel
                // Below 25 degrees pitch, use the surf roll logic
                // hysteresis on the switch ensures it doesn't bounce back and forth and lose the lock
                if (rollState)
                {
                    if (!rollStateWas)
                    {
                        GetController(SASList.Roll).SetPoint = 0;
                        GetController(SASList.Roll).skipDerivative = true;
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    }

                    Vector3 proj = FlightData.thisVessel.ReferenceTransform.up * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, rollTarget)
                        + FlightData.thisVessel.ReferenceTransform.right * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.right, rollTarget);
                    double roll = Vector3.Angle(proj, rollTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, rollTarget));

                    FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(roll) / activationFadeRoll;
                }
                else
                {
                    if (rollStateWas)
                    {
                        GetController(SASList.Roll).SetPoint = FlightData.roll;
                        GetController(SASList.Roll).skipDerivative = true;
                    }

                    if (GetController(SASList.Roll).SetPoint - FlightData.roll >= -180 && GetController(SASList.Roll).SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll + 360) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)GetController(SASList.Roll).Response(FlightData.roll - 360) / activationFadeRoll;
                }

                if (activationFadeRoll > 1)
                    activationFadeRoll *= 0.98f; // ~100 physics frames
                else
                    activationFadeRoll = 1;
            }
        }

        public static PID_Controller GetController(SASList id)
        {
            return controllers[(int)id];
        }

        public static void ToggleSSASMode()
        {
            // Swap modes, ensure operational state doesn't change.
            bool wasOperational = IsSSASOperational() || IsStockSASOperational();
            ssasMode = !ssasMode;
            SetOperational(wasOperational);                
        }

        public static void ToggleOperational()
        {
            if (ssasMode)
                SetOperational(!IsSSASOperational());
            else
                SetOperational(!IsStockSASOperational());
        }

        public static void SetOperational(bool operational)
        {
            if (ssasMode)
            {
                bool wasOperational = IsSSASOperational();
                // Behave the same a stock SAS
                if (wasOperational != ssasToggleKey && wasOperational != operational)
                    ssasToggleKey = !operational;
                else if (wasOperational != operational)
                    ssasToggleKey = operational;
                // If only just switched on, update target
                if (IsSSASOperational() && !wasOperational)
                    updateTarget();
                
            }
            else
            {
                FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]
                    = operational;
            }
        }

        public static bool IsSSASAxisEnabled(SASList id)
        {
            return isSSASAxisEnabled[(int)id];
        }

        public static void SetSSASAxisEnabled(SASList id, bool enabled)
        {
            isSSASAxisEnabled[(int)id] = enabled;
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
            if (rollState)
                GetController(SASList.Roll).SetPoint = 0;
            else
                GetController(SASList.Roll).SetPoint = FlightData.roll;
            
            GetController(SASList.Pitch).SetPoint = FlightData.pitch;
            GetController(SASList.Yaw).SetPoint = FlightData.heading;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;

            rollTarget = FlightData.thisVessel.ReferenceTransform.right;
        }

        private static void PauseManager()
        {
            // ========================================
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown())
            {
                SetPaused(SASList.Pitch, true);
                SetPaused(SASList.Yaw, true);
            }
            else if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp())
            {
                SetPaused(SASList.Pitch, false);
                SetPaused(SASList.Yaw, false);
                if (IsSSASOperational() && IsSSASAxisEnabled(SASList.Pitch))
                {
                    GetController(SASList.Pitch).SetPoint = FlightData.pitch;
                    GetController(SASList.Yaw).SetPoint = FlightData.heading;
                    activationFadePitch = 10;
                }
            }

            // ========================================
            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                SetPaused(SASList.Roll, true);
            else if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Roll, false);
                if (IsSSASOperational() && IsSSASAxisEnabled(SASList.Roll))
                {
                    if (rollState)
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    else
                        GetController(SASList.Roll).SetPoint = FlightData.roll;
                    activationFadeRoll = 10;
                }
            }

            // ========================================
            if (GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            {
                SetPaused(SASList.Pitch, true);
                SetPaused(SASList.Yaw, true);
            }
            else if (GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Pitch, false);
                SetPaused(SASList.Yaw, false);
                if (IsSSASOperational() && IsSSASAxisEnabled(SASList.Yaw))
                {
                    GetController(SASList.Pitch).SetPoint = FlightData.pitch;
                    GetController(SASList.Yaw).SetPoint = FlightData.heading;
                    activationFadeYaw = 10;
                }
            }
        }


        public static bool IsSSASOperational()
        {
            // ssasHoldKey toggles the main state, i.e. active --> off, off --> active
            return (ssasToggleKey != ssasHoldKey) && ssasMode;
        }

        public static bool IsStockSASOperational()
        {
            return FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS];
        }
    }
}
