using System;
using System.Collections.Generic;
using UnityEngine;

using PilotAssistant.Utility;
namespace PilotAssistant.UI
{
    static class PAMainWindow
    {
        internal static Rect window = new Rect(10, 50, 10, 10);

        internal static Vector2 scrollbarHdg = Vector2.zero;
        internal static Vector2 scrollbarVert = Vector2.zero;

        internal static bool showPresets = false;

        internal static bool showPIDGains = false;
        internal static bool showPIDLimits = false;
        internal static bool showControlSurfaces = false;

        internal static string targetVert = "0";
        internal static string targetHeading = "0";

        static GUIStyle labelStyle;
        static GUIStyle labelAlertStyle;
        static GUIStyle textStyle;
        static GUIStyle btnStyle1;
        static GUIStyle btnStyle2;

        public static void Draw()
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);

            labelAlertStyle = new GUIStyle(GUI.skin.label);
            labelAlertStyle.normal.textColor = XKCDColors.Red;
            labelAlertStyle.fontSize = 24;
            labelAlertStyle.fontStyle = FontStyle.Bold;

            textStyle = new GUIStyle(GUI.skin.textField);
            textStyle.alignment = TextAnchor.MiddleLeft;
            textStyle.margin = new RectOffset(4, 0, 5, 3);

            btnStyle1 = new GUIStyle(GUI.skin.button);
            btnStyle1.margin = new RectOffset(0, 4, 2, 0);

            btnStyle2 = new GUIStyle(GUI.skin.button);
            btnStyle2.margin = new RectOffset(0, 4, 0, 2);

            window = GUI.Window(34244, window, UI.PAMainWindow.displayWindow, "");

            PAPresetWindow.presetWindow.x = window.x + window.width;
            PAPresetWindow.presetWindow.y = window.y;
            if (showPresets)
            {
                PAPresetWindow.Draw();
            }

            // Window resizing
            float height = 345;
            if (PAMainWindow.showPIDGains)
                height += 150;
            if (PAMainWindow.showPIDGains && !PilotAssistant.bWingLeveller)
                height += 150;
            if (!PilotAssistant.bWingLeveller)
                height += 80;
            if (PilotAssistant.bAltitudeHold)
                height += 30;
            if (PilotAssistant.bPause)
                height += 40;
            PAMainWindow.window.height = height;

            if (PAMainWindow.showPIDLimits && PAMainWindow.showPIDGains)
                PAMainWindow.window.width = 420;
            else
                PAMainWindow.window.width = 245;
        }

        private static void displayWindow(int id)
        {
            if (PilotAssistant.bPause)
            {
                GUILayout.Label("CONTROL PAUSED", labelAlertStyle);
            }

            if (GUILayout.Button(showPresets ? "Hide Presets" : "Show Presets"))
            {
                showPresets = !showPresets;
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

            showPIDGains = GUILayout.Toggle(showPIDGains, "Show PID Gains", GUILayout.Width(200));
            if (showPIDGains)
            {
                showPIDLimits = GUILayout.Toggle(showPIDLimits, "Show PID Limits", GUILayout.Width(200));
                showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Show Control Surfaces", GUILayout.Width(200));
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

            GUILayout.BeginVertical();
            #region Hdg GUI
            GUILayout.Label("Heading Control", GUILayout.Width(100));

            if (GUILayout.Button(PilotAssistant.bHdgActive && !PilotAssistant.bPause ? "Deactivate" : "Activate", GUILayout.Width(200)))
            {
                PilotAssistant.bHdgActive = !PilotAssistant.bHdgActive;
                if (PilotAssistant.bPause)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            PilotAssistant.bWingLeveller = GUILayout.Toggle(PilotAssistant.bWingLeveller, PilotAssistant.bWingLeveller ? "Mode: Wing Leveller" : "Mode: Hdg Control", GUILayout.Width(200));
            if (!PilotAssistant.bWingLeveller)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Hdg: ", GUILayout.Width(98));
                targetHeading = GUILayout.TextField(targetHeading, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Update Target Hdg", GUILayout.Width(200)))
                {
                    double newHdg;
                    double.TryParse(targetHeading, out newHdg);
                    if (newHdg >= 0 && newHdg <= 360)
                    {
                        PilotAssistant.controllers[(int)PIDList.HdgBank].SetPoint = newHdg;
                        PilotAssistant.controllers[(int)PIDList.HdgYaw].SetPoint = newHdg;
                        PilotAssistant.bHdgActive = PilotAssistant.bHdgWasActive = true; // skip toggle check to avoid being overwritten
                    }
                }
            }

            GUILayout.Label("Current Hdg: " + FlightData.heading.ToString("N2") + "\u00B0", GUILayout.Width(200));

            scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, (showPIDGains && !PilotAssistant.bWingLeveller) ? GUILayout.Height(160) : GUILayout.Height(0));
            if (!PilotAssistant.bWingLeveller)
            {
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.HdgBank], "Hdg Roll", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0", false, false);
            }
            if (showControlSurfaces)
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Aileron], "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0");
            if (!PilotAssistant.bWingLeveller)
            {
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.HdgYaw], "Hdg Yaw", "\u00B0", FlightData.heading, 2, "Yaw", "\u00B0", false, false);
            }
            if (showControlSurfaces)
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Rudder], "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0");

            GUILayout.EndScrollView();
            #endregion

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

            #region Pitch GUI

            GUILayout.Label("Vertical Control");

            if (GUILayout.Button(PilotAssistant.bVertActive && !PilotAssistant.bPause ? "Deactivate" : "Activate", GUILayout.Width(200)))
            {
                PilotAssistant.bVertActive = !PilotAssistant.bVertActive;
                if (PilotAssistant.bPause)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }

            PilotAssistant.bAltitudeHold = GUILayout.Toggle(PilotAssistant.bAltitudeHold, PilotAssistant.bAltitudeHold ? "Mode: Altitude" : "Mode: Vertical Speed", GUILayout.Width(200));

            GUILayout.BeginHorizontal();
            GUILayout.Label(PilotAssistant.bAltitudeHold ? "Target Altitude: " : "Target Speed: ", GUILayout.Width(98));
            targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
            GUILayout.EndHorizontal();

            if (GUILayout.Button(PilotAssistant.bAltitudeHold ? "Update Target Altitude" : "Update Target Speed", GUILayout.Width(200)))
            {
                PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten

                double newVal;
                double.TryParse(targetVert, out newVal);
                if (PilotAssistant.bAltitudeHold)
                    PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                else
                    PilotAssistant.controllers[(int)PIDList.VertSpeed].SetPoint = newVal;
            }

            scrollbarVert = GUILayout.BeginScrollView(scrollbarVert);
            if (PilotAssistant.bAltitudeHold)
            {
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Altitude], "Alt", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true);
            }
            drawPIDvalues(PilotAssistant.controllers[(int)PIDList.VertSpeed], "Spd ", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);
            if (showControlSurfaces)
            {
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Elevator], "AoA", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0");
            }
            #endregion

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private static void drawPIDvalues(PID.PID_Controller controller, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showCurrent = true)
        {
            if (showPIDGains)
                GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(window.width - 50));

            if (showCurrent)
                GUILayout.Label(string.Format("Current {0}: ", inputName) + inputValue.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

            if (showPIDGains)
            {
                if (showCurrent)
                    GUILayout.Label(string.Format("Target {0}: ", inputName) + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();

                controller.PGain = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString("G3"), 80);
                controller.IGain = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString("G3"), 80);
                controller.DGain = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString("G3"), 80);
                controller.Scalar = labPlusNumBox(string.Format("{0} Scalar", inputName), controller.Scalar.ToString("G3"), 80);

                if (showPIDLimits)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (!invertOutput)
                    {
                        controller.OutMin = labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), controller.OutMin.ToString("G3"));
                        controller.OutMax = labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), controller.OutMax.ToString("G3"));
                        controller.ClampLower = labPlusNumBox("I Clamp Lower", controller.ClampLower.ToString("G3"));
                        controller.ClampUpper = labPlusNumBox("I Clamp Upper", controller.ClampUpper.ToString("G3"));
                    }
                    else
                    { // used when response * -1 is used to get the correct output
                        controller.OutMax = -1 * labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), (-controller.OutMax).ToString("G3"));
                        controller.OutMin = -1 * labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), (-controller.OutMin).ToString("G3"));
                        controller.ClampUpper = -1 * labPlusNumBox("I Clamp Lower", (-controller.ClampUpper).ToString("G3"));
                        controller.ClampLower = -1 * labPlusNumBox("I Clamp Upper", (-controller.ClampLower).ToString("G3"));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
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
    }
}
