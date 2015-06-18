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
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }
        public AsstVesselModule vesRef;
        Vessel controlledVessel;
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
        public readonly static double[] defaultRollGains = { 0.25, 0.1, 5, -1, 1, -1, 1, 1, 200 };
        public readonly static double[] defaultHdgGains = { 0.22, 0.12, 0.3, -1, 1, -1, 1, 1, 200 };

        // will be added back soonish
        //public Vector3 currentDirectionTarget = Vector3.zero; // this is the vec the IEnumerator is moving
        //public Vector3 newDirectionTarget = Vector3.zero; // this is the vec we are moving to

        VesselAutopilot.AutopilotMode currentMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        FlightUIController.SpeedDisplayModes referenceMode = FlightUIController.SpeedDisplayModes.Surface;

        #endregion

        public void Start(AsstVesselModule avm)
        {
            vesRef = avm;
            controlledVessel = avm.vesselRef;

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

        //public void OnDestroy()
        //{
        //}

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
                    }
                    if (GameSettings.SAS_HOLD.GetKey())
                        updateTarget();
                }
            }
            if (currentMode != controlledVessel.Autopilot.Mode)
            {
                currentMode = controlledVessel.Autopilot.Mode;
                if (currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
                    updateTarget();
            }
            if (referenceMode == FlightUIController.SpeedDisplayModes.Surface && FlightUIController.speedDisplayMode != FlightUIController.SpeedDisplayModes.Surface)
            {
                orbitalTarget = controlledVessel.transform.rotation;
            }
            referenceMode = FlightUIController.speedDisplayMode;
        }
        #endregion

        #region Fixed Update / Control
        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed || !ActivityCheck() || !controlledVessel.IsControllable)
                return;

            pauseManager();
            Transform vesRefTrans = controlledVessel.ReferenceTransform.transform;

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

            setCtrlState(SASList.Bank, rollError, controlledVessel.angularVelocity.y, ref state.roll);
            setCtrlState(SASList.Pitch, PYerror.y, controlledVessel.angularVelocity.x * Mathf.Rad2Deg, ref state.pitch);
            setCtrlState(SASList.Hdg, PYerror.x, controlledVessel.angularVelocity.z * Mathf.Rad2Deg, ref state.yaw);
        }

        void setCtrlState(SASList ID, double error, double rate, ref float ctrlState)
        {
            PIDmode mode = PIDmode.PID;
            if (!controlledVessel.checkLanded() && controlledVessel.IsControllable)
                mode = PIDmode.PD; // no integral when it can't do anything

            if (allowControl(ID))
                ctrlState = ID.GetSAS(this).ResponseF(error, rate, mode);
            else if (!Utils.hasInput(ID))
                ctrlState = 0; // kill off stock SAS inputs
        }

        Quaternion orbitalTarget = Quaternion.identity;
        Quaternion TargetModeSwitch()
        {
            Quaternion target = Quaternion.identity;
            switch(controlledVessel.Autopilot.Mode)
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
                        target = Quaternion.LookRotation(controlledVessel.obt_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(controlledVessel.srf_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(controlledVessel.obt_velocity - controlledVessel.targetObject.GetVessel().obt_velocity, vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(-controlledVessel.obt_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(controlledVessel.srf_velocity, vesRef.vesselData.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(controlledVessel.targetObject.GetVessel().obt_velocity - controlledVessel.obt_velocity, vesRef.vesselData.planetUp);
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
                    if (controlledVessel.targetObject != null)
                        target = Quaternion.LookRotation(controlledVessel.targetObject.GetVessel().GetWorldPos3D() - controlledVessel.GetWorldPos3D(), vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    if (controlledVessel.targetObject != null)
                        target = Quaternion.LookRotation(controlledVessel.GetWorldPos3D() - controlledVessel.targetObject.GetVessel().GetWorldPos3D(), vesRef.vesselData.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    if (controlledVessel.patchedConicSolver.maneuverNodes != null && controlledVessel.patchedConicSolver.maneuverNodes.Count > 0)
                        target = controlledVessel.patchedConicSolver.maneuverNodes[0].nodeRotation;
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
            if (Utils.isFlightControlLocked() && controlledVessel.isActiveVessel)
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

            if (!bPause[(int)SASList.Bank] && (Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 70 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
                bPause[(int)SASList.Bank] = true;
            else if (bPause[(int)SASList.Bank] && !(Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 70 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
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
            orbitalTarget = controlledVessel.transform.rotation;
        }

        IEnumerator FadeInAxis(SASList axis)
        {
            updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
            while (Math.Abs(Utils.getCurrentRate(axis, controlledVessel) * Mathf.Rad2Deg) > 10)
            {
                updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
                yield return null;
            }
            orbitalTarget = controlledVessel.transform.rotation;
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
        /// <param name="state"></param>
        public void setStockSAS(bool state)
        {
            controlledVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, state);
        }

        #region GUI
        public void drawGUI()
        {
            // SAS toggle button
            // is before the bDisplay check so it can be up without the GUI
            if (bArmed && FlightUIModeController.Instance.navBall.expanded)
            {
                if (ActivityCheck())
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
                ActivitySwitch(controlledVessel.ActionGroups[KSPActionGroup.SAS]);

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
                        SASList.Hdg.GetSAS(this).SetPoint = TogPlusNumBox("Heading:", SASList.Hdg, vesRef.vesselData.heading, 80, 70);
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
                PresetManager.newSSASPreset(ref newPresetName, SASControllers, controlledVessel);
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