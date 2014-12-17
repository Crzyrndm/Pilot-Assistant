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

        internal bool rollState = false; // false = surface mode, true = vector mode

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
                    if (!bStockSAS)
                        setStockSAS(false);
                    else
                        setStockSAS(true);
                }

            }

            // SAS activated by user
            if (bArmed && !ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                if (!bStockSAS)
                {
                    ActivitySwitch(true);
                    setStockSAS(false);
                    updateTarget();
                }
            }
            else if (ActivityCheck() && GameSettings.SAS_TOGGLE.GetKeyDown())
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

            GUI.skin = GeneralUI.UISkin;
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

                float pitchResponse = -1 * (float)SASControllers[(int)SASList.Pitch].Response(FlightData.pitch);

                float yawResponse = 0;
                if (SASControllers[(int)SASList.Yaw].SetPoint - FlightData.heading >= -180 && SASControllers[(int)SASList.Yaw].SetPoint - FlightData.heading <= 180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Yaw].Response(FlightData.heading);
                else if (SASControllers[(int)SASList.Yaw].SetPoint - FlightData.heading < -180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Yaw].Response(FlightData.heading - 360);
                else if (SASControllers[(int)SASList.Yaw].SetPoint - FlightData.heading > 180)
                    yawResponse = -1 * (float)SASControllers[(int)SASList.Yaw].Response(FlightData.heading + 360);

                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!bPause[(int)SASList.Pitch] && bActive[(int)SASList.Pitch])
                {
                    FlightData.thisVessel.ctrlState.pitch = (pitchResponse * (float)Math.Cos(rollRad) - yawResponse * (float)Math.Sin(rollRad)) / activationFadePitch;
                    if (activationFadePitch > 1)
                        activationFadePitch *= 0.98f; // ~100 physics frames
                    else
                        activationFadePitch = 1;
                }

                if (!bPause[(int)SASList.Yaw] && bActive[(int)SASList.Yaw])
                {
                    FlightData.thisVessel.ctrlState.yaw = (pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad)) / activationFadeYaw;
                    if (activationFadeYaw > 1)
                        activationFadeYaw *= 0.98f; // ~100 physics frames
                    else
                        activationFadeYaw = 1;
                }

                rollResponse();
            }
        }

        internal static void updateTarget()
        {
            SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
            SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;

            activationFadeRoll = 10;
            activationFadePitch = 10;
            activationFadeYaw = 10;

            updateVectorTarget();
        }

        internal static void updateVectorTarget()
        {
            rollTarget = FlightData.thisVessel.ReferenceTransform.right;
        }

        private void pauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown() || GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = true;
                bPause[(int)SASList.Yaw] = true;
            }
            else if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp() || GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = false;
                bPause[(int)SASList.Yaw] = false;
                if (bActive[(int)SASList.Pitch])
                {
                    SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
                    SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
                }

                activationFadePitch = 10;
                activationFadeYaw = 10;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                bPause[(int)SASList.Roll] = true;
            else if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Roll] = false;
                if (bActive[(int)SASList.Roll])
                {
                    SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
                    rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    activationFadeRoll = 10;
                }
            }

            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                ActivitySwitch(true);
                setStockSAS(false);
            }
            else if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                ActivitySwitch(false);
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
            FlightData.thisVessel.ctrlState.killRot = state; // incase anyone checks the ctrl state
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

                // Above 30 degrees, rollTarget should always lie on the horizontal plane of the vessel
                // Below 30 degrees, use the surf roll logic
                // hysteresis on the switch ensures it doesn't bounce back and forth and lose the lock
                if (rollState)
                {
                    if (!rollStateWas)
                    {
                        SASControllers[(int)SASList.Roll].SetPoint = 0;
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
                        SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;

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
    }
}