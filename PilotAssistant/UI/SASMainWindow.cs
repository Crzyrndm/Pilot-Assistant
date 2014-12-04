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

        internal static bool[] stockPIDDisplay = { true, true, true };

        public static void Draw()
        {
            GeneralUI.Styles();

            if (AppLauncher.AppLauncherInstance.bDisplaySAS)
            {
                SASwindow = GUI.Window(78934856, SASwindow, drawSASWindow, "SAS Module");
            }

            if (SurfSAS.bArmed)
            {
                if (SurfSAS.bActive)
                    GUI.backgroundColor = GeneralUI.SASActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    SurfSAS.bActive = !SurfSAS.bActive;
                    SurfSAS.updateTarget();
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }
            float height = 64;
            if (!SurfSAS.bStockSAS && SurfSAS.bArmed || SurfSAS.bStockSAS)
                height += 86;
            if (!SurfSAS.bStockSAS)
                height += 113;
            if ((stockPIDDisplay[0] && SurfSAS.bStockSAS) || (SurfSAS.SASControllers[(int)SASList.Pitch].bShow && !SurfSAS.bStockSAS))
                height += 113;
            if ((stockPIDDisplay[1] && SurfSAS.bStockSAS) || (SurfSAS.SASControllers[(int)SASList.Roll].bShow && !SurfSAS.bStockSAS))
                height += 113;
            if ((stockPIDDisplay[2] && SurfSAS.bStockSAS) || (SurfSAS.SASControllers[(int)SASList.Hdg].bShow && !SurfSAS.bStockSAS))
                height += 113;
            SASwindow.height = height;

            SASPresetWindow.Draw();
        }

        private static void drawSASWindow(int id)
        {
            if (GUI.Button(new Rect(SASwindow.width - 16, 2, 14, 14), ""))
            {
                AppLauncher.AppLauncherInstance.bDisplaySAS = false;
            }

            if (GUILayout.Button("SAS Presets"))
            {
                SASPresetWindow.bShowPresets = !SASPresetWindow.bShowPresets;
            }

            SurfSAS.bStockSAS = GUILayout.Toggle(SurfSAS.bStockSAS, "Use Stock SAS");
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
                if (GUILayout.Button(SurfSAS.bArmed ? "Disarm SAS" : "Arm SAS"))
                {
                    SurfSAS.bArmed = !SurfSAS.bArmed;
                    if (!SurfSAS.bArmed)
                        SurfSAS.bActive = false;
                }
                if (SurfSAS.bArmed)
                {
                    SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint = Utility.Functions.Clamp((float)GeneralUI.labPlusNumBox2("Pitch:", SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint.ToString("N2"), 80), -80, 80);
                    SurfSAS.SASControllers[(int)SASList.Hdg].SetPoint = (float)GeneralUI.labPlusNumBox2("Heading:", SurfSAS.SASControllers[(int)SASList.Hdg].SetPoint.ToString("N2"), 80, 60, 360, 0);
                    SurfSAS.SASControllers[(int)SASList.Roll].SetPoint = (float)GeneralUI.labPlusNumBox2("Roll:", SurfSAS.SASControllers[(int)SASList.Roll].SetPoint.ToString("N2"), 80, 60, 180, -180);
                }
                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Pitch], "Pitch");
                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Roll], "Roll");
                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Hdg], "Yaw");
            }
            else
            {
                VesselSAS sas = Utility.FlightData.thisVessel.VesselSAS;

                drawPIDvalues(sas.pidLockedPitch, "Pitch", 0);
                drawPIDvalues(sas.pidLockedRoll, "Roll", 1);
                drawPIDvalues(sas.pidLockedYaw, "Yaw", 2);
            }
            GUI.DragWindow();
        }

        private static void drawPIDvalues(PID.PID_Controller controller, string inputName)
        {
            if (GUILayout.Button(inputName))
                controller.bShow = !controller.bShow;

            if (controller.bShow)
            {
                controller.PGain = GeneralUI.labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString("G3"), 80);
                controller.IGain = GeneralUI.labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString("G3"), 80);
                controller.DGain = GeneralUI.labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString("G3"), 80);
                controller.Scalar = GeneralUI.labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.Scalar.ToString("G3"), 80);
            }
        }

        private static void drawPIDvalues(PIDclamp controller, string inputName, int ID)
        {
            if (GUILayout.Button(inputName))
            {
                stockPIDDisplay[ID] = !stockPIDDisplay[ID];
            }

            if (stockPIDDisplay[ID])
            {
                controller.kp = GeneralUI.labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.kp.ToString("G3"), 80);
                controller.ki = GeneralUI.labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.ki.ToString("G3"), 80);
                controller.kd = GeneralUI.labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.kd.ToString("G3"), 80);
                controller.clamp = GeneralUI.labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.clamp.ToString("G3"), 80);
            }
        }
    }
}
