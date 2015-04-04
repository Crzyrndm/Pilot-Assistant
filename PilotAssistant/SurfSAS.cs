using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using Presets;

    public enum SASList
    {
        Pitch,
        Bank,
        Hdg
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
        public bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
        public bool[] bActive = new bool[3]; // activate on per axis basis
        public bool[] bPause = new bool[3]; // pause on a per axis basis
        public bool bStockSAS = true;

        public string[] targets = { "0.00", "0.00", "0.00" };

        // unpause control authority scaling. Helps reduce the jump of SSAS gaining control of an axis
        public float[] fadeCurrent = { 1, 1, 1 }; // these are the current axis control factors
        public float[] timeElapsed = new float[3];
        float[] fadeSetpoint = { 10, 10, 10 }; // these are the values that get assigned every time control is unlocked
        const float fadeMult = 0.97f; // this is the decay rate. 0.97 < 1 after 0.75s starting from 10

        // unpause delay
        public float[] delayEngage = new float[3];

        public bool rollState = false; // false = surface mode, true = vector mode

        public Rect SASwindow = new Rect(10, 505, 200, 30); // gui window rect
        bool[] stockPIDDisplay = { true, false, false }; // which stock PID axes are visible

        string newPresetName = "";
        Rect SASPresetwindow = new Rect(550, 50, 50, 50);
        bool bShowPresets = false;

        // initialisation and default presets stuff
        public static double[] defaultPitchGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3, 200 };
        public static double[] defaultRollGains = { 0.1, 0.0, 0.06, -1, 1, -1, 1, 3, 200 };
        public static double[] defaultHdgGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3, 1 };

        public static double[] defaultPresetPitchGains = { 0.15, 0.0, 0.06, 3, 20 }; // Kp/i/d, scalar, delay
        public static double[] defaultPresetRollGains = { 0.1, 0.0, 0.06, 3, 20 };
        public static double[] defaultPresetHdgGains = { 0.15, 0.0, 0.06, 3, 20 };

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
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                updateTarget();
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
                SASControllers[(int)SASList.Bank] = new PID_Controller(defaultRollGains);
                SASControllers[(int)SASList.Hdg] = new PID_Controller(defaultHdgGains);

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
                bInit = true;
            }
            PresetManager.initSSASPreset();
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

            if (bActive[(int)SASList.Hdg])
                SASList.Hdg.GetSAS().SetPoint = calculateTargetHeading(axisLock);
        }

        public void drawGUI()
        {
            GUI.skin = GeneralUI.UISkin;

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

            SASwindow = GUILayout.Window(78934856, SASwindow, drawSASWindow, "SAS Module", GUILayout.Height(0));

            if (tooltip != "" && PilotAssistant.Instance.showTooltips)
                GUILayout.Window(34246, new Rect(SASwindow.x + SASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));

            if (bShowPresets)
            {
                SASPresetwindow = GUILayout.Window(78934857, SASPresetwindow, drawPresetWindow, "SAS Presets", GUILayout.Height(0));
                SASPresetwindow.x = SASwindow.x + SASwindow.width;
                SASPresetwindow.y = SASwindow.y;
            }
        }

        private void SurfaceSAS(FlightCtrlState state)
        {
            if (bArmed && ActivityCheck())
            {
                pauseManager(state);

                double vertResponse = 0;
                if (bActive[(int)SASList.Pitch])
                    vertResponse = -1 * SASList.Pitch.GetSAS().ResponseD(FlightData.pitch);

                double hrztResponse = 0;
                if (bActive[(int)SASList.Hdg])
                {
                    if (Utils.GetSAS(SASList.Hdg).SetPoint - FlightData.heading >= -180 && Utils.GetSAS(SASList.Hdg).SetPoint - FlightData.heading <= 180)
                        hrztResponse = -1 * Utils.GetSAS(SASList.Hdg).ResponseD(FlightData.heading);
                    else if (Utils.GetSAS(SASList.Hdg).SetPoint - FlightData.heading < -180)
                        hrztResponse = -1 * Utils.GetSAS(SASList.Hdg).ResponseD(FlightData.heading - 360);
                    else if (Utils.GetSAS(SASList.Hdg).SetPoint - FlightData.heading > 180)
                        hrztResponse = -1 * Utils.GetSAS(SASList.Hdg).ResponseD(FlightData.heading + 360);
                }

                double rollRad = Mathf.Deg2Rad * FlightData.roll;

                if (Math.Abs(FlightData.roll) > 5)
                {
                    if ((!bPause[(int)SASList.Pitch] || !bPause[(int)SASList.Hdg]) && (bActive[(int)SASList.Pitch] || bActive[(int)SASList.Hdg]))
                    {
                        state.pitch = pitchSet = (float)(vertResponse * Math.Cos(rollRad) - hrztResponse * Math.Sin(rollRad)) / fadeCurrent[(int)SASList.Pitch];
                        state.yaw = (float)(vertResponse * Math.Sin(rollRad) + hrztResponse * Math.Cos(rollRad)) / fadeCurrent[(int)SASList.Hdg];
                    }
                }
                else
                {
                    if (bActive[(int)SASList.Pitch] && !bPause[(int)SASList.Pitch])
                        state.pitch = pitchSet = (float)(vertResponse * Math.Cos(rollRad) - hrztResponse * Math.Sin(rollRad)) / fadeCurrent[(int)SASList.Pitch];
                    if (bActive[(int)SASList.Hdg] && !bPause[(int)SASList.Hdg])
                        state.yaw = (float)(vertResponse * Math.Sin(rollRad) + hrztResponse * Math.Cos(rollRad)) / fadeCurrent[(int)SASList.Hdg];
                }
                rollResponse();
            }

            if (!bActive[(int)SASList.Pitch] || !bArmed)
                pitchSet = 0;
        }

        public float pitchSet { get; set; }

        private void updateTarget()
        {
            StartCoroutine(FadeInPitch());
            StartCoroutine(FadeInRoll());
            StartCoroutine(FadeInHdg());
        }

        private void pauseManager(FlightCtrlState state)
        {
            if (Utils.isFlightControlLocked())
                return;

            if (!bPause[(int)SASList.Pitch])
            {
                Debug.Log("pitch pause check");
                Debug.Log(bPause[(int)SASList.Pitch]);
                Debug.Log(state.pitch);
                Debug.Log(state.yaw);
                Debug.Log(Math.Abs(FlightData.roll) > 5);
                Debug.Log(!bPause[(int)SASList.Pitch] && (state.pitch != 0 || (state.yaw != 0 && Math.Abs(FlightData.roll) > 5)));
            }

            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)SASList.Pitch] && (state.pitch != 0 || (state.yaw != 0 && Math.Abs(FlightData.roll) > 5)))
            {
                Debug.Log("PAUSED");
                bPause[(int)SASList.Pitch] = true;
            }
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Pitch] && state.pitch == 0 && (state.yaw == 0 || Math.Abs(FlightData.roll) <= 5))
            {
                Debug.Log("UNPAUSED");
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                    StartCoroutine(FadeInPitch());
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)SASList.Hdg] && (state.yaw != 0 || (state.pitch != 0 && Math.Abs(FlightData.roll) > 5)))
                bPause[(int)SASList.Hdg] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Hdg] && state.yaw == 0 && (state.pitch == 0 || Math.Abs(FlightData.roll) <= 5))
            {
                bPause[(int)SASList.Hdg] = false;
                if (bActive[(int)SASList.Hdg])
                    StartCoroutine(FadeInHdg());
            }
            
            if (state.roll != 0 && !bPause[(int)SASList.Bank])
                bPause[(int)SASList.Bank] = true;
            else if (state.roll == 0 && bPause[(int)SASList.Bank])
            {
                bPause[(int)SASList.Bank] = false;
                if (bActive[(int)SASList.Bank])
                        StartCoroutine(FadeInRoll());
            }
        }

        bool pitchEnum = false;
        IEnumerator FadeInPitch()
        {
            SASList.Pitch.GetSAS().skipDerivative = true;
            SASList.Pitch.GetSAS().SetPoint = FlightData.pitch;
            // initialse all relevant values
            timeElapsed[(int)SASList.Pitch] = 0;
            fadeCurrent[(int)SASList.Pitch] = fadeSetpoint[(int)SASList.Pitch]; // x to the power of 0 is 1

            if (pitchEnum) // don't need multiple running at once
                yield break;
            pitchEnum = true;

            while (fadeCurrent[(int)SASList.Pitch] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Pitch] += TimeWarp.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Pitch] < delayEngage[(int)SASList.Pitch])
                {
                    Utils.GetSAS(SASList.Pitch).SetPoint = FlightData.pitch;
                    targets[(int)SASList.Pitch] = FlightData.pitch.ToString("0.00");
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
            SASList.Bank.GetSAS().skipDerivative = true;
            SASList.Bank.GetSAS().SetPoint = rollState ? 0 : FlightData.roll;
            rollTarget = FlightData.thisVessel.ReferenceTransform.right;

            // initialse all relevant values
            timeElapsed[(int)SASList.Bank] = 0;
            fadeCurrent[(int)SASList.Bank] = fadeSetpoint[(int)SASList.Bank]; // x to the power of 0 is 1

            if (rollEnum) // don't need multiple running at once
                yield break;
            rollEnum = true;

            while (fadeCurrent[(int)SASList.Bank] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Bank] += TimeWarp.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Bank] < delayEngage[(int)SASList.Bank])
                {
                    if (rollState)
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    else
                    {
                        Utils.GetSAS(SASList.Bank).SetPoint = FlightData.roll;
                        targets[(int)SASList.Bank] = FlightData.roll.ToString("0.00");
                    }
                }
                else
                    fadeCurrent[(int)SASList.Bank] = Mathf.Max(fadeSetpoint[(int)SASList.Bank] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Bank]), 1);
            }

            // make sure we are actually at 1.0
            fadeCurrent[(int)SASList.Bank] = 1.0f;
            // clear the lock
            rollEnum = false;
        }

        bool yawEnum = false;
        IEnumerator FadeInHdg()
        {
            // initialse all relevant values
            timeElapsed[(int)SASList.Hdg] = 0;
            fadeCurrent[(int)SASList.Hdg] = fadeSetpoint[(int)SASList.Hdg]; // x to the power of 0 is 1

            if (yawEnum) // don't need multiple running at once
                yield break;
            yawEnum = true;

            bool updated = false;
            while (fadeCurrent[(int)SASList.Hdg] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Hdg] += TimeWarp.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Hdg] < delayEngage[(int)SASList.Hdg])
                {
                    updated = true;
                    
                    stop = true;
                    headingEdit = false;
                    axisLock = vecHeading(FlightData.heading);
                    SASList.Hdg.GetSAS().skipDerivative = true;
                }
                else
                    fadeCurrent[(int)SASList.Hdg] = Mathf.Max(fadeSetpoint[(int)SASList.Hdg] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Hdg] - delayEngage[(int)SASList.Hdg]), 1);
            }
            if (!updated)
            {
                stop = true;
                headingEdit = false;
                axisLock = vecHeading(FlightData.heading);
                SASList.Hdg.GetSAS().skipDerivative = true;
            }

            // make sure we are actually set to 1
            fadeCurrent[(int)SASList.Hdg] = 1.0f;
            
            // clear the lock
            yawEnum = false;
        }

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        public static void ActivitySwitch(bool enable)
        {
            if (enable)
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Bank] = instance.bActive[(int)SASList.Hdg] = true;
            else
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Bank] = instance.bActive[(int)SASList.Hdg] = false;
        }

        /// <summary>
        /// returns true if SSAS is active
        /// </summary>
        /// <returns></returns>
        public static bool ActivityCheck()
        {
            if (instance.bActive[(int)SASList.Pitch] || instance.bActive[(int)SASList.Bank] || instance.bActive[(int)SASList.Hdg])
                return true;
            else
                return false;
        }

        /// <summary>
        /// set stock SAS state
        /// </summary>
        /// <param name="state"></param>
        public static void setStockSAS(bool state)
        {
            FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, state);
            FlightData.thisVessel.ctrlState.killRot = state; // incase anyone checks the ctrl state (should be using checking vessel.ActionGroup[KSPActionGroup.SAS])
        }


        static Vector3d rollTarget = Vector3d.zero;
        private void rollResponse()
        {
            if (!bPause[(int)SASList.Bank] && bActive[(int)SASList.Bank])
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
                        Utils.GetSAS(SASList.Bank).SetPoint = 0;
                        Utils.GetSAS(SASList.Bank).skipDerivative = true;
                        rollTarget = FlightData.thisVessel.ReferenceTransform.right;
                    }

                    Vector3 proj = FlightData.thisVessel.ReferenceTransform.up * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.up, rollTarget)
                        + FlightData.thisVessel.ReferenceTransform.right * Vector3.Dot(FlightData.thisVessel.ReferenceTransform.right, rollTarget);
                    double roll = Vector3.Angle(proj, rollTarget) * Math.Sign(Vector3.Dot(FlightData.thisVessel.ReferenceTransform.forward, rollTarget));

                    FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Bank).ResponseD(roll) / fadeCurrent[(int)SASList.Bank];
                }
                else
                {
                    if (rollStateWas)
                    {
                        Utils.GetSAS(SASList.Bank).SetPoint = FlightData.roll;
                        Utils.GetSAS(SASList.Bank).skipDerivative = true;
                    }

                    if (Utils.GetSAS(SASList.Bank).SetPoint - FlightData.roll >= -180 && Utils.GetSAS(SASList.Bank).SetPoint - FlightData.roll <= 180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Bank).ResponseD(FlightData.roll) / fadeCurrent[(int)SASList.Bank];
                    else if (Utils.GetSAS(SASList.Bank).SetPoint - FlightData.roll > 180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Bank).ResponseD(FlightData.roll + 360) / fadeCurrent[(int)SASList.Bank];
                    else if (Utils.GetSAS(SASList.Bank).SetPoint - FlightData.roll < -180)
                        FlightData.thisVessel.ctrlState.roll = (float)Utils.GetSAS(SASList.Bank).ResponseD(FlightData.roll - 360) / fadeCurrent[(int)SASList.Bank];
                }
            }
        }

        /// <summary>
        /// calculate current heading from target vector
        /// </summary>
        public double calculateTargetHeading(Vector3 axisLock)
        {
            Vector3 fwd = Vector3.Cross(FlightData.planetUp, axisLock);
            double heading = -1 * Vector3.Angle(fwd, FlightData.planetNorth) * Math.Sign(Vector3.Dot(fwd, FlightData.planetEast));
            if (heading < 0)
                heading += 360;
            return heading;
        }

        /// <summary>
        /// Get the direction vector for a given heading
        /// </summary>
        public Vector3 vecHeading(double heading)
        {
            double angleDiff = heading - FlightData.heading;
            return Quaternion.AngleAxis((float)(angleDiff - 90), (Vector3)FlightData.planetUp) * FlightData.surfVesForward;
        }

        /// <summary>
        /// calculates the angle to feed corrected for 0/360 crossings
        /// eg. if the target is 350 and the current is 10, it will return 370 giving a diff of -20 degrees
        /// else you get +ve 340 and the turn is in the wrong direction
        /// </summary>
        double CurrentAngleTargetRel(double current, double target)
        {
            if (target - current < -180)
                return current - 360;
            else if (target - current > 180)
                return current + 360;
            else
                return current;
        }

        public Vector3 currentTarget = Vector3.zero; // this is the vec the Ienumerator is moving
        public Vector3 newTarget = Vector3.zero; // this is the vec we are moving to
        public Vector3 axisLock = Vector3.zero; // this is our controlVec
        double increment = 0; // this is the angle to shift per second
        bool running = false;
        bool stop = false;
        bool headingEdit = false;
        public IEnumerator shiftHeadingTarget(double newHdg)
        {
            newTarget = vecHeading(newHdg);
            currentTarget = vecHeading(SASList.Hdg.GetSAS().BumplessSetPoint);
            increment = 0;

            if (running)
                yield break;
            running = true;

            while (!stop && Math.Abs(Vector3.Angle(currentTarget, newTarget)) > 0.01)
            {
                double finalTarget = calculateTargetHeading(newTarget);
                double target = calculateTargetHeading(currentTarget);
                increment += SASList.Hdg.GetSAS().Easing * TimeWarp.fixedDeltaTime * 0.01;

                double remainder = finalTarget - CurrentAngleTargetRel(target, finalTarget);
                if (remainder < 0)
                    target += Math.Max(-1 * increment, remainder);
                else
                    target += Math.Min(increment, remainder);

                axisLock = vecHeading(target);
                currentTarget = vecHeading(target);
                yield return new WaitForFixedUpdate();
            }
            if (!stop)
                axisLock = newTarget;
            running = false;
        }

        #region GUI
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

                    Messaging.statusMessage(bArmed ? 8 : 9);
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

                if (bArmed)
                {
                    Utils.GetSAS(SASList.Pitch).BumplessSetPoint = Utils.Clamp(TogPlusNumBox("Pitch:", SASList.Pitch, FlightData.pitch, 80, 70), -90, 90);
                    TogPlusNumBox("Heading:", SASList.Hdg, FlightData.heading, 80, 70);
                    Utils.GetSAS(SASList.Bank).BumplessSetPoint = TogPlusNumBox("Roll:", SASList.Bank, FlightData.roll, 80, 70);
                    
                    GUILayout.Box("", GUILayout.Height(10)); // seperator

                    drawPIDValues(SASList.Pitch, "Pitch");
                    drawPIDValues(SASList.Bank, "Roll");
                    drawPIDValues(SASList.Hdg, "Yaw");
                }
            }
            else
            {
                VesselAutopilot.VesselSAS sas = FlightData.thisVessel.Autopilot.SAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", SASList.Pitch);
                drawPIDValues(sas.pidLockedRoll, "Roll", SASList.Bank);
                drawPIDValues(sas.pidLockedYaw, "Yaw", SASList.Hdg);
            }

            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        string tooltip;
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDValues(SASList controllerID, string inputName)
        {
            PID_Controller controller = Utils.GetSAS(controllerID);
            controller.bShow = GUILayout.Toggle(controller.bShow, inputName, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (controller.bShow)
            {
                controller.PGain = GeneralUI.labPlusNumBox(GeneralUI.KpLabel, controller.PGain.ToString("N3"), 45);
                controller.IGain = GeneralUI.labPlusNumBox(GeneralUI.KiLabel, controller.IGain.ToString("N3"), 45);
                controller.DGain = GeneralUI.labPlusNumBox(GeneralUI.KdLabel, controller.DGain.ToString("N3"), 45);
                controller.Scalar = GeneralUI.labPlusNumBox(GeneralUI.ScalarLabel, controller.Scalar.ToString("N3"), 45);
                delayEngage[(int)controllerID] = Math.Max((float)GeneralUI.labPlusNumBox(GeneralUI.DelayLabel, delayEngage[(int)controllerID].ToString("N3"), 45), 0);
            }
        }

        private void drawPIDValues(PIDclamp controller, string inputName, SASList controllerID)
        {
            stockPIDDisplay[(int)controllerID] = GUILayout.Toggle(stockPIDDisplay[(int)controllerID], inputName, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (stockPIDDisplay[(int)controllerID])
            {
                controller.kp = GeneralUI.labPlusNumBox(GeneralUI.KpLabel, controller.kp.ToString(), 45);
                controller.ki = GeneralUI.labPlusNumBox(GeneralUI.KiLabel, controller.ki.ToString(), 45);
                controller.kd = GeneralUI.labPlusNumBox(GeneralUI.KdLabel, controller.kd.ToString(), 45);
                controller.clamp = Math.Max(GeneralUI.labPlusNumBox(GeneralUI.ScalarLabel, controller.clamp.ToString(), 45), 0.01);
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

        /// <summary>
        /// Draws a toggle button and text box of specified widths with update button.
        /// </summary>
        /// <param name="toggleText"></param>
        /// <param name="boxVal"></param>
        /// <param name="toggleWidth"></param>
        /// <param name="boxWidth"></param>
        /// <returns></returns>
        public double TogPlusNumBox(string toggleText, SASList controllerID, double currentVal, float toggleWidth, float boxWidth)
        {
            double setPoint = controllerID.GetSAS().SetPoint;

            GUILayout.BeginHorizontal();

            bool tempState = GUILayout.Toggle(bActive[(int)controllerID], toggleText, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(toggleWidth));
            if (tempState != bActive[(int)controllerID])
            {
                bActive[(int)controllerID] = tempState;
                if (bActive[(int)controllerID])
                {
                    setPoint = currentVal;
                    targets[(int)controllerID] = currentVal.ToString("0.00");

                    if (controllerID == SASList.Hdg)
                        axisLock = vecHeading(FlightData.heading);
                    controllerID.GetSAS().skipDerivative = true;
                }
            }

            if (controllerID != SASList.Bank || !SurfSAS.Instance.rollState)
            {
                if (controllerID == SASList.Hdg && !headingEdit)
                {
                    if (running)
                        targets[(int)controllerID] = calculateTargetHeading(newTarget).ToString("0.00");
                    else
                        targets[(int)controllerID] = calculateTargetHeading(axisLock).ToString("0.00");
                }
                string tempText = GUILayout.TextField(targets[(int)controllerID], GeneralUI.UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
                if (controllerID == SASList.Hdg && targets[(int)controllerID] != tempText)
                    headingEdit = true;
                targets[(int)controllerID] = tempText;

                if (GUILayout.Button("u"))
                {
                    headingEdit = false;
                    double temp;
                    if (double.TryParse(targets[(int)controllerID], out temp))
                        setPoint = temp;

                    if (controllerID == SASList.Hdg)
                    {
                        stop = false;
                        StartCoroutine(shiftHeadingTarget(setPoint));
                        targets[(int)controllerID] = setPoint.ToString("0.00");
                    }
                    bActive[(int)controllerID] = true;
                    controllerID.GetSAS().skipDerivative = true;
                }
            }
            else
                GUILayout.TextField(targets[(int)controllerID], GeneralUI.UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));

            GUILayout.EndHorizontal();
            return setPoint;
        }
        #endregion
    }
}