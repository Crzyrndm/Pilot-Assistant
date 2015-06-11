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

        double bankAngleSynch = 5; // the bank angle below which pitch and yaw unlock seperately

        public Rect SSASwindow = new Rect(10, 505, 200, 30); // gui window rect
        string newPresetName = "";
        Rect SSASPresetwindow = new Rect(550, 50, 50, 50);
        bool bShowSSASPresets = false;

        // initialisation and default presets stuff
        // kp, ki, kd, outMin, outMax, iMin, iMax, scalar, easing (unused)
        public double[] defaultPitchGains = { 0.1, 0.01, 0.3, -1, 1, -1, 1, 1, 200 };
        public double[] defaultRollGains = { 0.1, 0.01, 0.1, -1, 1, -1, 1, 1, 200 };
        public double[] defaultHdgGains = { 0.1, 0.01, 0.3, -1, 1, -1, 1, 1, 200 };

        // will be added back soonish
        //public Vector3 currentDirectionTarget = Vector3.zero; // this is the vec the IEnumerator is moving
        //public Vector3 newDirectionTarget = Vector3.zero; // this is the vec we are moving to

        #endregion

        public void Start()
        {
            instance = this;

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
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(SurfaceSAS);
            instance = null;
        }

        #region Update / Input monitoring
        VesselAutopilot.AutopilotMode currentMode = VesselAutopilot.AutopilotMode.StabilityAssist;
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
                    }
                    if (GameSettings.SAS_HOLD.GetKey())
                        updateTarget();
                }
            }
            if (currentMode != FlightData.thisVessel.Autopilot.Mode)
            {
                currentMode = FlightData.thisVessel.Autopilot.Mode;
                if (currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                    updateTarget();
            }
        }
        #endregion

        #region Fixed Update / Control
        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed || !ActivityCheck() || !FlightData.thisVessel.IsControllable)
                return;

            pauseManager();
            Transform vesRefTrans = FlightData.thisVessel.ReferenceTransform.transform;

            Quaternion targetRot = TargetModeSwitch();
            Quaternion rotDiff = vesRefTrans.rotation.Inverse() * targetRot;

            //================================
            // pitch / yaw response ratio. Original method from MJ attitude controller
            Vector3d target = rotDiff * Vector3d.forward;
            double angleError = Math.Abs(Vector3d.Angle(Vector3d.up, target));
            Vector2d PYerror = (new Vector2d(target.x, -target.z)).normalized * angleError;
            //================================

            //================================
            // facing vectors for vessel (vesRefTrans.up) and target (targetRot * Vector3.forward)
            // normVec = axis normal to desired plane of travel
            Vector3d normVec = Vector3d.Cross(targetRot * Vector3d.forward, vesRefTrans.up).normalized;
            // rotation with Pitch and Yaw elements removed (facing aligned)
            Quaternion rollTargetRot = Quaternion.AngleAxis((float)angleError, normVec) * targetRot;
            // signed angle difference between vessel.right and rollTargetRot.right
            double rollError = Utils.headingClamp(Vector3d.Angle(vesRefTrans.right, rollTargetRot * Vector3d.right) * Math.Sign(Vector3d.Dot(rollTargetRot * Vector3d.right, vesRefTrans.forward)), 180);
            //================================

            setCtrlState(SASList.Bank, rollError, FlightData.thisVessel.angularVelocity.y * Mathf.Rad2Deg, ref state.roll);
            setCtrlState(SASList.Pitch, PYerror.y, FlightData.thisVessel.angularVelocity.x * Mathf.Rad2Deg, ref state.pitch);
            setCtrlState(SASList.Hdg, PYerror.x, FlightData.thisVessel.angularVelocity.z * Mathf.Rad2Deg, ref state.yaw);
        }

        void setCtrlState(SASList ID, double error, double rate, ref float ctrlState)
        {
            if (allowControl(ID))
                ctrlState = ID.GetSAS().ResponseF(error, rate);
            else if (!Utils.hasInput(ID))
                ctrlState = 0; // kill off stock SAS inputs
        }

        Quaternion TargetModeSwitch()
        {
            Quaternion target = Quaternion.identity;
            switch(FlightData.thisVessel.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    float hdgAngle = (float)(bActive[(int)SASList.Hdg] ? SASList.Hdg.GetSAS().SetPoint : FlightData.heading);
                    float pitchAngle = (float)(bActive[(int)SASList.Pitch] ? SASList.Pitch.GetSAS().SetPoint : FlightData.pitch);
                    
                    target = Quaternion.LookRotation(FlightData.planetNorth, FlightData.planetUp);
                    target = Quaternion.AngleAxis(hdgAngle, target * Vector3.up) * target; // heading rotation
                    target = Quaternion.AngleAxis(pitchAngle, target * -Vector3.right) * target; // pitch rotation
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(FlightData.thisVessel.obt_velocity, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(FlightData.thisVessel.srf_velocity, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(FlightData.thisVessel.obt_velocity - FlightData.thisVessel.targetObject.GetVessel().obt_velocity, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(-FlightData.thisVessel.obt_velocity, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(FlightData.thisVessel.srf_velocity, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(FlightData.thisVessel.targetObject.GetVessel().obt_velocity - FlightData.thisVessel.obt_velocity, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(FlightData.obtRadial, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(FlightData.srfRadial, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-FlightData.obtRadial, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-FlightData.srfRadial, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(FlightData.obtNormal, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(FlightData.srfNormal, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-FlightData.obtNormal, FlightData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-FlightData.srfNormal, FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    if (FlightData.thisVessel.targetObject != null)
                        target = Quaternion.LookRotation(FlightData.thisVessel.targetObject.GetVessel().GetWorldPos3D() - FlightData.thisVessel.GetWorldPos3D(), FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    if (FlightData.thisVessel.targetObject != null)
                        target = Quaternion.LookRotation(FlightData.thisVessel.GetWorldPos3D() - FlightData.thisVessel.targetObject.GetVessel().GetWorldPos3D(), FlightData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    if (FlightData.thisVessel.patchedConicSolver.maneuverNodes != null && FlightData.thisVessel.patchedConicSolver.maneuverNodes.Count > 0)
                        target = FlightData.thisVessel.patchedConicSolver.maneuverNodes[0].nodeRotation;
                    break;
            }
            float rollAngle = (float)(bActive[(int)SASList.Bank] ? SASList.Bank.GetSAS().SetPoint : FlightData.bank);
            target = Quaternion.AngleAxis(-rollAngle, target * Vector3.forward) * target; // roll rotation
            return target;
        }

        bool allowControl(SASList ID)
        {
            return bActive[(int)ID] && !bPause[(int)ID];
        }

        private void pauseManager()
        {
            if (Utils.isFlightControlLocked())
                return;

            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)SASList.Pitch] && (Utils.hasPitchInput() || (Utils.hasYawInput() && Math.Abs(FlightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Pitch] = true;
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Pitch] && !Utils.hasPitchInput() && (!Utils.hasYawInput() || Math.Abs(FlightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                    StartCoroutine(FadeInAxis(SASList.Pitch));
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)SASList.Hdg] && (Utils.hasYawInput() || (Utils.hasPitchInput() && Math.Abs(FlightData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Hdg] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the heading lock
            else if (bPause[(int)SASList.Hdg] && !Utils.hasYawInput() && (!Utils.hasPitchInput() || Math.Abs(FlightData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Hdg] = false;
                if (bActive[(int)SASList.Hdg])
                    StartCoroutine(FadeInAxis(SASList.Hdg));
            }

            if (!bPause[(int)SASList.Bank] && Utils.hasRollInput())
                bPause[(int)SASList.Bank] = true;
            else if (bPause[(int)SASList.Bank] && !Utils.hasRollInput())
            {
                bPause[(int)SASList.Bank] = false;
                if (bActive[(int)SASList.Bank])
                    StartCoroutine(FadeInAxis(SASList.Bank));
            }
        }

        private void updateTarget()
        {
            StartCoroutine(FadeInAxis(SASList.Pitch));
            StartCoroutine(FadeInAxis(SASList.Bank));
            StartCoroutine(FadeInAxis(SASList.Hdg));
        }

        IEnumerator FadeInAxis(SASList axis)
        {
            updateSetpoint(axis, Utils.getCurrentVal(axis));
            while (Math.Abs(Utils.getCurrentRate(axis) * Mathf.Rad2Deg) > 10)
            {
                updateSetpoint(axis, Utils.getCurrentVal(axis));
                yield return null;
            }
        }

        void updateSetpoint(SASList ID, double setpoint)
        {
            ID.GetSAS().SetPoint = setpoint;
            targets[(int)ID] = setpoint.ToString("0.00");
        }
        #endregion

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        public static void ActivitySwitch(bool enable)
        {
            if (enable)
            {
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Bank] = instance.bActive[(int)SASList.Hdg] = true;
                instance.updateTarget();
            }
            else
                instance.bActive[(int)SASList.Pitch] = instance.bActive[(int)SASList.Bank] = instance.bActive[(int)SASList.Hdg] = false;
            setStockSAS(enable);
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
        }

        #region GUI
        public void drawGUI()
        {
            // SAS toggle button
            // is before the bDisplay check so it can be up without the GUI
            if (bArmed && FlightUIModeController.Instance.navBall.expanded)
            {
                if (SurfSAS.ActivityCheck())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    ActivitySwitch(!ActivityCheck());
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
                if (tooltip != "" && PilotAssistantFlightCore.showTooltips)
                    GUILayout.Window(34246, new Rect(SSASwindow.x + SSASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));
            }
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
                ActivitySwitch(FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS]);

                GeneralUI.postMessage(bArmed ? "SSAS Armed" : "SSAS Disarmed");
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            if (bArmed)
            {
                if (currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                {
                    Utils.GetSAS(SASList.Pitch).SetPoint = TogPlusNumBox("Pitch:", SASList.Pitch, FlightData.pitch, 80, 70);
                    Utils.GetSAS(SASList.Hdg).SetPoint = TogPlusNumBox("Heading:", SASList.Hdg, FlightData.heading, 80, 70);
                }
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