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
        Hdg,
        Roll
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class SurfSAS : MonoBehaviour
    {
        private static List<PID_Controller> controllers = new List<PID_Controller>();

        private static bool initialized = false;
        private static bool isArmed = false;
        private static bool isActive = false;
        private static bool[] isPaused = new bool[3]; // pause on a per axis basis
        private static bool inAtmosphere = false;
        private static bool stockSASEnabled = false;

        private static float activationFadeRoll = 1;
        private static float activationFadePitch = 1;
        private static float activationFadeYaw = 1;

        public void Initialize()
        {
            // register vessel if not already
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // grab stock PID values
            if (FlightData.thisVessel.VesselSAS.pidLockedPitch != null)
            {
                PresetManager.InitDefaultStockSASTuning();

                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                controllers.Add(pitch);
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                controllers.Add(yaw);
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                controllers.Add(roll);

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
            isArmed = false;
            ActivitySwitch(false);

            controllers.Clear();

            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
        }

        public void Update()
        {
            if (!initialized)
                Initialize();

            // SAS activated by user
            if (isArmed && !ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                ActivitySwitch(true);
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                updateTarget();
            }
            else if (ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                ActivitySwitch(false);
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }

            // Atmospheric mode tracks horizon, don't want in space
            if (FlightData.thisVessel.staticPressure > 0 && !inAtmosphere)
            {
                inAtmosphere = true;
                if (FlightData.thisVessel.ctrlState.killRot && isArmed)
                {
                    ActivitySwitch(true);
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else if (FlightData.thisVessel.staticPressure == 0 && inAtmosphere)
            {
                inAtmosphere = false;
                if (ActivityCheck())
                {
                    ActivitySwitch(false);
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                }
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
            if (isArmed)
            {
                FlightData.updateAttitude();

                float pitchResponse = -1 * (float)GetController(SASList.Pitch).Response(FlightData.pitch);

                float yawResponse = 0;
                if (GetController(SASList.Hdg).SetPoint - FlightData.heading >= -180 && GetController(SASList.Hdg).SetPoint - FlightData.heading <= 180)
                    yawResponse = -1 * (float)GetController(SASList.Hdg).Response(FlightData.heading);
                else if (GetController(SASList.Hdg).SetPoint - FlightData.heading < -180)
                    yawResponse = -1 * (float)GetController(SASList.Hdg).Response(FlightData.heading - 360);
                else if (GetController(SASList.Hdg).SetPoint - FlightData.heading > 180)
                    yawResponse = -1 * (float)GetController(SASList.Hdg).Response(FlightData.heading + 360);

                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!IsPaused(SASList.Pitch) && ActivityCheck()) // && IsActive(SASList.Pitch))
                {
                    FlightData.thisVessel.ctrlState.pitch = (pitchResponse * (float)Math.Cos(rollRad) - yawResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                    if (activationFadePitch > 1)
                        activationFadePitch *= 0.98f; // ~100 physics frames
                    else
                        activationFadePitch = 1;
                }

                if (!IsPaused(SASList.Hdg) && ActivityCheck()) // && IsActive(SASList.Hdg))
                {
                    FlightData.thisVessel.ctrlState.yaw = (pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                    if (activationFadeYaw > 1)
                        activationFadeYaw *= 0.98f; // ~100 physics frames
                    else
                        activationFadeYaw = 1;
                }

                if (!IsPaused(SASList.Roll) && ActivityCheck()) // && IsActive(SASList.Roll))
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

        public static void ToggleStockSAS()
        {
            stockSASEnabled = !stockSASEnabled;
            // This was binned in 5cd3773
            /*if (stockSASEnabled)
            {
                if (PresetManager.activeStockSASPreset == null)
                {
                    PresetManager.loadStockSASPreset(PresetManager.defaultStockSASTuning);
                    PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
                }
                else
                    PresetManager.loadStockSASPreset(PresetManager.activeStockSASPreset);
            }
            else
            {
                if (PresetManager.activeSASPreset == null)
                {
                    PresetManager.loadSASPreset(PresetManager.defaultSASTuning);
                    PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
                }
                else
                    PresetManager.loadSASPreset(PresetManager.activeSASPreset);
            }*/
        }

        public static void ToggleArmed()
        {
            isArmed = !isArmed;
            if (!isArmed)
                SurfSAS.ActivitySwitch(false);
        }

        public static bool StockSASEnabled() { return stockSASEnabled; }

        public static bool IsArmed() { return isArmed; }

        public static bool IsPaused(SASList id)
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
                p.UpdateStock(Utility.FlightData.thisVessel.VesselSAS);
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
            GetController(SASList.Hdg).SetPoint = FlightData.heading;
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
                SetPaused(SASList.Hdg, true);
            }
            if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp() || GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Pitch, false);
                SetPaused(SASList.Hdg, false);
                if (ActivityCheck()) // IsActive(SASList.Pitch))
                {
                    GetController(SASList.Pitch).SetPoint = FlightData.pitch;
                    GetController(SASList.Hdg).SetPoint = FlightData.heading;
                }

                activationFadePitch = 10;
                activationFadeYaw = 10;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                SetPaused(SASList.Roll, true);
            if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                SetPaused(SASList.Roll, false);
                if (ActivityCheck()) // IsActive(SASList.Roll))
                    GetController(SASList.Roll).SetPoint = FlightData.roll;

                activationFadeRoll = 10;
            }

            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                ActivitySwitch(true);
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                ActivitySwitch(false);
                updateTarget();
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
        }

        public static void ActivitySwitch(bool enable)
        {
            isActive = enable;
        }

        public static bool ActivityCheck()
        {
            return isActive;
        }
    }
}
