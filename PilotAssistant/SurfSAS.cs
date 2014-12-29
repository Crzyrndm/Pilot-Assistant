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
        private static FlightData flightData;
        private static PID_Controller[] controllers = new PID_Controller[3]; 

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

        private static bool pauseMgrUserPitch = false;
        private static bool pauseMgrUserRoll = false;
        private static bool pauseMgrUserYaw = false;

        // rollState: false = surface mode, true = vector mode
        private static bool rollState = false; 
        private static Vector3d rollTarget = Vector3d.zero;

        public void Start()
        {
            flightData = new FlightData(FlightGlobals.ActiveVessel);

            // grab stock PID values
            PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
            PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
            PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
            controllers[(int)SASList.Pitch] = pitch;
            controllers[(int)SASList.Roll] = roll;
            controllers[(int)SASList.Yaw] = yaw;

            // Set up a default preset that can be easily returned to
            PresetManager.InitDefaultSASTuning(controllers);
            PresetManager.InitDefaultStockSASTuning(flightData.Vessel.Autopilot.SAS);
            
            isPaused[0] = isPaused[1] = isPaused[2] = false;
            
            // register vessel
            flightData.Vessel.OnAutopilotUpdate += new FlightInputCallback(VesselController);
            GameEvents.onVesselChange.Add(VesselSwitch);
            
            // Init UI
            GeneralUI.InitColors();
            
            RenderingManager.AddToPostDrawQueue(5, DrawGUI);
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
            ssasMode = false;
            ssasToggleKey = false;
            ssasHoldKey = false;

            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = null;

            flightData.Vessel.OnAutopilotUpdate -= new FlightInputCallback(VesselController);
        }

        public void Update()
        {
            if (ssasMode)
                flightData.Vessel.ActionGroups[KSPActionGroup.SAS] = false;

            if (ssasMode && flightData.Vessel.staticPressure == 0)
            {
                // Try to seamlessly switch to stock SAS
                ToggleSSASMode();
            }

            keyPressChanges();
        }

        private static void keyPressChanges()
        {
            // Respect current input locks
            if (InputLockManager.IsLocked(ControlTypes.ALL_SHIP_CONTROLS))
                return;

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
        }

        public void DrawGUI()
        {
            if (IsSSASMode())
            {
                if (IsSSASOperational())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;
                
                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    ToggleOperational();
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }
            SASMainWindow.Draw(AppLauncher.AppLauncherInstance.bDisplaySAS);
        }

        private static void VesselController(FlightCtrlState state)
        {
            flightData.UpdateAttitude();

            if (!IsSSASOperational())
                return;
            
            PauseManager(state); // manage activation of SAS axes depending on user input
            
            float vertResponse = 0;
            if (IsSSASAxisEnabled(SASList.Pitch))
                vertResponse = -1 * (float)GetController(SASList.Pitch).Response(flightData.Pitch);
            
            float hrztResponse = 0;
            if (IsSSASAxisEnabled(SASList.Yaw))
            {
                if (GetController(SASList.Yaw).SetPoint - flightData.Heading >= -180 && GetController(SASList.Yaw).SetPoint - flightData.Heading <= 180)
                    hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(flightData.Heading);
                else if (GetController(SASList.Yaw).SetPoint - flightData.Heading < -180)
                    hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(flightData.Heading - 360);
                else if (GetController(SASList.Yaw).SetPoint - flightData.Heading > 180)
                    hrztResponse = -1 * (float)GetController(SASList.Yaw).Response(flightData.Heading + 360);
            }
            
            double rollRad = Math.PI / 180 * flightData.Roll;
            
            if ((!IsPaused(SASList.Pitch) && IsSSASAxisEnabled(SASList.Pitch)) ||
                (!IsPaused(SASList.Yaw) && IsSSASAxisEnabled(SASList.Yaw)))
            {
                state.pitch = (vertResponse * (float)Math.Cos(rollRad) - hrztResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                if (activationFadePitch > 1)
                    activationFadePitch *= 0.98f; // ~100 physics frames
                else
                    activationFadePitch = 1;
                
                state.yaw = (vertResponse * (float)Math.Sin(rollRad) + hrztResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                if (activationFadeYaw > 1)
                    activationFadeYaw *= 0.98f; // ~100 physics frames
                else
                    activationFadeYaw = 1;
            }
            
            RollResponse(state);
        }

        private static void RollResponse(FlightCtrlState state)
        {
            if (!IsPaused(SASList.Roll) && IsSSASAxisEnabled(SASList.Roll))
            {
                bool rollStateWas = rollState;
                // switch tracking modes
                if (rollState) // currently in vector mode
                {
                    if (flightData.Pitch < 25 && flightData.Pitch > -25)
                        rollState = false; // fall back to surface mode
                }
                else // surface mode
                {
                    if (flightData.Pitch > 30 || flightData.Pitch < -30)
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
                        GetController(SASList.Roll).SkipDerivative = true;
                        rollTarget = flightData.Vessel.ReferenceTransform.right;
                    }

                    Vector3 proj = flightData.Vessel.ReferenceTransform.up * Vector3.Dot(flightData.Vessel.ReferenceTransform.up, rollTarget)
                        + flightData.Vessel.ReferenceTransform.right * Vector3.Dot(flightData.Vessel.ReferenceTransform.right, rollTarget);
                    double roll = Vector3.Angle(proj, rollTarget) * Math.Sign(Vector3.Dot(flightData.Vessel.ReferenceTransform.forward, rollTarget));

                    state.roll = (float)GetController(SASList.Roll).Response(roll) / activationFadeRoll;
                }
                else
                {
                    if (rollStateWas)
                    {
                        GetController(SASList.Roll).SetPoint = flightData.Roll;
                        GetController(SASList.Roll).SkipDerivative = true;
                    }

                    if (GetController(SASList.Roll).SetPoint - flightData.Roll >= -180 && GetController(SASList.Roll).SetPoint - flightData.Roll <= 180)
                        state.roll = (float)GetController(SASList.Roll).Response(flightData.Roll) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - flightData.Roll > 180)
                        state.roll = (float)GetController(SASList.Roll).Response(flightData.Roll + 360) / activationFadeRoll;
                    else if (GetController(SASList.Roll).SetPoint - flightData.Roll < -180)
                        state.roll = (float)GetController(SASList.Roll).Response(flightData.Roll - 360) / activationFadeRoll;
                }

                if (activationFadeRoll > 1)
                    activationFadeRoll *= 0.98f; // ~100 physics frames
                else
                    activationFadeRoll = 1;
            }
        }

        public static FlightData GetFlightData() { return flightData; }

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
                flightData.Vessel.ActionGroups[KSPActionGroup.SAS]
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
                p.UpdateStock(flightData.Vessel.Autopilot.SAS);
            PresetManager.SavePresetsToFile();
        }
        
        public static void RegisterNewStockPreset(string name)
        {
            PresetManager.RegisterStockSASPreset(flightData.Vessel.Autopilot.SAS, name);
        }
        
        public static void LoadStockPreset(SASPreset p)
        {
            PresetManager.LoadStockSASPreset(flightData.Vessel.Autopilot.SAS, p);
        }

        public static void updateTarget()
        {
            if (rollState)
                GetController(SASList.Roll).SetPoint = 0;
            else
                GetController(SASList.Roll).SetPoint = flightData.Roll;
            
            GetController(SASList.Pitch).SetPoint = flightData.Pitch;
            GetController(SASList.Yaw).SetPoint = flightData.Heading;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;

            rollTarget = flightData.Vessel.ReferenceTransform.right;
        }

        private static void PauseManager(FlightCtrlState state)
        {
            if (state.pitch != 0.0 || state.yaw != 0.0)
            {
                SetPaused(SASList.Pitch, true);
                SetPaused(SASList.Yaw, true);
                pauseMgrUserPitch = pauseMgrUserPitch || (state.pitch != 0.0);
                pauseMgrUserYaw = pauseMgrUserYaw || (state.yaw != 0.0);
            }
            else if (pauseMgrUserPitch || pauseMgrUserYaw)
            {
                SetPaused(SASList.Pitch, false);
                SetPaused(SASList.Yaw, false);
                if (IsSSASAxisEnabled(SASList.Pitch) || IsSSASAxisEnabled(SASList.Yaw))
                {
                    GetController(SASList.Pitch).SetPoint = flightData.Pitch;
                    GetController(SASList.Yaw).SetPoint = flightData.Heading;
                    if (pauseMgrUserPitch && IsSSASAxisEnabled(SASList.Pitch))
                        activationFadePitch = 10;
                    if (pauseMgrUserYaw && IsSSASAxisEnabled(SASList.Yaw))
                        activationFadeYaw = 10;
                }
                pauseMgrUserPitch = false;
                pauseMgrUserYaw = false;
            }

            if (state.roll != 0.0)
            {
                SetPaused(SASList.Roll, true);
                pauseMgrUserRoll = true;
            }
            else if (pauseMgrUserRoll)
            {
                SetPaused(SASList.Roll, false);
                if (IsSSASAxisEnabled(SASList.Roll))
                {
                    if (rollState)
                        rollTarget = flightData.Vessel.ReferenceTransform.right;
                    else
                        GetController(SASList.Roll).SetPoint = flightData.Roll;
                    activationFadeRoll = 10;
                }
                pauseMgrUserRoll = false;
            }
        }

        public static bool IsSSASOperational()
        {
            // ssasHoldKey toggles the main state, i.e. active --> off, off --> active
            return (ssasToggleKey != ssasHoldKey) && ssasMode;
        }

        public static bool IsStockSASOperational()
        {
            return flightData.Vessel.ActionGroups[KSPActionGroup.SAS];
        }
    }
}
