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

        public PIDErrorController[] SASControllers = new PIDErrorController[3]; // controller per axis

        public bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
        public bool[] bActive = new bool[3]; // activate on per axis basis
        public bool[] bPause = new bool[3]; // pause on a per axis basis

        public string[] targets = { "0.00", "0.00", "0.00" };

        // unpause control authority scaling. Helps reduce the jump of SSAS gaining control of an axis
        public float[] fadeCurrent = { 1, 1, 1 }; // these are the current axis control factors
        public float[] timeElapsed = new float[3];
        float[] fadeSetpoint = { 10, 10, 10 }; // these are the values that get assigned every time control is unlocked
        const float fadeMult = 0.97f; // this is the decay rate. 0.97 < 1 after 0.75s starting from 10
        double bankAngleSynch = 5; // the bank angle below which pitch and yaw unlock seperately

        // unpause delay
        public float[] delayEngage = new float[3];

        public Rect SSASwindow = new Rect(10, 505, 200, 30); // gui window rect

        string newPresetName = "";

        Rect SSASPresetwindow = new Rect(550, 50, 50, 50);
        bool bShowSSASPresets = false;

        // initialisation and default presets stuff
        public double[] defaultPitchGains = { 0.1, 0.05, 0.3, -1, 1, -1, 1, 1, 200 };
        public double[] defaultRollGains = { 0.1, 0.02, 0.1, -1, 1, -1, 1, 1, 200 };
        public double[] defaultHdgGains = { 0.1, 0.05, 0.3, -1, 1, -1, 1, 1, 200 };

        // will be added back soonish
        //public Vector3 currentDirectionTarget = Vector3.zero; // this is the vec the IEnumerator is moving
        //public Vector3 newDirectionTarget = Vector3.zero; // this is the vec we are moving to

        #endregion

        public void Start()
        {
            instance = this;

            bPause.Initialize();
            ActivitySwitch(false);
            delayEngage[0] = delayEngage[1] = delayEngage[2] = 20; // delay engagement by 0.2s

            SASControllers[(int)SASList.Pitch] = new PIDErrorController(SASList.Pitch, defaultPitchGains);
            SASControllers[(int)SASList.Bank] = new PIDErrorController(SASList.Bank, defaultRollGains);
            SASControllers[(int)SASList.Hdg] = new PIDErrorController(SASList.Hdg, defaultHdgGains);

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
            if (GameSettings.MODIFIER_KEY.GetKey())
            {
                if (GameSettings.SAS_TOGGLE.GetKeyDown())
                    bArmed = !bArmed;
            }
            else
            {
                if (bArmed)
                {
                    if (GameSettings.SAS_TOGGLE.GetKeyDown())
                    {
                        ActivitySwitch(!ActivityCheck());
                        setStockSAS(false);
                        updateTarget();
                    }
                    if (GameSettings.SAS_HOLD.GetKey())
                        updateTarget();
                }
            }

            // lets target slide while key is down, effectively temporary deactivation
        }
        #endregion

        #region Fixed Update / Control

        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed || !ActivityCheck() || !FlightData.thisVessel.IsControllable)
                return;

            pauseManager(state);

            // still need 3 values to build the quaternion, even if a control system isn't active
            float hdgAngle = (float)(bActive[(int)SASList.Hdg] ? SASList.Hdg.GetSAS().SetPoint : FlightData.heading);
            float pitchAngle = (float)(bActive[(int)SASList.Pitch] ? SASList.Pitch.GetSAS().SetPoint : FlightData.pitch);
            float rollAngle = (float)(bActive[(int)SASList.Bank] ? SASList.Bank.GetSAS().SetPoint : FlightData.bank);

            Transform vesRefTrans = FlightData.thisVessel.ReferenceTransform.transform;
            Quaternion targetRot = Quaternion.LookRotation(FlightData.planetNorth, FlightData.planetUp); // reference rotation
            targetRot = Quaternion.AngleAxis(hdgAngle, targetRot * Vector3.up) * targetRot; // heading rotation
            targetRot = Quaternion.AngleAxis(pitchAngle, targetRot * -Vector3.right) * targetRot; // pitch rotation
            targetRot = Quaternion.AngleAxis(rollAngle, targetRot * Vector3.forward) * targetRot; // roll rotation
            Quaternion rotDiff = vesRefTrans.rotation.Inverse() * targetRot;            

            // pitch / yaw response ratio. Largely sourced from MJ attitude controller
            Vector3 target = rotDiff * Vector3.forward;
            float angleError = Math.Abs(Vector3.Angle(Vector3.up, target));
            Vector2 PYratio = (new Vector2(target.x, -target.z)).normalized;
            Vector2 PYError = PYratio * angleError;
            ////////////////////////////////////////////////////////////////////////////

            // roll error isn't particularly well defined past 90 degrees so we'll just not worry about it for now
            double rollError = 0;
            if (angleError < 89)
                rollError = Utils.headingClamp(Vector3.Angle(rotDiff * Vector3.right, Vector3.right) * Math.Sign(Vector3.Dot(rotDiff * Vector3.right, Vector3.forward)), 180);

            if (allowControl(SASList.Bank))
                state.roll = SASControllers[(int)SASList.Bank].ResponseF(Utils.headingClamp(rollError, 180), FlightData.thisVessel.angularVelocity.y * Mathf.Rad2Deg);
            if (allowControl(SASList.Pitch))
                state.pitch = SASControllers[(int)SASList.Pitch].ResponseF(PYError.y, FlightData.thisVessel.angularVelocity.x * Mathf.Rad2Deg);
            if (allowControl(SASList.Hdg))
                state.yaw = SASControllers[(int)SASList.Hdg].ResponseF(PYError.x, FlightData.thisVessel.angularVelocity.z * Mathf.Rad2Deg);
        }

        bool allowControl(SASList ID)
        {
            return bActive[(int)ID] && !bPause[(int)ID];
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
                    SASList.Pitch.GetSAS().SetPoint = FlightData.pitch;
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
            SASList.Bank.GetSAS().SetPoint = FlightData.bank;

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
                    SASList.Bank.GetSAS().SetPoint = FlightData.bank;
                    targets[(int)SASList.Bank] = FlightData.bank.ToString("0.00");
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
            SASList.Hdg.GetSAS().SetPoint = FlightData.heading;

            // initialse all relevant values
            timeElapsed[(int)SASList.Hdg] = 0;
            fadeCurrent[(int)SASList.Hdg] = fadeSetpoint[(int)SASList.Hdg]; // x to the power of 0 is 1

            if (yawEnum) // don't need multiple running at once
                yield break;
            yawEnum = true;

            while (fadeCurrent[(int)SASList.Hdg] > 1) // fadeCurrent only decreases after delay period finishes
            {
                yield return new WaitForFixedUpdate();
                timeElapsed[(int)SASList.Hdg] += TimeWarp.fixedDeltaTime * 100f; // 1 == 1/100th of a second
                // handle both in the same while loop so if we pause/unpause again it just resets
                if (timeElapsed[(int)SASList.Hdg] < delayEngage[(int)SASList.Hdg])
                {
                    SASList.Hdg.GetSAS().SetPoint = FlightData.heading;
                    targets[(int)SASList.Hdg] = FlightData.heading.ToString("0.00");
                }
                else
                    fadeCurrent[(int)SASList.Hdg] = Mathf.Max(fadeSetpoint[(int)SASList.Hdg] * Mathf.Pow(fadeMult, timeElapsed[(int)SASList.Hdg] - delayEngage[(int)SASList.Hdg]), 1);
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
                Utils.GetSAS(SASList.Pitch).SetPoint = TogPlusNumBox("Pitch:", SASList.Pitch, FlightData.pitch, 80, 70);
                Utils.GetSAS(SASList.Hdg).SetPoint = TogPlusNumBox("Heading:", SASList.Hdg, FlightData.heading, 80, 70);
                Utils.GetSAS(SASList.Bank).SetPoint = TogPlusNumBox("Roll:", SASList.Bank, FlightData.bank, 80, 70);

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
                }
            }

            string tempText = GUILayout.TextField(targets[(int)controllerID], GeneralUI.UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
            targets[(int)controllerID] = tempText;

            if (GUILayout.Button("u"))
            {
                double temp;
                if (double.TryParse(targets[(int)controllerID], out temp))
                    setPoint = temp;

                bActive[(int)controllerID] = true;
            }

            GUILayout.EndHorizontal();
            return setPoint;
        }
        #endregion
    }
}