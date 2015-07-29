using System;
using System.Collections;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    using PID;
    using Utility;
    using PID.Presets;

    /// <summary>
    /// provides a UI for tuning, and a save/load mechanism for the stock SAS systems. Just windows and a few calls to PresetManager
    /// </summary>
    public class Stock_SAS
    {
        public AsstVesselModule vesRef;

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            vesRef.StartCoroutine(routine);
        }

        static public Rect StockSASwindow = new Rect(10, 505, 200, 30); // gui window rect
        static Rect SASPresetwindow = new Rect(550, 50, 50, 50);
        static bool[] stockPIDDisplay = { false, false, false }; // which stock PID axes are visible
        static bool bShowPresets = false;
        string newPresetName = "";

        public Stock_SAS(AsstVesselModule avm)
        {
            vesRef = avm;
        }

        public void Start()
        {
            StartCoroutine(Initialise());
        }

        IEnumerator Initialise()
        {
            while (vesRef.vesselRef.Autopilot.SAS.pidLockedPitch == null || vesRef.vesselRef.Autopilot.RSAS.pidPitch == null)
                yield return null;

            PresetManager.initDefaultPresets(new SASPreset(vesRef.vesselRef.Autopilot.SAS, "stock"));
            PresetManager.initDefaultPresets(new RSASPreset(vesRef.vesselRef.Autopilot.RSAS, "RSAS"));

            PresetManager.loadCraftSASPreset(this);
        }

        public void vesselSwitch()
        {
            StartCoroutine(Initialise());
        }

        public void drawGUI()
        {
            if (PilotAssistantFlightCore.bDisplaySAS)
            {
                if (vesRef.vesselRef.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist)
                {
                    StockSASwindow = GUILayout.Window(78934857, StockSASwindow, drawSASWindow, "Stock SAS", GUILayout.Height(0));

                    if (bShowPresets)
                    {
                        SASPresetwindow = GUILayout.Window(78934858, SASPresetwindow, drawSASPresetWindow, "SAS Presets", GUILayout.Height(0));
                        SASPresetwindow.x = StockSASwindow.x + StockSASwindow.width;
                        SASPresetwindow.y = StockSASwindow.y;
                    }
                }
                else
                {

                    StockSASwindow = GUILayout.Window(78934857, StockSASwindow, drawRSASWindow, "Stock SAS", GUILayout.Height(0));

                    if (bShowPresets)
                    {
                        SASPresetwindow = GUILayout.Window(78934858, SASPresetwindow, drawRSASPresetWindow, "SAS Presets", GUILayout.Height(0));
                        SASPresetwindow.x = StockSASwindow.x + StockSASwindow.width;
                        SASPresetwindow.y = StockSASwindow.y;
                    }
                }

                if (tooltip != "" && PilotAssistantFlightCore.showTooltips)
                    GUILayout.Window(34246, new Rect(StockSASwindow.x + StockSASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));
            }
        }

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }
        
        #region SAS Windows
        private void drawSASWindow(int id)
        {
            if (GUI.Button(new Rect(StockSASwindow.width - 16, 2, 14, 14), ""))
                PilotAssistantFlightCore.bDisplaySAS = false;

            bShowPresets = GUILayout.Toggle(bShowPresets, bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            VesselAutopilot.VesselSAS sas = vesRef.vesselRef.Autopilot.SAS;

            drawPIDValues(sas.pidLockedPitch, "Pitch", Attitude_Controller.Axis.Pitch);
            drawPIDValues(sas.pidLockedRoll, "Roll", Attitude_Controller.Axis.Roll);
            drawPIDValues(sas.pidLockedYaw, "Yaw", Attitude_Controller.Axis.Yaw);

            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        private void drawSASPresetWindow(int id)
        {
            if (GUI.Button(new Rect(SASPresetwindow.width - 16, 2, 14, 14), ""))
                bShowPresets = false;

            if (PresetManager.Instance.activeSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeSASPreset.name));
                if (PresetManager.Instance.activeSASPreset.name != "stock")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.UpdateSASPreset(this);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newSASPreset(ref newPresetName, this.vesRef.vesselRef);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadSASPreset(PresetManager.Instance.craftPresetDict["default"].SASPreset, this);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SASPreset p in PresetManager.Instance.SASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSASPreset(p, this);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion

        #region RSAS windows
        private void drawRSASWindow(int id)
        {
            if (GUI.Button(new Rect(StockSASwindow.width - 16, 2, 14, 14), ""))
                PilotAssistantFlightCore.bDisplaySAS = false;

            bShowPresets = GUILayout.Toggle(bShowPresets, bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            VesselAutopilot.VesselRSAS rsas = vesRef.vesselRef.Autopilot.RSAS;
            drawPIDValues(rsas.pidPitch, "Pitch", Attitude_Controller.Axis.Pitch);
            drawPIDValues(rsas.pidRoll, "Roll", Attitude_Controller.Axis.Roll);
            drawPIDValues(rsas.pidYaw, "Yaw", Attitude_Controller.Axis.Yaw);

            GUI.DragWindow();
            tooltip = GUI.tooltip;
        }

        private void drawRSASPresetWindow(int id)
        {
            if (GUI.Button(new Rect(SASPresetwindow.width - 16, 2, 14, 14), ""))
                bShowPresets = false;

            if (PresetManager.Instance.activeRSASPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeRSASPreset.name));
                if (PresetManager.Instance.activeRSASPreset.name != "stock")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.UpdateRSASPreset(this);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newRSASPreset(ref newPresetName, this.vesRef.vesselRef);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadRSASPreset(PresetManager.Instance.craftPresetDict["default"].RSASPreset, this);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (RSASPreset p in PresetManager.Instance.RSASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadRSASPreset(p, this);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteRSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion

        private void drawPIDValues(PIDclamp controller, string inputName, Attitude_Controller.Axis controllerID)
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

        private void drawPIDValues(PIDRclamp controller, string inputName, Attitude_Controller.Axis controllerID)
        {
            stockPIDDisplay[(int)controllerID] = GUILayout.Toggle(stockPIDDisplay[(int)controllerID], inputName, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);

            if (stockPIDDisplay[(int)controllerID])
            {
                float kp, ki, kd;
                kp = (float)GeneralUI.labPlusNumBox(GeneralUI.KpLabel, controller.KP.ToString(), 45);
                ki = (float)GeneralUI.labPlusNumBox(GeneralUI.KiLabel, controller.KI.ToString(), 45);
                kd = (float)GeneralUI.labPlusNumBox(GeneralUI.KdLabel, controller.KD.ToString(), 45);

                if (kp != controller.KP || ki != controller.KI || kd != controller.KD)
                    controller.ReinitializePIDsOnly(kp, ki, kd);
            }
        }
    }
}
