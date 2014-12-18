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
        internal static List<PID_Controller> SASControllers = new List<PID_Controller>();

        internal static bool bInit = false;
        internal static bool bArmed = false;
        internal static bool[] bActive = new bool[3]; // activate on per axis basis
        internal bool[] bPause = new bool[3]; // pause on a per axis basis
        internal bool bAtmosphere = false;
        internal static bool bStockSAS = false;

        internal static float activationFadeRoll = 1;
        internal static float activationFadePitch = 1;
        internal static float activationFadeYaw = 1;

        internal static bool rollState = false; // false = surface mode, true = vector mode

        public void Initialise()
        {
            // register vessel if not already
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // grab stock PID values - needs to be done in update so that it is initialised
            if (FlightData.thisVessel.Autopilot.SAS.pidLockedPitch != null)
            {
                PresetManager.defaultStockSASTuning = new PresetSAS(FlightData.thisVessel.Autopilot.SAS, "Stock");
                if (PresetManager.activeStockSASPreset == null)
                    PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
                else
                {
                    PresetManager.loadStockSASPreset(PresetManager.activeStockSASPreset);
                    Messaging.statusMessage(7);
                }

                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(pitch);
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(yaw);
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(roll);
                PresetManager.defaultSASTuning = new PresetSAS(SASControllers, "Default");

                if (PresetManager.activeSASPreset == null)
                    PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
                else
                {
                    PresetManager.loadSASPreset(PresetManager.activeSASPreset);
                    Messaging.statusMessage(6);
                }

                bInit = true;
                bPause[0] = bPause[1] = bPause[2] = false;
                ActivitySwitch(false);

                GeneralUI.InitColors();

                RenderingManager.AddToPostDrawQueue(5, GUI);
            }
        }

        public void OnDestroy()
        {
            bInit = false;
            bArmed = false;
            ActivitySwitch(false);

            SASControllers.Clear();

            RenderingManager.RemoveFromPostDrawQueue(5, GUI);
        }

        public void Update()
        {
            if (!bInit)
                Initialise();

            bool mod = GameSettings.MODIFIER_KEY.GetKey();
            // Arm Hotkey
            if (mod && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bArmed = !bArmed;
                if (ActivityCheck())
                {
                    ActivitySwitch(false);
                    setStockSAS(false);
                }
            }

            // SAS activated by user
            if (bArmed && !ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown() && !mod)
            {
                if (!bStockSAS)
                {
                    ActivitySwitch(true);
                    setStockSAS(false);
                    updateTarget();
                }
            }
            else if (ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown() && !mod)
            {
                ActivitySwitch(false);
                setStockSAS(bStockSAS);
            }

            // Atmospheric mode tracks horizon, don't want in space
            if (FlightData.thisVessel.staticPressure > 0 && !bAtmosphere)
            {
                bAtmosphere = true;
                if (FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] && bArmed)
                {
                    ActivitySwitch(true);
                    setStockSAS(false);
                }
            }
            else if (FlightData.thisVessel.staticPressure == 0 && bAtmosphere)
            {
                bAtmosphere = false;
                if (ActivityCheck())
                {
                    ActivitySwitch(false);
                    setStockSAS(true);
                }
            }

            pauseManager(); // manage activation of SAS axes depending on user input
        }

        public void GUI()
        {
            if (GeneralUI.UISkin == null)
                GeneralUI.UISkin = UnityEngine.GUI.skin;

            UnityEngine.GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            // SAS toggle button
            if (SurfSAS.bArmed)
            {
                if (SurfSAS.ActivityCheck())
                    UnityEngine.GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    UnityEngine.GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (UnityEngine.GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    ActivitySwitch(!ActivityCheck());
                    updateTarget();
                    if (ActivityCheck())
                        setStockSAS(false);
                }
                UnityEngine.GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }
            
            // Main and preset window stuff
            if (!AppLauncher.AppLauncherInstance.bDisplaySAS)
                return;
            SASMainWindow.Draw();
        }

        public void FixedUpdate()
        {
            if (bArmed)
            {
                FlightData.updateAttitude();

                pitchResponse();
                rollResponse();
                //yawResponse();
            }
        }

        internal static void updateTarget()
        {
            if (rollState)
                SASControllers[(int)SASList.Roll].SetPoint = 0;
            else
                SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;

            rollTarget = FlightData.thisVessel.ReferenceTransform.right;
            pitchTarget = FlightData.thisVessel.ReferenceTransform.forward;
            yawTarget = FlightData.thisVessel.ReferenceTransform.up;
        }

        private void pauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown())
                bPause[(int)SASList.Pitch] = true;
            else if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                {
                    pitchTarget = FlightData.thisVessel.ReferenceTransform.forward;
                    activationFadePitch = 10;
                }
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                bPause[(int)SASList.Roll] = true;
            else if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Roll] = false;
                if (bActive[(int)SASList.Roll])
                {
                    if (rollState)
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    else
                        SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
                    activationFadeRoll = 10;
                }
            }

            if (GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
                bPause[(int)SASList.Yaw] = true;
            else if (GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Yaw] = false;
                if (bActive[(int)SASList.Yaw])
                {
                    yawTarget = FlightData.thisVessel.ReferenceTransform.up;
                    activationFadeYaw = 10;
                }
            }
            // buggy-----------------------------------------------------------------------------------------------------------------------------------
            if (GameSettings.SAS_HOLD.GetKeyDown() && !bStockSAS)
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Yaw] = true;
                setStockSAS(false);
            }
            else if (GameSettings.SAS_HOLD.GetKeyUp() && !bStockSAS)
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Yaw] = false;
                setStockSAS(false);
                updateTarget();
            }
        }

        internal static void ActivitySwitch(bool enable)
        {
            if (enable)
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Roll] = bActive[(int)SASList.Yaw] = true;
            else
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Roll] = bActive[(int)SASList.Yaw] = false;
        }

        internal static bool ActivityCheck()
        {
            if (bActive[(int)SASList.Pitch] || bActive[(int)SASList.Roll] || bActive[(int)SASList.Yaw])
                return true;
            else
                return false;
        }

        internal static void setStockSAS(bool state)
        {
            FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, state);
            FlightData.thisVessel.ctrlState.killRot = state; // incase anyone checks the ctrl state (should be using checking vessel.ActionGroup[KSPActionGroup.SAS])
        }


        static Vector3d rollTarget = Vector3d.zero;
        private void rollResponse()
        {
            if (!bPause[(int)SASList.Roll] && bActive[(int)SASList.Roll])
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
                        SASControllers[(int)SASList.Roll].SetPoint = 0;
                        SASControllers[(int)SASList.Roll].skipDerivative = true;
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    }

                    Vector3 proj = FlightData.thisVessel.ReferenceTransform.up * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, rollTarget)
                        + FlightData.thisVessel.ReferenceTransform.right * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.right, rollTarget);
                    double roll = Vector3.Angle(proj, rollTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, rollTarget));

                    FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(roll) / activationFadeRoll;
                }
                else
                {
                    if (rollStateWas)
                    {
                        SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
                        SASControllers[(int)SASList.Roll].skipDerivative = true;
                    }

                    if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll >= -180 && SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll) / activationFadeRoll;
                    else if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll + 360) / activationFadeRoll;
                    else if (SASControllers[(int)SASList.Roll].SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll - 360) / activationFadeRoll;
                }

                if (activationFadeRoll > 1)
                    activationFadeRoll *= 0.98f; // ~100 physics frames
                else
                    activationFadeRoll = 1;
            }
        }

        static Vector3d pitchTarget = Vector3d.zero; // pitch is worked on vessel forward(up) vector
        private void pitchResponse()
        {
            if (!bPause[(int)SASList.Pitch] && bActive[(int)SASList.Pitch])
            {
                Vector3 proj = FlightData.thisVessel.ReferenceTransform.forward * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, pitchTarget)
                        + FlightData.thisVessel.ReferenceTransform.right * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.right, pitchTarget);
                double pitch = Vector3.Angle(proj, pitchTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, pitchTarget));

                print("foo");
                print(Vector3.Angle(pitchTarget, FlightData.surfVesForward));
                print(pitch);

                FlightData.thisVessel.ctrlState.pitch = (float)SASControllers[(int)SASList.Pitch].Response(pitch) / activationFadePitch;

                if (activationFadePitch > 1)
                    activationFadePitch *= 0.98f; // ~100 physics frames
                else
                    activationFadePitch = 1;
            }
        }

        static Vector3d yawTarget = Vector3d.zero; // yaw is worked on vessel up(forward) vector
        private void yawResponse()
        {
            if (!bPause[(int)SASList.Yaw] && bActive[(int)SASList.Yaw])
            {
                Vector3 proj = FlightData.thisVessel.ReferenceTransform.forward * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, yawTarget)
                        + FlightData.thisVessel.ReferenceTransform.up * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, yawTarget);
                double yaw = Vector3.Angle(proj, yawTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, yawTarget));

                FlightData.thisVessel.ctrlState.yaw = (float)SASControllers[(int)SASList.Yaw].Response(yaw) / activationFadeYaw;

                if (activationFadeYaw > 1)
                    activationFadeYaw *= 0.98f; // ~100 physics frames
                else
                    activationFadeYaw = 1;
            }
        }
    }
}