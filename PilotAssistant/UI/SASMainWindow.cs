using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Presets;
    using Utility;

    internal static class SASMainWindow
    {
        public static Rect windowRect = new Rect(350, 50, 200, 30);

        private static bool showPresets = false;

        private static bool[] stockPIDDisplay = { true, true, true };

        public static void Draw()
        {
            GeneralUI.Styles();

            windowRect = GUILayout.Window(78934856, windowRect, DrawSASWindow, "SAS Module", GUILayout.Width(0), GUILayout.Height(0));

            SASPresetWindow.windowRect.x = windowRect.x + windowRect.width;
            SASPresetWindow.windowRect.y = windowRect.y;

            if (SurfSAS.IsArmed())
            {
                if (SurfSAS.ActivityCheck())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    SurfSAS.ActivitySwitch(!SurfSAS.ActivityCheck());
                    SurfSAS.updateTarget();
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }

            if (showPresets)
                SASPresetWindow.Draw();
        }

        private static void DrawSASWindow(int id)
        {
            bool useStockSAS = SurfSAS.StockSASEnabled();
            GUILayout.BeginVertical(GUILayout.Height(0), GUILayout.Width(0), GUILayout.ExpandHeight(true));
            showPresets = GUILayout.Toggle(showPresets, "Presets", GeneralUI.toggleButtonStyle);
            bool tmpToggle = GUILayout.Toggle(useStockSAS, "Use Stock SAS", GUILayout.MinWidth(200));
            if (tmpToggle != useStockSAS)
            {
                SurfSAS.ToggleStockSAS();
            }
            /*
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
            }*/

            if (!useStockSAS)
            {
                bool tmpToggle1 = GUILayout.Toggle(SurfSAS.IsArmed(), SurfSAS.IsArmed() ? "Disarm SAS" : "Arm SAS", GeneralUI.toggleButtonStyle);
                if (tmpToggle1 != SurfSAS.IsArmed())
                {
                    SurfSAS.ToggleArmed();
                    ScreenMessages.PostScreenMessage("Surface SAS " + (SurfSAS.IsArmed() ? "Armed" : "Disarmed"));
                }

                if (SurfSAS.IsArmed())
                {
                    double pitch = SurfSAS.GetController(SASList.Pitch).SetPoint;
                    double roll = SurfSAS.GetController(SASList.Roll).SetPoint;
                    double hdg = SurfSAS.GetController(SASList.Yaw).SetPoint;
        
                    SurfSAS.GetController(SASList.Pitch).SetPoint = GeneralUI.labPlusNumBox2("Pitch:", pitch.ToString("N2"), 80, 60, 80, -80);
                    SurfSAS.GetController(SASList.Roll).SetPoint = GeneralUI.labPlusNumBox2("Roll:", roll.ToString("N2"), 80, 60, 180, -180);
                    SurfSAS.GetController(SASList.Yaw).SetPoint = GeneralUI.labPlusNumBox2("Heading:", hdg.ToString("N2"), 80, 60, 360, 0);
                    
                    //SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint = Utility.Functions.Clamp((float)GeneralUI.labPlusNumBox2("Pitch:", SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint.ToString("N2"), 80), -80, 80);
                    //SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint = (float)GeneralUI.labPlusNumBox2("Heading:", SurfSAS.SASControllers[(int)SASList.Yaw].SetPoint.ToString("N2"), 80, 60, 360, 0);
                    //SurfSAS.SASControllers[(int)SASList.Roll].SetPoint = (float)GeneralUI.labPlusNumBox2("Roll:", SurfSAS.SASControllers[(int)SASList.Roll].SetPoint.ToString("N2"), 80, 60, 180, -180);
                    drawPIDValues(SASList.Pitch, "Pitch");
                    drawPIDValues(SASList.Roll, "Roll");
                    drawPIDValues(SASList.Yaw, "Yaw");
                }
            }
            else
            {
                VesselSAS sas = Utility.FlightData.thisVessel.VesselSAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", 0);
                drawPIDValues(sas.pidLockedRoll, "Roll", 1);
                drawPIDValues(sas.pidLockedYaw, "Yaw", 2);
            }
 
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void drawPIDValues(SASList controllerID, string inputName)
        {
            PID.PID_Controller controller = SurfSAS.GetController(controllerID);
            if (GUILayout.Button(inputName, GeneralUI.buttonStyle, GUILayout.ExpandWidth(true)))
                controller.bShow = !controller.bShow;

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
            if (GUILayout.Button(inputName, GeneralUI.buttonStyle, GUILayout.ExpandWidth(true)))
            {
                stockPIDDisplay[ID] = !stockPIDDisplay[ID];
            }

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
