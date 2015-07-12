using System;
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

    public class SurfSAS
    {
        #region Globals

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            vesRef.StartCoroutine(routine);
        }
        public AsstVesselModule vesRef;
        public PIDErrorController[] SASControllers = new PIDErrorController[3]; // controller per axis

        public bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
        public bool[] bActive = new bool[3]; // activate on per axis basis
        public bool[] bPause = new bool[3]; // pause on a per axis basis

        public string[] targets = { "0.00", "0.00", "0.00" };

        readonly double bankAngleSynch = 5; // the bank angle below which pitch and yaw unlock seperately

        public static Rect SSASwindow = new Rect(10, 505, 100, 30); // gui window rect
        string newPresetName = "";
        static Rect SSASPresetwindow = new Rect(550, 50, 50, 50);
        static bool bShowSSASPresets = false;

        // initialisation and default presets stuff
        // kp, ki, kd, outMin, outMax, iMin, iMax, scalar, easing (unused)
        public readonly static double[] defaultPitchGains = { 0.22, 0.12, 0.3, -1, 1, -1, 1, 1, 200 };
        public readonly static double[] defaultRollGains = { 0.25, 0.1, 0.09, -1, 1, -1, 1, 1, 200 };
        public readonly static double[] defaultHdgGains = { 0.22, 0.12, 0.3, -1, 1, -1, 1, 1, 200 };

        public Quaternion currentTarget = Quaternion.identity;

        VesselAutopilot.AutopilotMode currentMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        FlightUIController.SpeedDisplayModes referenceMode = FlightUIController.SpeedDisplayModes.Surface;

        #endregion
        public SurfSAS(AsstVesselModule avm)
        {
            vesRef = avm;
        }

        public void Start()
        {
            SASControllers[(int)SASList.Pitch] = new PIDErrorController(SASList.Pitch, defaultPitchGains);
            SASControllers[(int)SASList.Bank] = new PIDErrorController(SASList.Bank, defaultRollGains);
            SASControllers[(int)SASList.Hdg] = new PIDErrorController(SASList.Hdg, defaultHdgGains);
            
            PresetManager.initDefaultPresets(new SSASPreset(SASControllers, "SSAS"));
            PresetManager.loadCraftSSASPreset(this);
            
            tooltip = "";
        }

        public void warpHandler()
        {
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                updateTarget();
        }

        #region Update / Input monitoring
        public void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && GameSettings.SAS_TOGGLE.GetKeyDown())
                bArmed = !bArmed;
            if (bArmed)
            {
                if (GameSettings.SAS_TOGGLE.GetKeyDown())
                    ActivitySwitch(!ActivityCheck());
                if (GameSettings.SAS_HOLD.GetKey())
                    updateTarget();
            }
            if (currentMode != vesRef.vesselRef.Autopilot.Mode && currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                updateTarget();
            if (referenceMode == FlightUIController.SpeedDisplayModes.Surface && FlightUIController.speedDisplayMode != FlightUIController.SpeedDisplayModes.Surface)
                orbitalTarget = vesRef.vesselRef.transform.rotation;
            currentMode = vesRef.vesselRef.Autopilot.Mode;
            referenceMode = FlightUIController.speedDisplayMode;

            if (bActive[(int)SASList.Hdg])
                SASList.Hdg.GetSAS(this).SetPoint = Utils.calculateTargetHeading(currentTarget, vesRef);
        }
        #endregion

        #region Fixed Update / Control
        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed || !ActivityCheck() || !vesRef.vesselRef.IsControllable)
                return;

            pauseManager();
            // facing vectors : vessel (vesRefTrans.up) and target (targetRot * Vector3.forward)
            Transform vesRefTrans = vesRef.vesselRef.ReferenceTransform.transform;
            Quaternion targetRot = TargetModeSwitch();
            double angleError = Vector3d.Angle(vesRefTrans.up, targetRot * Vector3d.forward);
            //================================
            // pitch / yaw response ratio. Original method from MJ attitude controller
            Vector3d relativeTargetFacing = vesRefTrans.rotation.Inverse() * targetRot * Vector3d.forward;
            Vector2d PYerror = (new Vector2d(relativeTargetFacing.x, -relativeTargetFacing.z)).normalized * angleError;
            //================================
            // roll error is dependant on path taken in pitch/yaw plane. Minimise unnecesary rotation by evaluating the roll error relative to that path
            Vector3d normVec = Vector3d.Cross(targetRot * Vector3d.forward, vesRefTrans.up).normalized; // axis normal to desired plane of travel
            //Quaternion rollTargetRot = Quaternion.AngleAxis((float)angleError, normVec) * targetRot; // rotation with facing aligned. Direction is taken care of by the orientation of the normVec
            Vector3d rollTargetRight = Quaternion.AngleAxis((float)angleError, normVec) * targetRot * Vector3d.right;
            double rollError = Vector3d.Angle(vesRefTrans.right, rollTargetRight) * Math.Sign(Vector3d.Dot(rollTargetRight, vesRefTrans.forward)); // signed angle difference between vessel.right and rollTargetRot.right
            //================================

            setCtrlState(SASList.Bank, rollError, vesRef.vesselRef.angularVelocity.y * Mathf.Rad2Deg, ref state.roll);
            setCtrlState(SASList.Pitch, PYerror.y, vesRef.vesselRef.angularVelocity.x * Mathf.Rad2Deg, ref state.pitch);
            setCtrlState(SASList.Hdg, PYerror.x, vesRef.vesselRef.angularVelocity.z * Mathf.Rad2Deg, ref state.yaw);
        }

        void setCtrlState(SASList ID, double error, double rate, ref float axisCtrlState)
        {
            PIDmode mode = PIDmode.PID;
            if (!vesRef.vesselRef.checkLanded() && vesRef.vesselRef.IsControllable)
                mode = PIDmode.PD; // no integral when it can't do anything useful

            if (allowControl(ID))
                axisCtrlState = ID.GetSAS(this).ResponseF(error, rate, mode);
            else if (!Utils.hasInput(ID))
                axisCtrlState = 0; // kill off stock SAS inputs
            // nothing happens if player input is present
        }

        Quaternion orbitalTarget = Quaternion.identity;
        Quaternion TargetModeSwitch()
        {
            Quaternion target = Quaternion.identity;
            switch (vesRef.vesselRef.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                    {
                        float hdgAngle = (float)(bActive[(int)SASList.Hdg] ? SASList.Hdg.GetSAS(this).SetPoint : vesRef.vesselData.heading);
                        float pitchAngle = (float)(bActive[(int)SASList.Pitch] ? SASList.Pitch.GetSAS(this).SetPoint : vesRef.vesselData.pitch);

                        target = Quaternion.LookRotation(vesRef.vesselData.planetNorth, vesRef.vesselData.planetUp);
                        target = Quaternion.AngleAxis(hdgAngle, target * Vector3.up) * target; // heading rotation
                        target = Quaternion.AngleAxis(pitchAngle, target * -Vector3.right) * target; // pitch rotation
                    }
                    else
                        return orbitalTarget * Quaternion.Euler(-90, 0, 0);
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(vesRef.vesselRef.obt_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(vesRef.vesselRef.srf_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(vesRef.vesselRef.obt_velocity - vesRef.vesselRef.targetObject.GetVessel().obt_velocity, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(-vesRef.vesselRef.obt_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(vesRef.vesselRef.srf_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(vesRef.vesselRef.targetObject.GetVessel().obt_velocity - vesRef.vesselRef.obt_velocity, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(vesRef.vesselData.obtRadial, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(vesRef.vesselData.srfRadial, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-vesRef.vesselData.obtRadial, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-vesRef.vesselData.srfRadial, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(vesRef.vesselData.obtNormal, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(vesRef.vesselData.srfNormal, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-vesRef.vesselData.obtNormal, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-vesRef.vesselData.srfNormal, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    if (vesRef.vesselRef.targetObject != null)
                        target = Quaternion.LookRotation(vesRef.vesselRef.targetObject.GetVessel().GetWorldPos3D() - vesRef.vesselRef.GetWorldPos3D(), vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    if (vesRef.vesselRef.targetObject != null)
                        target = Quaternion.LookRotation(vesRef.vesselRef.GetWorldPos3D() - vesRef.vesselRef.targetObject.GetVessel().GetWorldPos3D(), vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    if (vesRef.vesselRef.patchedConicSolver.maneuverNodes != null && vesRef.vesselRef.patchedConicSolver.maneuverNodes.Count > 0)
                        target = vesRef.vesselRef.patchedConicSolver.maneuverNodes[0].nodeRotation;
                    break;
            }
            float rollAngle = (float)(bActive[(int)SASList.Bank] ? SASList.Bank.GetSAS(this).SetPoint : vesRef.vesselData.bank);
            target = Quaternion.AngleAxis(-rollAngle, target * Vector3.forward) * target; // roll rotation
            return target;
        }

        bool allowControl(SASList ID)
        {
            return bActive[(int)ID] && !bPause[(int)ID];
        }

        private void pauseManager()
        {
            if (Utils.isFlightControlLocked() && vesRef.vesselRef.isActiveVessel)
                return;

            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)SASList.Pitch] && (Utils.hasPitchInput() || (Utils.hasYawInput() && Math.Abs(vesRef.vesselData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Pitch] = true;
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)SASList.Pitch] && !Utils.hasPitchInput() && (!Utils.hasYawInput() || Math.Abs(vesRef.vesselData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Pitch] = false;
                if (bActive[(int)SASList.Pitch])
                    StartCoroutine(FadeInAxis(SASList.Pitch));
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)SASList.Hdg] && (Utils.hasYawInput() || (Utils.hasPitchInput() && Math.Abs(vesRef.vesselData.bank) > bankAngleSynch)))
                bPause[(int)SASList.Hdg] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the heading lock
            else if (bPause[(int)SASList.Hdg] && !Utils.hasYawInput() && (!Utils.hasPitchInput() || Math.Abs(vesRef.vesselData.bank) <= bankAngleSynch))
            {
                bPause[(int)SASList.Hdg] = false;
                if (bActive[(int)SASList.Hdg])
                    StartCoroutine(FadeInAxis(SASList.Hdg));
            }

            // if the roll control is not paused, and there is roll input or thevessel pitch is > 70 degrees and there is pitch/yaw input
            if (!bPause[(int)SASList.Bank] && (Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 70 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
                bPause[(int)SASList.Bank] = true;
            // if the roll control is paused, and there is not roll input and not any pitch/yaw input if pitch < 60 degrees
            else if (bPause[(int)SASList.Bank] && !(Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 60 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
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
            orbitalTarget = vesRef.vesselRef.transform.rotation;
        }

        /// <summary>
        /// wait for rate of rotation to fall below 10 degres / s before locking in the target. Derivative only action until that time
        /// </summary>
        IEnumerator FadeInAxis(SASList axis)
        {
            updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
            while (Math.Abs(Utils.getCurrentRate(axis, vesRef.vesselRef) * Mathf.Rad2Deg) > 10)
            {
                updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
                yield return null;
            }
            orbitalTarget = vesRef.vesselRef.transform.rotation;
            if (axis == SASList.Hdg)
                currentTarget = Utils.getPlaneRotation(vesRef.vesselData.heading, vesRef);
        }

        void updateSetpoint(SASList ID, double setpoint)
        {
            ID.GetSAS(this).SetPoint = setpoint;
            targets[(int)ID] = setpoint.ToString("0.00");
        }
        #endregion

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        public void ActivitySwitch(bool enable)
        {
            if (enable)
            {
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Bank] = bActive[(int)SASList.Hdg] = true;
                updateTarget();
            }
            else
                bActive[(int)SASList.Pitch] = bActive[(int)SASList.Bank] = bActive[(int)SASList.Hdg] = false;
            setStockSAS(enable);
        }

        /// <summary>
        /// returns true if SSAS is active
        /// </summary>
        /// <returns></returns>
        public bool ActivityCheck()
        {
            if (bActive[(int)SASList.Pitch] || bActive[(int)SASList.Bank] || bActive[(int)SASList.Hdg])
                return true;
            else
                return false;
        }

        /// <summary>
        /// set stock SAS state
        /// </summary>
        public void setStockSAS(bool state)
        {
            vesRef.vesselRef.ActionGroups.SetGroup(KSPActionGroup.SAS, state);
        }

        #region GUI
        public void drawGUI()
        {
            // SAS toggle button
            // is before the bDisplay check so it can be up without the rest of the UI
            if (bArmed && FlightUIModeController.Instance.navBall.expanded)
            {
                if (ActivityCheck())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                    ActivitySwitch(!ActivityCheck());
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
                ActivitySwitch(vesRef.vesselRef.ActionGroups[KSPActionGroup.SAS]);

                GeneralUI.postMessage(bArmed ? "SSAS Armed" : "SSAS Disarmed");
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            if (bArmed)
            {
                if (!(FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit && currentMode == VesselAutopilot.AutopilotMode.StabilityAssist))
                {
                    if (currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                    {
                        SASList.Pitch.GetSAS(this).SetPoint = TogPlusNumBox("Pitch:", SASList.Pitch, vesRef.vesselData.pitch, 80, 70);
                        currentTarget = Utils.getPlaneRotation(TogPlusNumBox("Heading:", SASList.Hdg, vesRef.vesselData.heading, 80, 70), vesRef);
                    }
                    SASList.Bank.GetSAS(this).SetPoint = TogPlusNumBox("Roll:", SASList.Bank, vesRef.vesselData.bank, 80, 70);
                }

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
            SASController controller = controllerID.GetSAS(this);
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
                        PresetManager.UpdateSSASPreset(this);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSSASPreset(ref newPresetName, SASControllers, vesRef.vesselRef);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadSSASPreset(PresetManager.Instance.craftPresetDict["default"].SSASPreset, this);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SSASPreset p in PresetManager.Instance.SSASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSSASPreset(p, this);
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
            double setPoint = controllerID.GetSAS(this).SetPoint;

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