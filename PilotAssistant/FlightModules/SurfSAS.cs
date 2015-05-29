using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.FlightModules
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

    class SurfSAS
    {
        #region Globals
        private static SurfSAS instance;
        public static SurfSAS Instance
        {
            get
            {
                if (instance == null)
                    instance = new SurfSAS();
                return instance;
            }
        }

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        public SASController[] SASControllers = new SASController[3]; // controller per axis

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
        double bankAngleSynch = 5; // the bank angle below which pitch and yaw unlock seperately

        // unpause delay
        public float[] delayEngage = new float[3];

        public bool rollState = false; // false = surface mode, true = vector mode

        public Rect SSASwindow = new Rect(10, 505, 200, 30); // gui window rect

        string newPresetName = "";

        Rect SSASPresetwindow = new Rect(550, 50, 50, 50);
        bool bShowSSASPresets = false;


        // initialisation and default presets stuff
        public double[] defaultPitchGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3, 200 };
        public double[] defaultRollGains = { 0.1, 0.0, 0.06, -1, 1, -1, 1, 3, 200 };
        public double[] defaultHdgGains = { 0.15, 0.0, 0.06, -1, 1, -1, 1, 3, 1 };

        public Vector3 currentDirectionTarget = Vector3.zero; // this is the vec the IEnumerator is moving
        public Vector3 newDirectionTarget = Vector3.zero; // this is the vec we are moving to
        double increment = 0; // this is the angle to shift per second
        bool hdgShiftIsRunning = false;
        bool stopHdgShift = false;
        bool headingEdit = false;

        Vector3d rollTarget = Vector3d.zero;

        #endregion

        public void Start()
        {
            instance = this;

            bPause.Initialize();
            ActivitySwitch(false);
            delayEngage[0] = delayEngage[1] = delayEngage[2] = 20; // delay engagement by 0.2s

            SASControllers[(int)SASList.Pitch] = new SASController(SASList.Pitch, defaultPitchGains);
            SASControllers[(int)SASList.Bank] = new SASController(SASList.Bank, defaultRollGains);
            SASControllers[(int)SASList.Hdg] = new SASController(SASList.Hdg, defaultHdgGains);
            SASList.Hdg.GetSAS().isHeadingControl = true;

            PresetManager.initDefaultPresets(new SSASPreset(SASControllers, "SSAS"));
            
            tooltip = "";
        }

        public void warpHandler()
        {
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                updateTarget();
        }

        public void OnDestroy()
        {
            bArmed = false;
            ActivitySwitch(false);
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(SurfaceSAS);
            instance = null;
        }

        #region Update / Input monitoring
        public void Update()
        {
            bool mod = GameSettings.MODIFIER_KEY.GetKey();

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
                SASList.Hdg.GetSAS().SetPoint = Utils.calculateTargetHeading(currentDirectionTarget);
        }
        #endregion

        #region Fixed Update / Control
        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed /* || !ActivityCheck() */|| !FlightData.thisVessel.IsControllable)
                return;

            pauseManager(state);

            QuaternionResponse(state);
        }

        public PIDErrorController[] QuatControlArray = new PIDErrorController[3] { 
                                              new PIDErrorController(SASList.Pitch, 0.15, 0.05, 0.1, -1, 1, -1, 1)
                                            , new PIDErrorController(SASList.Bank, 0.15, 0.05, 0.1, -1, 1, -1, 1)
                                            , new PIDErrorController(SASList.Hdg, 0.15, 0.05, 0.1, -1, 1, -1, 1) };

        void QuaternionResponse(FlightCtrlState state)
        {
            Transform vesRefTrans = FlightData.thisVessel.ReferenceTransform.transform;
            Quaternion vesRefRot = vesRefTrans.rotation;
            Quaternion currentRotation = vesRefRot * Quaternion.AngleAxis(-90, FlightData.thisVessel.ReferenceTransform.transform.up);
            Quaternion targetRotation = Quaternion.LookRotation(FlightData.planetNorth, FlightData.planetUp);

            //////////////////////////////////////////////////////////////////
            // (roll, heading, pitch) error in 0-360 degrees (will need to make that +/- 180)
            Vector3 angleDiff = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * vesRefRot.Inverse() * targetRotation).eulerAngles;
            Vector3d targetUp = vesRefRot.Inverse() * targetRotation * Vector3d.forward;
            Vector3d currentUp = Vector3d.up;

            double turnAngle = Math.Abs(Vector3d.Angle(currentUp, targetUp));
            Vector2d direction = (new Vector2d(targetUp.x, targetUp.z)).normalized;
            Vector3d newDiff = new Vector3d(-direction.y * turnAngle, Utils.headingClamp(angleDiff.z, 180), direction.x * turnAngle);

            if (bActive[(int)SASList.Bank] && !bPause[(int)SASList.Bank])
                state.roll = QuatControlArray[(int)SASList.Bank].ResponseF(newDiff.y, FlightData.thisVessel.angularVelocity.y * Mathf.Rad2Deg);
            if (bActive[(int)SASList.Pitch] && !bPause[(int)SASList.Pitch])
                state.pitch = QuatControlArray[(int)SASList.Pitch].ResponseF(newDiff.x, FlightData.thisVessel.angularVelocity.x * Mathf.Rad2Deg);
            if (bActive[(int)SASList.Hdg] && !bPause[(int)SASList.Hdg])
                state.yaw = QuatControlArray[(int)SASList.Hdg].ResponseF(newDiff.z, FlightData.thisVessel.angularVelocity.z * Mathf.Rad2Deg);
        }

        private void rollResponse(FlightCtrlState state)
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

                    state.roll = SASList.Bank.GetSAS().ResponseF(roll) / fadeCurrent[(int)SASList.Bank];
                }
                else
                {
                    if (rollStateWas)
                    {
                        SASList.Bank.GetSAS().SetPoint = FlightData.bank;
                        SASList.Bank.GetSAS().skipDerivative = true;
                    }

                    state.roll = SASList.Bank.GetSAS().ResponseF(Utils.CurrentAngleTargetRel(FlightData.bank, SASList.Bank.GetSAS().SetPoint, 180)) / fadeCurrent[(int)SASList.Bank];
                }
            }
        }

        public IEnumerator shiftHeadingTarget(double newHdg)
        {
            stopHdgShift = false;
            headingEdit = false;
            newDirectionTarget = Utils.vecHeading(newHdg);
            currentDirectionTarget = Utils.vecHeading(SASList.Hdg.GetSAS().BumplessSetPoint);
            increment = 0;

            if (hdgShiftIsRunning)
                yield break;
            hdgShiftIsRunning = true;

            while (!stopHdgShift && Math.Abs(Vector3.Angle(currentDirectionTarget, newDirectionTarget)) > 0.01)
            {
                double finalTarget = Utils.calculateTargetHeading(newDirectionTarget);
                double target = Utils.calculateTargetHeading(currentDirectionTarget);
                increment += SASList.Hdg.GetSAS().Easing * TimeWarp.fixedDeltaTime * 0.01;

                double remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                if (remainder < 0)
                    target += Math.Max(-1 * increment, remainder);
                else
                    target += Math.Min(increment, remainder);

                currentDirectionTarget = Utils.vecHeading(target);
                yield return new WaitForFixedUpdate();
            }
            if (!stopHdgShift)
                currentDirectionTarget = newDirectionTarget;
            hdgShiftIsRunning = false;
        }

        private void pauseManager(FlightCtrlState state)
        {
            if (Utils.isFlightControlLocked())
                return;

            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)SASList.Pitch] && (state.pitch != 0 || (state.yaw != 0 && Math.Abs(FlightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Pitch] = true;
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Pitch] && state.pitch == 0 && (state.yaw == 0 || Math.Abs(FlightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                    StartCoroutine(FadeInPitch());
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)SASList.Hdg] && (state.yaw != 0 || (state.pitch != 0 && Math.Abs(FlightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Hdg] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Hdg] && state.yaw == 0 && (state.pitch == 0 || Math.Abs(FlightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Hdg] = false;
                if (bActive[(int)SASList.Hdg])
                    StartCoroutine(FadeInHdg());
            }

            if (!bPause[(int)SASList.Bank] && state.roll != 0)
                bPause[(int)SASList.Bank] = true;
            else if (bPause[(int)SASList.Bank] && state.roll == 0)
            {
                bPause[(int)SASList.Bank] = false;
                if (bActive[(int)SASList.Bank])
                    StartCoroutine(FadeInRoll());
            }
        }

        private void updateTarget()
        {
            StartCoroutine(FadeInPitch());
            StartCoroutine(FadeInRoll());
            StartCoroutine(FadeInHdg());
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
            SASList.Bank.GetSAS().SetPoint = rollState ? 0 : FlightData.bank;
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
                        Utils.GetSAS(SASList.Bank).SetPoint = FlightData.bank;
                        targets[(int)SASList.Bank] = FlightData.bank.ToString("0.00");
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
                    
                    stopHdgShift = true;
                    headingEdit = false;
                    currentDirectionTarget = Utils.vecHeading(FlightData.heading);
                    SASList.Hdg.GetSAS().skipDerivative = true;
                }
                else
                    fadeCurrent[(int)SASList.Hdg] = Mathf.Max(fadeSetpoint[(int)SASList.Hdg] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Hdg] - delayEngage[(int)SASList.Hdg]), 1);
            }
            if (!updated)
            {
                stopHdgShift = true;
                headingEdit = false;
                currentDirectionTarget = Utils.vecHeading(FlightData.heading);
                SASList.Hdg.GetSAS().skipDerivative = true;
            }

            // make sure we are actually set to 1
            fadeCurrent[(int)SASList.Hdg] = 1.0f;
            
            // clear the lock
            yawEnum = false;
        }
        #endregion

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

        #region GUI
        public void drawGUI()
        {
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
            if (PilotAssistantFlightCore.bDisplaySSAS)
            {
                SSASwindow = GUILayout.Window(78934856, SSASwindow, drawSSASWindow, "SSAS", GUILayout.Height(0));

                if (bShowSSASPresets)
                {
                    SSASPresetwindow = GUILayout.Window(78934859, SSASPresetwindow, drawSSASPresetWindow, "SSAS Presets", GUILayout.Height(0));
                    SSASPresetwindow.x = SSASwindow.x + SSASwindow.width;
                    SSASPresetwindow.y = SSASwindow.y;
                }
            }

            if (tooltip != "" && PilotAssistantFlightCore.showTooltips)
                GUILayout.Window(34246, new Rect(SSASwindow.x + SSASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));
        }

        private void drawSSASWindow(int id)
        {
            if (GUI.Button(new Rect(SSASwindow.width - 16, 2, 14, 14), ""))
                PilotAssistantFlightCore.bDisplaySSAS = false;

            bShowSSASPresets = GUILayout.Toggle(bShowSSASPresets, bShowSSASPresets ? "Hide SAS Presets" : "Show SAS Presets");

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button(bArmed ? "Disarm SAS" : "Arm SAS"))
            {
                bArmed = !bArmed;
                if (!bArmed)
                    ActivitySwitch(false);

                GeneralUI.postMessage(bArmed ? "SSAS Armed" : "SSAS Disarmed");
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            if (bArmed)
            {
                Utils.GetSAS(SASList.Pitch).BumplessSetPoint = Utils.Clamp(TogPlusNumBox("Pitch:", SASList.Pitch, FlightData.pitch, 80, 70), -90, 90);
                TogPlusNumBox("Heading:", SASList.Hdg, FlightData.heading, 80, 70);
                Utils.GetSAS(SASList.Bank).BumplessSetPoint = TogPlusNumBox("Roll:", SASList.Bank, FlightData.bank, 80, 70);

                GUILayout.Box("", GUILayout.Height(10)); // seperator

                drawPIDValues(SASList.Pitch, "Pitch");
                drawPIDValues(SASList.Bank, "Roll");
                drawPIDValues(SASList.Hdg, "Yaw");
            }

            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDValues(SASList controllerID, string inputName)
        {
            SASController controller = Utils.GetSAS(controllerID);
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

        private void drawSSASPresetWindow(int id)
        {
            if (GUI.Button(new Rect(SSASPresetwindow.width - 16, 2, 14, 14), ""))
                bShowSSASPresets = false;

            if (PresetManager.Instance.activeSSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeSSASPreset.name));
                if (PresetManager.Instance.activeSSASPreset.name != "SSAS")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.UpdateSSASPreset();
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSSASPreset(ref newPresetName, SASControllers);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadSSASPreset(PresetManager.Instance.craftPresetDict["default"].SSASPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SSASPreset p in PresetManager.Instance.SSASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSSASPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteSSASPreset(p);
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
                        currentDirectionTarget = Utils.vecHeading(FlightData.heading);
                    controllerID.GetSAS().skipDerivative = true;
                }
            }

            if (controllerID != SASList.Bank || !SurfSAS.Instance.rollState)
            {
                if (controllerID == SASList.Hdg && !headingEdit)
                {
                    if (hdgShiftIsRunning)
                        targets[(int)controllerID] = Utils.calculateTargetHeading(newDirectionTarget).ToString("0.00");
                    else
                        targets[(int)controllerID] = Utils.calculateTargetHeading(currentDirectionTarget).ToString("0.00");
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
                        StartCoroutine(shiftHeadingTarget(setPoint));

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