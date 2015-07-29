using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    using PID;
    using PID.Presets;
    using Utility;

    public class SurfSAS
    {
        #region Globals

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            vesRef.StartCoroutine(routine);
        }
        public AsstVesselModule vesRef;
        public Attitude_Controller controller;

        public bool bArmed = false; // if armed, SAS toggles activate/deactivate SSAS
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

        VesselAutopilot.AutopilotMode currentMode = VesselAutopilot.AutopilotMode.StabilityAssist;
        FlightUIController.SpeedDisplayModes referenceMode = FlightUIController.SpeedDisplayModes.Surface;

        #endregion
        public SurfSAS(AsstVesselModule avm)
        {
            vesRef = avm;
        }

        public void Start()
        {
            PIDConstants pitch = new PIDConstants(defaultPitchGains);
            PIDConstants yaw = new PIDConstants(defaultHdgGains);
            PIDConstants roll = new PIDConstants(defaultRollGains);
            controller = new Attitude_Controller(vesRef.vesselData, pitch, yaw, roll);
            
            PresetManager.initDefaultPresets(new SSASPreset(pitch, yaw, roll, "SSAS"));
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
            
            #warning do not remove, part of Attitude controller that needs shifting
            //if (currentMode != vesRef.vesselRef.Autopilot.Mode && currentMode == VesselAutopilot.AutopilotMode.StabilityAssist)
            //    updateTarget();
            //if (referenceMode == FlightUIController.SpeedDisplayModes.Surface && FlightUIController.speedDisplayMode != FlightUIController.SpeedDisplayModes.Surface)
            //    orbitalTarget = vesRef.vesselRef.transform.rotation;
            //currentMode = vesRef.vesselRef.Autopilot.Mode;
            //referenceMode = FlightUIController.speedDisplayMode;

            controller.UpdateSrf();
        }
        #endregion

        #region Fixed Update / Control
        public void SurfaceSAS(FlightCtrlState state)
        {
            if (!bArmed || !ActivityCheck() || !vesRef.vesselRef.IsControllable)
                return;

            pauseManager();
            Vector3 orgState = new Vector3(state.pitch, state.roll, state.yaw);
            bool[] active = new bool[3] { allowControl(Attitude_Controller.Axis.Pitch), allowControl(Attitude_Controller.Axis.Roll), allowControl(Attitude_Controller.Axis.Yaw) };

            Vessel v = vesRef.vesselRef;
            controller.ResponseF(v.ReferenceTransform.transform.rotation, v.angularVelocity, active, state);
        }

       
        /// <summary>
        /// active and not paused
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        bool allowControl(Attitude_Controller.Axis ID)
        {
            return controller.GetCtrl(ID).Active && !bPause[(int)ID];
        }

        private void pauseManager()
        {
            if (Utils.isFlightControlLocked() && vesRef.vesselRef.isActiveVessel)
                return;

            // if the pitch control is not paused, and there is pitch input or there is yaw input and the bank angle is greater than 5 degrees, pause the pitch lock
            if (!bPause[(int)Attitude_Controller.Axis.Pitch] && (Utils.hasPitchInput() || (Utils.hasYawInput() && Math.Abs(vesRef.vesselData.bank) > bankAngleSynch)))
                bPause[(int)Attitude_Controller.Axis.Pitch] = true;
            // if the pitch control is paused, and there is no pitch input, and there is no yaw input or the bank angle is less than 5 degrees, unpause the pitch lock
            else if (bPause[(int)Attitude_Controller.Axis.Pitch] && !Utils.hasPitchInput() && (!Utils.hasYawInput() || Math.Abs(vesRef.vesselData.bank) <= bankAngleSynch))
            {
                bPause[(int)Attitude_Controller.Axis.Pitch] = false;
                if (controller.GetCtrl(Attitude_Controller.Axis.Pitch).Active)
                    StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Pitch));
            }

            // if the heading control is not paused, and there is yaw input input or there is pitch input and the bank angle is greater than 5 degrees, pause the heading lock
            if (!bPause[(int)Attitude_Controller.Axis.Yaw] && (Utils.hasYawInput() || (Utils.hasPitchInput() && Math.Abs(vesRef.vesselData.bank) > bankAngleSynch)))
                bPause[(int)Attitude_Controller.Axis.Yaw] = true;
            // if the heading control is paused, and there is no yaw input, and there is no pitch input or the bank angle is less than 5 degrees, unpause the heading lock
            else if (bPause[(int)Attitude_Controller.Axis.Yaw] && !Utils.hasYawInput() && (!Utils.hasPitchInput() || Math.Abs(vesRef.vesselData.bank) <= bankAngleSynch))
            {
                bPause[(int)Attitude_Controller.Axis.Yaw] = false;
                if (controller.GetCtrl(Attitude_Controller.Axis.Pitch).Active)
                    StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Yaw));
            }

            // if the roll control is not paused, and there is roll input or thevessel pitch is > 70 degrees and there is pitch/yaw input
            if (!bPause[(int)Attitude_Controller.Axis.Roll] && (Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 70 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
                bPause[(int)Attitude_Controller.Axis.Roll] = true;
            // if the roll control is paused, and there is not roll input and not any pitch/yaw input if pitch < 60 degrees
            else if (bPause[(int)Attitude_Controller.Axis.Roll] && !(Utils.hasRollInput() || (Math.Abs(vesRef.vesselData.pitch) > 60 && (Utils.hasPitchInput() || Utils.hasYawInput()))))
            {
                bPause[(int)Attitude_Controller.Axis.Roll] = false;
                if (controller.GetCtrl(Attitude_Controller.Axis.Roll).Active)
                    StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Roll));
            }
        }

        private void updateTarget()
        {
            StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Pitch));
            StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Roll));
            StartCoroutine(FadeInAxis(Attitude_Controller.Axis.Yaw));
            #warning Do not remove, part of attitude controller that needs shifting
            // orbitalTarget = vesRef.vesselRef.transform.rotation;
        }

        /// <summary>
        /// wait for rate of rotation to fall below 10 degres / s before locking in the target. Derivative only action until that time
        /// </summary>
        IEnumerator FadeInAxis(Attitude_Controller.Axis axis)
        {
            updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
            while (Math.Abs(Utils.getCurrentRate(axis, vesRef.vesselRef) * Mathf.Rad2Deg) > 10)
            {
                updateSetpoint(axis, Utils.getCurrentVal(axis, vesRef.vesselData));
                yield return null;
            }
        }

        void updateSetpoint(Attitude_Controller.Axis ID, double setpoint)
        {
            controller.Setpoint(ID, (float)setpoint);
            targets[(int)ID] = setpoint.ToString("0.00");
        }
        #endregion

        /// <summary>
        /// Set SSAS mode
        /// </summary>
        /// <param name="enable"></param>
        public void ActivitySwitch(bool enable)
        {
            controller.GetCtrl(Attitude_Controller.Axis.Pitch).Active = controller.GetCtrl(Attitude_Controller.Axis.Roll).Active = controller.GetCtrl(Attitude_Controller.Axis.Yaw).Active = enable;
            if (enable)                
                updateTarget();

            setStockSAS(enable);
        }

        /// <summary>
        /// returns true if SSAS is active
        /// </summary>
        /// <returns></returns>
        public bool ActivityCheck()
        {
            return controller.GetCtrl(Attitude_Controller.Axis.Pitch).Active || controller.GetCtrl(Attitude_Controller.Axis.Roll).Active || controller.GetCtrl(Attitude_Controller.Axis.Yaw).Active;
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
                        TogPlusNumBox("Pitch:", Attitude_Controller.Axis.Pitch, vesRef.vesselData.pitch, 80, 70);
                        TogPlusNumBox("Heading:", Attitude_Controller.Axis.Yaw, vesRef.vesselData.heading, 80, 70);
                    }
                    TogPlusNumBox("Roll:", Attitude_Controller.Axis.Roll, vesRef.vesselData.bank, 80, 70);
                }

                GUILayout.Box("", GUILayout.Height(10)); // seperator

                drawPIDValues(Attitude_Controller.Axis.Pitch, "Pitch");
                drawPIDValues(Attitude_Controller.Axis.Roll, "Roll");
                drawPIDValues(Attitude_Controller.Axis.Yaw, "Yaw");
            }

            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDValues(Attitude_Controller.Axis controllerID, string inputName)
        {
            Axis_Controller c = controller.GetCtrl(controllerID);
            c.BShow = GUILayout.Toggle(c.BShow, inputName, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (c.BShow)
            {
                c.Constants.KP = GeneralUI.labPlusNumBox(GeneralUI.KpLabel, c.Constants.KP.ToString("N3"), 45);
                c.Constants.KI = GeneralUI.labPlusNumBox(GeneralUI.KiLabel, c.Constants.KI.ToString("N3"), 45);
                c.Constants.KD = GeneralUI.labPlusNumBox(GeneralUI.KdLabel, c.Constants.KD.ToString("N3"), 45);
                c.Constants.Scalar = GeneralUI.labPlusNumBox(GeneralUI.ScalarLabel, c.Constants.Scalar.ToString("N3"), 45);
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
                newPresetName = PresetManager.newSSASPreset(newPresetName, controller, vesRef.vesselRef);
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
        public void TogPlusNumBox(string toggleText, Attitude_Controller.Axis controllerID, double currentVal, float toggleWidth, float boxWidth)
        {
            GUILayout.BeginHorizontal();

            Axis_Controller c = controller.GetCtrl(controllerID);
            bool tempState = GUILayout.Toggle(c.Active, toggleText, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(toggleWidth));
            if (tempState != c.Active)
            {
                c.Active = tempState;
                if (tempState)
                {
                    controller.Setpoint(controllerID, (float)currentVal);
                    targets[(int)controllerID] = currentVal.ToString("0.00");
                }
            }

            string tempText = GUILayout.TextField(targets[(int)controllerID], GeneralUI.UISkin.customStyles[(int)myStyles.numBoxText], GUILayout.Width(boxWidth));
            targets[(int)controllerID] = tempText;

            if (GUILayout.Button("u"))
            {
                double temp;
                if (double.TryParse(targets[(int)controllerID], out temp))
                    controller.Setpoint(controllerID, (float)temp);

                c.Active = true;
            }

            GUILayout.EndHorizontal();
        }
        #endregion
    }
}