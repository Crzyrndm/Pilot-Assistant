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
                    SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint = Functions.Clamp((float)GeneralUI.TogPlusNumBox("Pitch:", ref SurfSAS.bActive[(int)SASList.Pitch], SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint, 80), -80, 80);
                    SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint = (float)GeneralUI.TogPlusNumBox("Heading:", ref SurfSAS.bActive[(int)SASList.Yaw], SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint, 80, 60, 360, 0);
                    if (!SurfSAS.rollState) // editable
                        SurfSAS.SASControllers[(int)SASList.Roll].SetPoint = (float)GeneralUI.TogPlusNumBox("Roll:", ref SurfSAS.bActive[(int)SASList.Roll], SurfSAS.SASControllers[(int)SASList.Roll].SetPoint, 80, 60, 180, -180);
                    else // not editable b/c vector mode
                    {
                        GUILayout.BeginHorizontal();
                        SurfSAS.bActive[(int)SASList.Roll] = GUILayout.Toggle(SurfSAS.bActive[(int)SASList.Roll], "Roll:", GeneralUI.toggleButton, GUILayout.Width(80));
                        GUILayout.TextField(FlightData.roll.ToString("N2"), GUILayout.Width(60));
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Box("", GUILayout.Height(10));
                    drawPIDValues(SASList.Pitch, "Pitch");
                    drawPIDValues(SASList.Roll, "Roll");
                    drawPIDValues(SASList.Yaw, "Yaw");
                }
            }
            else
            {
                VesselAutopilot.VesselSAS sas = FlightData.thisVessel.Autopilot.SAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", (int)SASList.Pitch);
                drawPIDValues(sas.pidLockedRoll, "Roll", (int)SASList.Roll);
                drawPIDValues(sas.pidLockedYaw, "Yaw", (int)SASList.Yaw);

                
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
                controller.clamp = Math.Max(GeneralUI.labPlusNumBox("Scalar:", controller.clamp.ToString("G3"), 45), 0.01);
            }
        }
    }
}
