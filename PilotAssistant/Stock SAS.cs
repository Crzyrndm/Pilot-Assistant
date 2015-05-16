using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using Presets;

    class Stock_SAS
    {
        private static Stock_SAS instance;
        public static Stock_SAS Instance
        {
            get
            {
                if (instance == null)
                    instance = new Stock_SAS();
                return instance;
            }
        }

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        public Rect StockSASwindow = new Rect(10, 505, 200, 30); // gui window rect
        Rect SASPresetwindow = new Rect(550, 50, 50, 50);
        bool[] stockPIDDisplay = { true, false, false }; // which stock PID axes are visible
        bool bShowPresets = false;
        string newPresetName = "";

        public void Start()
        {
            instance = this;
            StartCoroutine(Initialise());
        }

        IEnumerator Initialise()
        {
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;
            
            while (FlightData.thisVessel.Autopilot.SAS.pidLockedPitch == null)
                yield return null;

            PresetManager.initDefaultPresets(new SASPreset(FlightData.thisVessel.Autopilot.SAS, "stock"));
            PresetManager.initDefaultPresets(new RSASPreset(FlightData.thisVessel.Autopilot.RSAS, "RSAS"));
        }

        public void vesselSwitch()
        {
            StartCoroutine(Initialise());
        }

        public void OnDestroy()
        {
            instance = null;
        }

        public void drawGUI()
        {
            if (FlightData.thisVessel.Autopilot.Mode == VesselAutopilot.AutopilotMode.StabilityAssist)
            {
                if (AppLauncherFlight.bDisplaySAS)
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
                if (AppLauncherFlight.bDisplaySAS)
                    StockSASwindow = GUILayout.Window(78934857, StockSASwindow, drawRSASWindow, "Stock SAS", GUILayout.Height(0));

                if (bShowPresets)
                {
                    SASPresetwindow = GUILayout.Window(78934858, SASPresetwindow, drawRSASPresetWindow, "SAS Presets", GUILayout.Height(0));
                    SASPresetwindow.x = StockSASwindow.x + StockSASwindow.width;
                    SASPresetwindow.y = StockSASwindow.y;
                }
            }

            if (tooltip != "" && PilotAssistant.Instance.showTooltips)
                GUILayout.Window(34246, new Rect(StockSASwindow.x + StockSASwindow.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));
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
                AppLauncherFlight.bDisplaySAS = false;

            bShowPresets = GUILayout.Toggle(bShowPresets, bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            VesselAutopilot.VesselSAS sas = FlightData.thisVessel.Autopilot.SAS;

            drawPIDValues(sas.pidLockedPitch, "Pitch", SASList.Pitch);
            drawPIDValues(sas.pidLockedRoll, "Roll", SASList.Bank);
            drawPIDValues(sas.pidLockedYaw, "Yaw", SASList.Hdg);

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
                        PresetManager.UpdateSASPreset();
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
                PresetManager.loadSASPreset(PresetManager.Instance.craftPresetDict["default"].SASPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (SASPreset p in PresetManager.Instance.SASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadSASPreset(p);
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
                AppLauncherFlight.bDisplaySAS = false;

            bShowPresets = GUILayout.Toggle(bShowPresets, bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            VesselAutopilot.VesselRSAS rsas = FlightData.thisVessel.Autopilot.RSAS;
            drawPIDValues(rsas.pidPitch, "Pitch", SASList.Pitch);
            drawPIDValues(rsas.pidRoll, "Roll", SASList.Bank);
            drawPIDValues(rsas.pidYaw, "Yaw", SASList.Hdg);

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
                        PresetManager.UpdateRSASPreset();
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newRSASPreset(ref newPresetName);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadRSASPreset(PresetManager.Instance.craftPresetDict["default"].RSASPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (RSASPreset p in PresetManager.Instance.RSASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadRSASPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteRSASPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion

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

        private void drawPIDValues(PIDRclamp controller, string inputName, SASList controllerID)
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
