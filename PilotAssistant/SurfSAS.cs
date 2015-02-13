using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using Presets;

    [Flags]
    public enum SASList
    {
        Pitch,
        Roll,
        Yaw
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class SurfSAS : MonoBehaviour
    {
        private static SurfSAS instance;
        public static SurfSAS Instance
        {
            get { return instance; }
        }

        public static PID_Controller[] SASControllers = new PID_Controller[3]; // controller per axis

        static bool bInit = false; // if initialisation fails for any reason, things shouldn't run
        bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
        bool[] bActive = new bool[3]; // activate on per axis basis
        bool[] bPause = new bool[3]; // pause on a per axis basis
        public bool bStockSAS = true;

        string[] targets = { "0", "0", "0" };

        // unpause control authority scaling. Helps reduce the jump of SSAS gaining control of an axis
        public float[] fadeCurrent = { 1, 1, 1 }; // these are the current axis control factors
        public float[] timeElapsed = new float[3];
        float[] fadeSetpoint = { 10, 10, 10 }; // these are the values that get assigned every time control is unlocked
        const float fadeMult = 0.97f; // this is the decay rate. 0.97 < 1 after 0.75s starting from 10

        // unpause delay
        public float[] delayEngage = new float[3];

        bool rollState = false; // false = surface mode, true = vector mode

        Rect SASwindow = new Rect(10, 505, 200, 30); // gui window rect
        bool[] stockPIDDisplay = { true, false, false }; // which stock PID axes are visible

        string newPresetName = "";
        Rect SASPresetwindow = new Rect(550, 50, 50, 50);
        bool bShowPresets = false;

        // initialisation and default presets stuff
        public static double[] defaultPitchGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3 };
        public static double[] defaultRollGains = { 0.1, 0.0, 0.06, -1, 1, -1, 1, 3 };
        public static double[] defaultYawGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3 };

        public static double[] defaultPresetPitchGains = { 0.15, 0.0, 0.06, 3, 10 };
        public static double[] defaultPresetRollGains = { 0.1, 0.0, 0.06, 3, 10 };
        public static double[] defaultPresetYawGains = { 0.15, 0.0, 0.06, 3, 10 };

        public void Start()
        {
            instance = this;
            // Have to wait for stock SAS to be ready
            StartCoroutine(Initialise());
            // hide/show with KSP hide UI
            RenderingManager.AddToPostDrawQueue(5, drawGUI);

            // events and callbacks
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(SurfaceSAS);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpHandler);
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(SurfaceSAS);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(SurfaceSAS);

            StartCoroutine(Initialise());
        }

        private void warpHandler()
        {
            //if (!FlightGlobals.warpDriveActive)
            //    updateTarget();
        }

        // need to wait for Stock SAS to be ready, hence the Coroutine
        IEnumerator Initialise()
        {
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // wait for SAS to init
            if (FlightData.thisVessel.Autopilot.SAS.pidLockedPitch == null)
                yield return null;
            
            bPause.Initialize();
            ActivitySwitch(false);
            delayEngage[0] = delayEngage[1] = delayEngage[2] = 20; // delay engagement by 0.2s

            if (!bInit)
            {
                SASControllers[(int)SASList.Pitch] = new PID_Controller(defaultPitchGains);
                SASControllers[(int)SASList.Roll] = new PID_Controller(defaultRollGains);
                SASControllers[(int)SASList.Yaw] = new PID_Controller(defaultYawGains);

                if (!PresetManager.Instance.craftPresetList.ContainsKey("default"))
                    PresetManager.Instance.craftPresetList.Add("default", new CraftPreset("default", null, new SASPreset(SASControllers, "SSAS"), new SASPreset(FlightData.thisVessel.Autopilot.SAS, "stock"), bStockSAS));
                else
                {
                    if (PresetManager.Instance.craftPresetList["default"].SSASPreset == null)
                        PresetManager.Instance.craftPresetList["default"].SSASPreset = new SASPreset(SASControllers, "SSAS");
                    if (PresetManager.Instance.craftPresetList["default"].StockPreset == null)
                        PresetManager.Instance.craftPresetList["default"].StockPreset = new SASPreset(FlightData.thisVessel.Autopilot.SAS, "stock");
                }
                PresetManager.saveDefaults();

                GeneralUI.InitColors();
                bInit = true;
            }
            PresetManager.loadCraftSSASPreset();
            PresetManager.loadCraftStockPreset();
        }

        public void OnDestroy()
        {
            bInit = false;
            bArmed = false;
            ActivitySwitch(false);

            RenderingManager.RemoveFromPostDrawQueue(5, drawGUI);
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(SurfaceSAS);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);
        }

        public void Update()
        {
            bool mod = GameSettings.MODIFIER_KEY.GetKey();
            // Arm Hotkey
            if (mod && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bArmed = !bArmed;
                if (bArmed) // if we are armed, switch to SSAS
                    bStockSAS = false;
                
                if (ActivityCheck()) {
                    ActivitySwitch(false);
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

            // lets target slide while key is down, effectively temporary deactivation
            if (GameSettings.SAS_HOLD.GetKey())
                updateTarget();
        }

        public void drawGUI()
        {
            GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            // SAS toggle button
            // is before the bDisplay check so it can be up without the GUI
            if (bArmed)
            {
                if (SurfSAS.ActivityCheck())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    ActivitySwitch(!ActivityCheck());
                    updateTarget();
                    if (ActivityCheck())
                        setStockSAS(false);
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }
            
            // Main and preset window stuff
            if (!AppLauncherFlight.bDisplaySAS)
                return;
            Draw();
        }

        private void SurfaceSAS(FlightCtrlState state)
        {
            if (bArmed)
            {
                FlightData.updateAttitude();

                pauseManager(state);

                float vertResponse = 0;
                if (bActive[(int)SASList.Pitch])
                    vertResponse = -1 * (float)Utils.GetSAS(SASList.Pitch).ResponseD(FlightData.pitch);

                float hrztResponse = 0;
                if (bActive[(int)SASList.Yaw] && (FlightData.thisVessel.latitude < 88 && FlightData.thisVessel.latitude > -88))
                {
                    if (Utils.GetSAS(SASList.Yaw).SetPoint - FlightData.heading >= -180 && Utils.GetSAS(SASList.Yaw).SetPoint - FlightData.heading <= 180)
                        hrztResponse = -1 * (float)Utils.GetSAS(SASList.Yaw).ResponseD(FlightData.heading);
                    else if (Utils.GetSAS(SASList.Yaw).SetPoint - FlightData.heading < -180)
                        hrztResponse = -1 * (float)Utils.GetSAS(SASList.Yaw).ResponseD(FlightData.heading - 360);
                    else if (Utils.GetSAS(SASList.Yaw).SetPoint - FlightData.heading > 180)
                        hrztResponse = -1 * (float)Utils.GetSAS(SASList.Yaw).ResponseD(FlightData.heading + 360);
                }
                else
                {
                    Utils.GetSAS(SASList.Yaw).SetPoint = FlightData.heading;
                }

                double rollRad = Math.PI / 180 * FlightData.roll;

                if ((!bPause[(int)SASList.Pitch] || !bPause[(int)SASList.Yaw]) && (bActive[(int)SASList.Pitch] || bActive[(int)SASList.Yaw]))
                {
                    state.pitch = (vertResponse * (float)Math.Cos(rollRad) - hrztResponse * (float)Math.Sin(rollRad)) / fadeCurrent[(int)SASList.Pitch];
                    state.yaw = (vertResponse * (float)Math.Sin(rollRad) + hrztResponse * (float)Math.Cos(rollRad)) / fadeCurrent[(int)SASList.Yaw];
                }
                rollResponse();
            }
        }

        private void updateTarget()
        {
            if (rollState)
                Utils.GetSAS(SASList.Roll).SetPoint = 0;
            else
                Utils.GetSAS(SASList.Roll).SetPoint = FlightData.roll;

            Utils.GetSAS(SASList.Pitch).SetPoint = FlightData.pitch;
            Utils.GetSAS(SASList.Yaw).SetPoint = FlightData.heading;

            rollTarget = FlightData.thisVessel.ReferenceTransform.right;

            StartCoroutine(FadeInPitch());
            StartCoroutine(FadeInRoll());
            StartCoroutine(FadeInYaw());
        }

        private void pauseManager(FlightCtrlState state)
        {
            if (Utils.isFlightControlLocked())
                return;

            if (state.pitch != 0 && !bPause[(int)SASList.Pitch])
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Yaw] = true;
            else if (state.pitch == 0 && bPause[(int)SASList.Pitch])
            {
                if (state.yaw == 0)
                    bPause[(int)SASList.Pitch] = bPause[(int)SASList.Yaw] = false;

                if (bActive[(int)SASList.Pitch])
                        StartCoroutine(FadeInPitch());
            }
            
            if (state.roll != 0 && !bPause[(int)SASList.Roll])
                bPause[(int)SASList.Roll] = true;
            else if (state.roll == 0 && bPause[(int)SASList.Roll])
            {
                bPause[(int)SASList.Roll] = false;
                if (bActive[(int)SASList.Roll])
                        StartCoroutine(FadeInRoll());
            }

            if (state.yaw != 0 && !bPause[(int)SASList.Yaw])
                bPause[(int)SASList.Yaw] = bPause[(int)SASList.Pitch]= true;
            else if (state.yaw == 0 && bPause[(int)SASList.Yaw])
            {
                if (state.pitch == 0)
                    bPause[(int)SASList.Pitch] = bPause[(int)SASList.Yaw] = false;

                if (bActive[(int)SASList.Yaw])
                    StartCoroutine(FadeInYaw());
            }
        }

        bool pitchEnum = false;
        IEnumerator FadeInPitch()
        {
            // initialse all relevant values
            timeElapsed[(int)SASList.Pitch] = 0;
            fadeCurrent[(int)SASList.Pitch] = fadeSetpoint[(int)SASList.Pitch]; // x to the power of 0 is 1

            if (pitchEnum) // don't need multiple running at once
                yield break;
            pitchEnum = true;

            while (fadeCurrent[(int)SASList.Pitch] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Pitch] += Time.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Pitch] < delayEngage[(int)SASList.Pitch])
                {
                    Utils.GetSAS(SASList.Yaw).SetPoint = FlightData.heading;
                    targets[(int)SASList.Yaw] = FlightData.heading.ToString("N2");
                    Utils.GetSAS(SASList.Pitch).SetPoint = FlightData.pitch;
                    targets[(int)SASList.Pitch] = FlightData.pitch.ToString("N2");
                }
                else
                    fadeCurrent[(int)SASList.Pitch] = Mathf.Max(fadeSetpoint[(int)SASList.Pitch] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Pitch] - delayEngage[(int)SASList.Pitch]), 1);
            }

            // make sure we are actually set to 1
            fadeCurrent[(int)SASList.Pitch] = 1.0f;
            // clear the lock
            pitchEnum = false;
        }

        bool rollEnum = false;
        IEnumerator FadeInRoll()
        {
            // initialse all relevant values
            timeElapsed[(int)SASList.Roll] = 0;
            fadeCurrent[(int)SASList.Roll] = fadeSetpoint[(int)SASList.Roll]; // x to the power of 0 is 1

            if (rollEnum) // don't need multiple running at once
                yield break;
            rollEnum = true;

            while (fadeCurrent[(int)SASList.Roll] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Roll] += Time.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Roll] < delayEngage[(int)SASList.Roll])
                {
                    if (rollState)
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    else
                    {
                        Utils.GetSAS(SASList.Roll).SetPoint = FlightData.roll;
                        targets[(int)SASList.Roll] = FlightData.roll.ToString("N2");
                    }
                }
                else
                    fadeCurrent[(int)SASList.Roll] = Mathf.Max(fadeSetpoint[(int)SASList.Roll] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Roll]), 1);
            }

            // make sure we are actually at 1.0
            fadeCurrent[(int)SASList.Roll] = 1.0f;
            // clear the lock
            rollEnum = false;
        }

        bool yawEnum = false;
        IEnumerator FadeInYaw()
        {
            // initialse all relevant values
            timeElapsed[(int)SASList.Yaw] = 0;
            fadeCurrent[(int)SASList.Yaw] = fadeSetpoint[(int)SASList.Yaw]; // x to the power of 0 is 1

            if (yawEnum) // don't need multiple running at once
                yield break;
            yawEnum = true;

            while (fadeCurrent[(int)SASList.Yaw] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Yaw] += Time.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Yaw] < delayEngage[(int)SASList.Yaw])
                {
                    Utils.GetSAS(SASList.Yaw).SetPoint = FlightData.heading;
                    targets[(int)SASList.Yaw] = FlightData.heading.ToString("N2");
                    Utils.GetSAS(SASList.Pitch).SetPoint = FlightData.pitch;
                    targets[(int)SASList.Pitch] = FlightData.pitch.ToString("N2");
                }
                else
                    fadeCurrent[(int)SASList.Yaw] = Mathf.Max(fadeSetpoint[(int)SASList.Yaw] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Yaw] - delayEngage[(int)SASList.Yaw]), 1);
            }

            // make sure we are actually set to 1
            fadeCurrent[(int)SASList.Yaw] = 1.0f;
            // clear the lock
            pitchEnum = false;
        }

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        internal static void ActivitySwitch(bool enable)
        {
            if (enable)
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Roll] = instance.bActive[(int)SASList.Yaw] = true;
            else
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Roll] = instance.bActive[(int)SASList.Yaw] = false;
        }

        /// <summary>
        /// returns true if SSAS is active
        /// </summary>
        /// <returns></returns>
        internal static bool ActivityCheck()
        {
            if (instance.bActive[(int)SASList.Pitch] || instance.bActive[(int)SASList.Roll] || instance.bActive[(int)SASList.Yaw])
                return true;
            else
                return false;
        }

        /// <summary>
        /// set stock SAS state
        /// </summary>
        /// <param name="state"></param>
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
                    if (FlightData.pitch < 35 && FlightData.pitch > -35)
                        rollState = false; // fall back to surface mode
                }
                else // surface mode
                {
                    if (FlightData.pitch > 40 || FlightData.pitch < -40)
                        rollState = true; // go to vector mode
                }

                // Above 40 degrees pitch, rollTarget should always lie on the horizontal plane of the vessel
                // Below 35 degrees pitch, use the surf roll logic
                // hysteresis on the switch ensures it doesn't bounce back and forth and lose the lock
                if (rollState)
                {
                    if (!rollStateWas)
                    {
                        Utils.GetSAS(SASList.Roll).SetPoint = 0;
                        Utils.GetSAS(SASList.Roll).skipDerivative = true;
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    }

                    Vector3 proj = FlightData.thisVessel.ReferenceTransform.up * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, rollTarget)
                        + FlightData.thisVessel.ReferenceTransform.right * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.right, rollTarget);
                    double roll = Vector3.Angle(proj, rollTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, rollTarget));

                    FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Roll).ResponseD(roll) / fadeCurrent[(int)SASList.Roll];
                }
                else
                {
                    if (rollStateWas)
                    {
                        Utils.GetSAS(SASList.Roll).SetPoint = FlightData.roll;
                        Utils.GetSAS(SASList.Roll).skipDerivative = true;
                    }

                    if (Utils.GetSAS(SASList.Roll).SetPoint - FlightData.roll >= -180 && Utils.GetSAS(SASList.Roll).SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Roll).ResponseD(FlightData.roll) / fadeCurrent[(int)SASList.Roll];
                    else if (Utils.GetSAS(SASList.Roll).SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Roll).ResponseD(FlightData.roll + 360) / fadeCurrent[(int)SASList.Roll];
                    else if (Utils.GetSAS(SASList.Roll).SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Roll).ResponseD(FlightData.roll - 360) / fadeCurrent[(int)SASList.Roll];
                }
            }
        }

        #region GUI
        public void Draw()
        {
            if (AppLauncherFlight.bDisplaySAS)
                SASwindow = GUILayout.Window(78934856, SASwindow, drawSASWindow, "SAS Module", GUILayout.Height(0));

            if (bShowPresets)
            {
                SASPresetwindow = GUILayout.Window(78934857, SASPresetwindow, drawPresetWindow, "SAS Presets", GUILayout.Height(0));
                SASPresetwindow.x = SASwindow.x + SASwindow.width;
                SASPresetwindow.y = SASwindow.y;
            }
        }

        private void drawSASWindow(int id)
        {
            if (GUI.Button(new Rect(SASwindow.width - 16, 2, 14, 14), ""))
            {
                AppLauncherFlight.bDisplaySAS = false;
            }

            bShowPresets = GUILayout.Toggle(bShowPresets, bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            bStockSAS = GUILayout.Toggle(bStockSAS, bStockSAS ? "Mode: Stock SAS" : "Mode: SSAS");

            if (!bStockSAS)
            {
                GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
                if (GUILayout.Button(bArmed ? "Disarm SAS" : "Arm SAS"))
                {
                    bArmed = !bArmed;
                    if (!bArmed)
                        ActivitySwitch(false);

                    if (bArmed)
                        Messaging.statusMessage(8);
                    else
                        Messaging.statusMessage(9);
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

                if (bArmed)
                {
                    Utils.GetSAS(SASList.Pitch).SetPoint = Utils.Clamp((float)GeneralUI.TogPlusNumBox("Pitch:", ref bActive[(int)SASList.Pitch], ref targets[(int)SASList.Pitch], FlightData.pitch, Utils.GetSAS(SASList.Pitch).SetPoint, 80, 70), -90, 90);
                    Utils.GetSAS(SASList.Yaw).SetPoint = GeneralUI.TogPlusNumBox("Heading:", ref bActive[(int)SASList.Yaw], ref targets[(int)SASList.Yaw], FlightData.heading, Utils.GetSAS(SASList.Yaw).SetPoint, 80, 70);
                    if (!rollState) // editable
                        Utils.GetSAS(SASList.Roll).SetPoint = GeneralUI.TogPlusNumBox("Roll:", ref bActive[(int)SASList.Roll], ref targets[(int)SASList.Roll], FlightData.roll, Utils.GetSAS(SASList.Roll).SetPoint, 80, 70);
                    else // not editable b/c vector mode
                    {
                        GUILayout.BeginHorizontal();
                        bActive[(int)SASList.Roll] = GUILayout.Toggle(bActive[(int)SASList.Roll], "Roll:", GeneralUI.toggleButton, GUILayout.Width(80));
                        GUILayout.TextField(FlightData.roll.ToString("N2"), GUILayout.Width(70));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Box("", GUILayout.Height(10));
                    drawPIDValues(SASList.Pitch, "Pitch");
                    drawPIDValues(SASList.Roll, "Roll");
                    drawPIDValues(SASList.Yaw, "Yaw");
                }
            }
            else
            {
                VesselAutopilot.VesselSAS sas = FlightData.thisVessel.Autopilot.SAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", SASList.Pitch);
                drawPIDValues(sas.pidLockedRoll, "Roll", SASList.Roll);
                drawPIDValues(sas.pidLockedYaw, "Yaw", SASList.Yaw);
            }

            GUI.DragWindow();
        }

        private void drawPIDValues(SASList controllerID, string inputName)
        {
            PID_Controller controller = Utils.GetSAS(controllerID);
            controller.bShow = GUILayout.Toggle(controller.bShow, inputName, GeneralUI.toggleButton);

            if (controller.bShow)
            {
                controller.PGain = GeneralUI.labPlusNumBox("Kp:", controller.PGain.ToString("G3"), 45);
                controller.IGain = GeneralUI.labPlusNumBox("Ki:", controller.IGain.ToString("G3"), 45);
                controller.DGain = GeneralUI.labPlusNumBox("Kd:", controller.DGain.ToString("G3"), 45);
                controller.Scalar = GeneralUI.labPlusNumBox("Scalar:", controller.Scalar.ToString("G3"), 45);
                delayEngage[(int)controllerID] = Math.Max((float)GeneralUI.labPlusNumBox("Delay:", delayEngage[(int)controllerID].ToString("G3"), 45), 0);
            }
        }

        private void drawPIDValues(PIDclamp controller, string inputName, SASList controllerID)
        {
            stockPIDDisplay[(int)controllerID] = GUILayout.Toggle(stockPIDDisplay[(int)controllerID], inputName, GeneralUI.toggleButton);

            if (stockPIDDisplay[(int)controllerID])
            {
                controller.kp = GeneralUI.labPlusNumBox("Kp:", controller.kp.ToString("G3"), 45);
                controller.ki = GeneralUI.labPlusNumBox("Ki:", controller.ki.ToString("G3"), 45);
                controller.kd = GeneralUI.labPlusNumBox("Kd:", controller.kd.ToString("G3"), 45);
                controller.clamp = Math.Max(GeneralUI.labPlusNumBox("Scalar:", controller.clamp.ToString("G3"), 45), 0.01);
            }
        }

        private void drawPresetWindow(int id)
        {
            if (GUI.Button(new Rect(SASPresetwindow.width - 16, 2, 14, 14), ""))
            {
                bShowPresets = false;
            }

            if (bStockSAS)
                drawStockPreset();
            else
                drawSurfPreset();
        }

        private void drawSurfPreset()
        {
            if (PresetManager.Instance.activeSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeSASPreset.name));
                if (PresetManager.Instance.activeSASPreset.name != "SSAS")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.updateSASPreset(false, SASControllers);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSASPreset(ref newPresetName, SASControllers);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadSASPreset(PresetManager.Instance.craftPresetList["default"].SSASPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SASPreset p in PresetManager.Instance.SASPresetList)
            {
                if (p.bStockSAS)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSASPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }

        private void drawStockPreset()
        {
            if (PresetManager.Instance.activeStockSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeStockSASPreset.name));
                if (PresetManager.Instance.activeStockSASPreset.name != "stock")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.updateSASPreset(true);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSASPreset(ref newPresetName);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadStockSASPreset(PresetManager.Instance.craftPresetList["default"].StockPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SASPreset p in PresetManager.Instance.SASPresetList)
            {
                if (!p.bStockSAS)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadStockSASPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion
    }
}