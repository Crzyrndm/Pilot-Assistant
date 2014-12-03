using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Presets;

    internal static class SASMainWindow
    {
        internal static GUIStyle labelStyle;
        internal static GUIStyle textStyle;
        internal static GUIStyle btnStyle1;
        internal static GUIStyle btnStyle2;

        internal static Rect SASwindow = new Rect(350, 50, 200, 30);

        public static void Draw()
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);

            textStyle = new GUIStyle(GUI.skin.textField);
            textStyle.alignment = TextAnchor.MiddleLeft;
            textStyle.margin = new RectOffset(4, 0, 5, 3);

            btnStyle1 = new GUIStyle(GUI.skin.button);
            btnStyle1.margin = new RectOffset(0, 4, 2, 0);

            btnStyle2 = new GUIStyle(GUI.skin.button);
            btnStyle2.margin = new RectOffset(0, 4, 0, 2);

            if (AppLauncher.AppLauncherInstance.bDisplaySAS)
            {
                SASwindow = GUI.Window(78934856, SASwindow, drawSASWindow, "SAS Module");
            }

            if (SurfSAS.bArmed)
            {
                Color c = GUI.backgroundColor;
                if (SurfSAS.bActive)
                    GUI.backgroundColor = XKCDColors.BrightSkyBlue;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    SurfSAS.bActive = !SurfSAS.bActive;
                    SurfSAS.updateTarget();
                }
                GUI.backgroundColor = c;
            }

            if (SurfSAS.bStockSAS)
                SASwindow.height = 440;
            else
                SASwindow.height = 550;

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
                //GUILayout.Label("Atmospheric Mode: " + bAtmosphere.ToString());

                SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint = Utility.Functions.Clamp((float)labPlusNumBox2("Pitch:", SurfSAS.SASControllers[(int)SASList.Pitch].SetPoint.ToString("N2"), 80), -80, 80);
                SurfSAS.SASControllers[(int)SASList.Hdg].SetPoint = (float)labPlusNumBox2("Heading:", SurfSAS.SASControllers[(int)SASList.Hdg].SetPoint.ToString("N2"), 80, 60, 360, 0);
                SurfSAS.SASControllers[(int)SASList.Roll].SetPoint = (float)labPlusNumBox2("Roll:", SurfSAS.SASControllers[(int)SASList.Roll].SetPoint.ToString("N2"), 80, 60, 180, -180);

                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Pitch], "Pitch");
                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Roll], "Roll");
                drawPIDvalues(SurfSAS.SASControllers[(int)SASList.Hdg], "Yaw");
            }
            else
            {
                VesselSAS sas = Utility.FlightData.thisVessel.VesselSAS;

                drawPIDvalues(sas.pidLockedPitch, "Pitch");
                drawPIDvalues(sas.pidLockedRoll, "Roll");
                drawPIDvalues(sas.pidLockedYaw, "Yaw");
            }
            GUI.DragWindow();
        }

        private static void drawPIDvalues(PID.PID_Controller controller, string inputName)
        {
            GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(SASwindow.width - 50));

            controller.PGain = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString("G3"), 80);
            controller.IGain = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString("G3"), 80);
            controller.DGain = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString("G3"), 80);
            controller.Scalar = labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.Scalar.ToString("G3"), 80);
        }

        private static void drawPIDvalues(PIDclamp controller, string inputName)
        {
            GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(SASwindow.width - 50));

            controller.kp = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.kp.ToString("G3"), 80);
            controller.ki = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.ki.ToString("G3"), 80);
            controller.kd = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.kd.ToString("G3"), 80);
            controller.clamp = labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.clamp.ToString("G3"), 80);
        }

        private static double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
            val = double.Parse(boxText);
            boxText = val.ToString(",0.0#####");
            string text = GUILayout.TextField(boxText, textStyle, GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", btnStyle1, GUILayout.Width(20), GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", btnStyle2, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        private static double labPlusNumBox2(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60, float upper = 360, float lower = -360)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
            string text = GUILayout.TextField(boxText, textStyle, GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", btnStyle1, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val += 1;
                if (val >= upper)
                    val = lower;
            }
            if (GUILayout.Button("-", btnStyle2, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val -= 1;
                if (val < lower)
                    val = upper - 1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return Utility.Functions.Clamp(val, lower, upper);
        }
    }
}
