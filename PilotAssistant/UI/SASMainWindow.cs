using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Presets;
    using Utility;

    internal static class SASMainWindow
    {
        internal static Rect SASwindow = new Rect(350, 50, 200, 30);

        internal static bool[] stockPIDDisplay = { true, false, false };

        public static void Draw()
        {
            GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            if (AppLauncher.AppLauncherInstance.bDisplaySAS)
                SASwindow = GUILayout.Window(78934856, SASwindow, drawSASWindow, "SAS Module", GUILayout.Height(0));

            SASPresetWindow.Draw();
        }

        private static void drawSASWindow(int id)
        {
            if (GUI.Button(new Rect(SASwindow.width - 16, 2, 14, 14), ""))
            {
                AppLauncher.AppLauncherInstance.bDisplaySAS = false;
            }

            SASPresetWindow.bShowPresets = GUILayout.Toggle(SASPresetWindow.bShowPresets, SASPresetWindow.bShowPresets ? "Hide SAS Presets" : "Show SAS Presets");

            SurfSAS.bStockSAS = GUILayout.Toggle(SurfSAS.bStockSAS, SurfSAS.bStockSAS ? "Mode: Stock SAS" : "Mode: SSAS");
            if (SurfSAS.bStockSAS != SurfSAS.bWasStockSAS)
            {
                SurfSAS.bWasStockSAS = SurfSAS.bStockSAS;
                if (SurfSAS.bStockSAS)
                {
                    if (PresetManager.activeStockSASPreset == null)
                    {
                        PresetManager.loadStockSASPreset(PresetManager.defaultStockSASTuning);
                        PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
                    }
                    else
                        PresetManager.loadStockSASPreset(PresetManager.activeStockSASPreset);
                }
                else
                {
                    if (PresetManager.activeSASPreset == null)
                    {
                        PresetManager.loadSASPreset(PresetManager.defaultSASTuning);
                        PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
                    }
                    else
                        PresetManager.loadSASPreset(PresetManager.activeSASPreset);
                }
            }

            if (!SurfSAS.bStockSAS)
            {
                GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
                if (GUILayout.Button(SurfSAS.bArmed ? "Disarm SAS" : "Arm SAS"))
                {
                    SurfSAS.bArmed = !SurfSAS.bArmed;
                    if (!SurfSAS.bArmed)
                        SurfSAS.ActivitySwitch(false);

                    if (SurfSAS.bArmed)
                        Messaging.statusMessage(8);
                    else
                        Messaging.statusMessage(9);
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

                if (SurfSAS.bArmed)
                {
                    SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint = Functions.Clamp((float)GeneralUI.labPlusNumBox2("Pitch:", SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint.ToString("N2"), 80), -80, 80);
                    SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint = (float)GeneralUI.labPlusNumBox2("Heading:", SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint.ToString("N2"), 80, 60, 360, 0);
                    SurfSAS.SASControllers[(int)SASList.Roll].SetPoint = (float)GeneralUI.labPlusNumBox2("Roll:", SurfSAS.SASControllers[(int)SASList.Roll].SetPoint.ToString("N2"), 80, 60, 180, -180);
                    drawPIDValues(SASList.Pitch, "Pitch");
                    drawPIDValues(SASList.Roll, "Roll");
                    drawPIDValues(SASList.Yaw, "Yaw");
                }
            }
            else
            {
                VesselAutopilot.VesselSAS sas = FlightData.thisVessel.Autopilot.SAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", 0);
                drawPIDValues(sas.pidLockedRoll, "Roll", 1);
                drawPIDValues(sas.pidLockedYaw, "Yaw", 2);
            }

            GUI.DragWindow();
        }

        private static void drawPIDValues(SASList controllerID, string inputName)
        {
            PID.PID_Controller controller = SurfSAS.SASControllers[(int)controllerID];
            controller.bShow = GUILayout.Toggle(controller.bShow, inputName, GeneralUI.toggleButton);

            if (controller.bShow)
            {
                controller.PGain = GeneralUI.labPlusNumBox("Kp:", controller.PGain.ToString("G3"), 45);
                controller.IGain = GeneralUI.labPlusNumBox("Ki:", controller.IGain.ToString("G3"), 45);
                controller.DGain = GeneralUI.labPlusNumBox("Kd:", controller.DGain.ToString("G3"), 45);
                controller.Scalar = GeneralUI.labPlusNumBox("Scalar:", controller.Scalar.ToString("G3"), 45);
            }
        }

        private static void drawPIDValues(PIDclamp controller, string inputName, int ID)
        {
            stockPIDDisplay[ID] = GUILayout.Toggle(stockPIDDisplay[ID], inputName, GeneralUI.toggleButton);
            

            if (stockPIDDisplay[ID])
            {
                controller.kp = GeneralUI.labPlusNumBox("Kp:", controller.kp.ToString("G3"), 45);
                controller.ki = GeneralUI.labPlusNumBox("Ki:", controller.ki.ToString("G3"), 45);
                controller.kd = GeneralUI.labPlusNumBox("Kd:", controller.kd.ToString("G3"), 45);
                controller.clamp = GeneralUI.labPlusNumBox("Scalar:", controller.clamp.ToString("G3"), 45);
            }
        }
    }
}
