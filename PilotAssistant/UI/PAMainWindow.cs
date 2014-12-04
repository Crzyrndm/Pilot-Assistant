using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Utility;

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

        internal static bool bShowSettings = true;
        internal static bool bShowHdg = true;
        internal static bool bShowVert = true;

        internal static float hdgScrollHeight;
        internal static float vertScrollHeight;

        public static void Draw()
        {
            GeneralUI.Styles();

            window = GUI.Window(34244, window, displayWindow, "Pilot Assistant");

            PAPresetWindow.presetWindow.x = window.x + window.width;
            PAPresetWindow.presetWindow.y = window.y;
            if (showPresets)
            {
                PAPresetWindow.Draw();
            }

            // Window resizing
            float height = 100;
            hdgScrollHeight = 0;
            vertScrollHeight = 0;

            if (bShowSettings)
            {
                height += 46;
                if (showPIDGains)
                    height += 44;
            }

            if (bShowHdg)
                height += 75;
            if (bShowHdg && showPIDGains)
            {
                height += 55;
                hdgScrollHeight = 55;
                if (showPIDGains)
                {
                    if (PilotAssistant.controllers[(int)PIDList.HdgBank].bShow || PilotAssistant.controllers[(int)PIDList.HdgYaw].bShow && !showControlSurfaces)
                    {
                        height += 130;
                        hdgScrollHeight += 130;
                    }
                    else if (showControlSurfaces)
                    {
                        height += 50;
                        hdgScrollHeight += 50;
                        if (PilotAssistant.controllers[(int)PIDList.Aileron].bShow || PilotAssistant.controllers[(int)PIDList.Rudder].bShow)
                        {
                            height += 80;
                            hdgScrollHeight += 80;
                        }
                    }
                }
            }
            if (bShowVert)
                height += 75;
            if (bShowVert && showPIDGains)
            {
                height += 7;
                vertScrollHeight = 35;
                if (PilotAssistant.bAltitudeHold)
                {
                    height += 27;
                    vertScrollHeight += 27;
                }

                if (showPIDGains)
                {
                    if ((PilotAssistant.controllers[(int)PIDList.Altitude].bShow && PilotAssistant.bAltitudeHold) || PilotAssistant.controllers[(int)PIDList.VertSpeed].bShow && !showControlSurfaces)
                    {
                        height += 130;
                        vertScrollHeight += 130;
                    }
                    else if (showControlSurfaces)
                    {
                        height += 27;
                        vertScrollHeight += 27;
                        if (PilotAssistant.controllers[(int)PIDList.Elevator].bShow)
                        {
                            height += 80;
                            vertScrollHeight += 80;
                        }
                    }
                }
            }

            
            window.height = height;

            if (showPIDLimits && showPIDGains)
                window.width = 420;
            else
                window.width = 245;
        }

        private static void displayWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
            {
                AppLauncher.AppLauncherInstance.bDisplayAssistant = false;
            }

            if (PilotAssistant.bPause)
            {
                GUILayout.Label("CONTROL PAUSED", GeneralUI.labelAlertStyle);
            }

            if (GUILayout.Button("Options", GUILayout.Width(225)))
            {
                bShowSettings = !bShowSettings;
            }
            if (bShowSettings)
            {
                if (GUILayout.Button(showPresets ? "Hide Presets" : "Show Presets", GUILayout.Width(200)))
                {
                    showPresets = !showPresets;
                }

                showPIDGains = GUILayout.Toggle(showPIDGains, "Show PID Gains", GUILayout.Width(200));
                if (showPIDGains)
                {
                    showPIDLimits = GUILayout.Toggle(showPIDLimits, "Show PID Limits", GUILayout.Width(200));
                    showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Show Control Surfaces", GUILayout.Width(200));
                }
                else
                {
                    showPIDLimits = showControlSurfaces = false;
                }
            }

            #region Hdg GUI

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Roll and Yaw Control", GUILayout.Width(206)))
            {
                bShowHdg = !bShowHdg;
            }
            bool toggleCheck = GUILayout.Toggle(PilotAssistant.bHdgActive, "");
            if (toggleCheck != PilotAssistant.bHdgActive)
            {
                PilotAssistant.bHdgActive = toggleCheck;
                PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.EndHorizontal();

            if (bShowHdg)
            {
                PilotAssistant.bWingLeveller = GUILayout.Toggle(PilotAssistant.bWingLeveller, PilotAssistant.bWingLeveller ? "Mode: Wing Leveller" : "Mode: Hdg Control", GUILayout.Width(200));
                if (!PilotAssistant.bWingLeveller)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Hdg: ", GUILayout.Width(98)))
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
                    targetHeading = GUILayout.TextField(targetHeading, GUILayout.Width(98));
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("Current Hdg: " + FlightData.heading.ToString("N2") + "\u00B0", GUILayout.Width(200));

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GeneralUI.scrollview, GUILayout.Height(hdgScrollHeight));
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
            }
            #endregion

            #region Pitch GUI

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Vertical Control", GUILayout.Width(206)))
            {
                bShowVert = !bShowVert;
            }
            toggleCheck = GUILayout.Toggle(PilotAssistant.bVertActive, "");
            if (toggleCheck != PilotAssistant.bVertActive)
            {
                PilotAssistant.bVertActive = toggleCheck;
                if (!toggleCheck)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.EndHorizontal();

            if (bShowVert)
            {
                PilotAssistant.bAltitudeHold = GUILayout.Toggle(PilotAssistant.bAltitudeHold, PilotAssistant.bAltitudeHold ? "Mode: Altitude" : "Mode: Vertical Speed", GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(PilotAssistant.bAltitudeHold ? "Target Altitude:" : "Target Speed:", GUILayout.Width(98)))
                {
                    PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (PilotAssistant.bAltitudeHold)
                        PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                    else
                        PilotAssistant.controllers[(int)PIDList.VertSpeed].SetPoint = newVal;
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GeneralUI.scrollview);
                if (PilotAssistant.bAltitudeHold)
                {
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Altitude], "Alt", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true);
                }
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.VertSpeed], "Spd ", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);
                if (showControlSurfaces)
                {
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Elevator], "AoA", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0");
                }

                GUILayout.EndScrollView();
            }
            #endregion

            GUI.DragWindow();
        }

        private static void drawPIDvalues(PID.PID_Controller controller, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showCurrent = true)
        {
            if (showPIDGains)
            {
                if (GUILayout.Button(inputName, GUILayout.Width(window.width - 50)))
                {
                    controller.bShow = !controller.bShow;
                }
            }
            if (controller.bShow)
            {
                if (showCurrent)
                    GUILayout.Label(string.Format("Current: ", inputName) + inputValue.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

                if (showPIDGains)
                {
                    if (showCurrent)
                        GUILayout.Label(string.Format("Target: ", inputName) + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical();

                    controller.PGain = GeneralUI.labPlusNumBox(string.Format("Kp: ", inputName), controller.PGain.ToString("G3"), 80);
                    controller.IGain = GeneralUI.labPlusNumBox(string.Format("Ki: ", inputName), controller.IGain.ToString("G3"), 80);
                    controller.DGain = GeneralUI.labPlusNumBox(string.Format("Kd: ", inputName), controller.DGain.ToString("G3"), 80);
                    controller.Scalar = GeneralUI.labPlusNumBox(string.Format("Scalar", inputName), controller.Scalar.ToString("G3"), 80);

                    if (showPIDLimits)
                    {
                        GUILayout.EndVertical();
                        GUILayout.BeginVertical();

                        if (!invertOutput)
                        {
                            controller.OutMin = GeneralUI.labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), controller.OutMin.ToString("G3"));
                            controller.OutMax = GeneralUI.labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), controller.OutMax.ToString("G3"));
                            controller.ClampLower = GeneralUI.labPlusNumBox("I Clamp Lower", controller.ClampLower.ToString("G3"));
                            controller.ClampUpper = GeneralUI.labPlusNumBox("I Clamp Upper", controller.ClampUpper.ToString("G3"));
                        }
                        else
                        { // used when response * -1 is used to get the correct output
                            controller.OutMax = -1 * GeneralUI.labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), (-controller.OutMax).ToString("G3"));
                            controller.OutMin = -1 * GeneralUI.labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), (-controller.OutMin).ToString("G3"));
                            controller.ClampUpper = -1 * GeneralUI.labPlusNumBox("I Clamp Lower", (-controller.ClampUpper).ToString("G3"));
                            controller.ClampLower = -1 * GeneralUI.labPlusNumBox("I Clamp Upper", (-controller.ClampLower).ToString("G3"));
                        }
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
