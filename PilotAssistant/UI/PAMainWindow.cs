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
            GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            window = GUI.Window(34244, window, displayWindow, "Pilot Assistant", GeneralUI.UISkin.window);

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

            if (PilotAssistant.bPause)
                height += 36;
            if (bShowSettings)
            {
                height += 68;
            }
            if (bShowHdg)
            {
                height += 35;
                if (!PilotAssistant.bWingLeveller)
                {
                    height += 75;
                    hdgScrollHeight = 55;
                }
                if ((PilotAssistant.controllers[(int)PIDList.HdgBank].bShow || PilotAssistant.controllers[(int)PIDList.HdgYaw].bShow) && !PilotAssistant.bWingLeveller)
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
            if (bShowVert) 
            {
                height += 82;
                vertScrollHeight = 38;
                if (PilotAssistant.bAltitudeHold)
                {
                    vertScrollHeight += 27;
                    height += 27;
                }
                if ((PilotAssistant.controllers[(int)PIDList.Altitude].bShow && PilotAssistant.bAltitudeHold) || (PilotAssistant.controllers[(int)PIDList.VertSpeed].bShow))
                {
                    height += 150;
                    vertScrollHeight += 150;
                }
                else if (showControlSurfaces)
                {
                    height += 27;
                    vertScrollHeight += 27;
                    if (PilotAssistant.controllers[(int)PIDList.Elevator].bShow)
                    {
                        height += 123;
                        vertScrollHeight += 123;
                    }
                }
            }
            window.height = height;

            if (showPIDLimits)
                window.width = 370;
            else
                window.width = 225;
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
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button("Options", GUILayout.Width(205)))
            {
                bShowSettings = !bShowSettings;
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            if (bShowSettings)
            {
                if (GUILayout.Button(showPresets ? "Hide Presets" : "Show Presets", GUILayout.Width(200)))
                {
                    showPresets = !showPresets;
                }
                showPIDLimits = GUILayout.Toggle(showPIDLimits, "Show PID Limits", GUILayout.Width(200));
                showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Show Control Surfaces", GUILayout.Width(200));
            }

            #region Hdg GUI

            GUILayout.BeginHorizontal();
            // button background colour
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button("Roll and Yaw Control", GUILayout.Width(186)))
            {
                bShowHdg = !bShowHdg;
            }
            // Toggle colour
            if (PilotAssistant.bHdgActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;

            bool toggleCheck = GUILayout.Toggle(PilotAssistant.bHdgActive, "");
            if (toggleCheck != PilotAssistant.bHdgActive)
            {
                PilotAssistant.bHdgActive = toggleCheck;
                PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.ActivitySwitch(false);
            }
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowHdg)
            {
                PilotAssistant.bWingLeveller = GUILayout.Toggle(PilotAssistant.bWingLeveller, PilotAssistant.bWingLeveller ? "Mode: Wing Leveller" : "Mode: Hdg Control", GUILayout.Width(200));
                if (!PilotAssistant.bWingLeveller)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Hdg: ", GUILayout.Width(98)))
                    {
                        ScreenMessages.PostScreenMessage("Target Heading updated");
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

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GeneralUI.scrollview, GUILayout.Height(hdgScrollHeight));
                if (!PilotAssistant.bWingLeveller)
                {
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.HdgBank], "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0", false, true, false);
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.HdgYaw], "Bank => Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Aileron], "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0", false, true, false);
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Rudder], "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0", false, true, false);
                }
                GUILayout.EndScrollView();
            }
            #endregion

            #region Pitch GUI

            GUILayout.BeginHorizontal();
            // button background
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button("Vertical Control", GUILayout.Width(186)))
            {
                bShowVert = !bShowVert;
            }
            // Toggle colour
            if (PilotAssistant.bVertActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;

            toggleCheck = GUILayout.Toggle(PilotAssistant.bVertActive, "");
            if (toggleCheck != PilotAssistant.bVertActive)
            {
                PilotAssistant.bVertActive = toggleCheck;
                if (!toggleCheck)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.ActivitySwitch(false);
            }
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowVert)
            {
                PilotAssistant.bAltitudeHold = GUILayout.Toggle(PilotAssistant.bAltitudeHold, PilotAssistant.bAltitudeHold ? "Mode: Altitude" : "Mode: Vertical Speed", GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(PilotAssistant.bAltitudeHold ? "Target Altitude:" : "Target Speed:", GUILayout.Width(98)))
                {
                    ScreenMessages.PostScreenMessage("Target " + (PilotAssistant.bAltitudeHold ? "Altitude" : "Vertical Speed") + " updated");

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (PilotAssistant.bAltitudeHold)
                        PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                    else
                        PilotAssistant.controllers[(int)PIDList.VertSpeed].SetPoint = newVal;

                    PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GeneralUI.scrollview);
                
                if (PilotAssistant.bAltitudeHold)
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Altitude], "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true, true, false);
                drawPIDvalues(PilotAssistant.controllers[(int)PIDList.VertSpeed], "Vertical Speed", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);
                
                if (showControlSurfaces)
                    drawPIDvalues(PilotAssistant.controllers[(int)PIDList.Elevator], "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true, true, false);

                GUILayout.EndScrollView();
            }
            #endregion

            GUI.DragWindow();
        }

        private static void drawPIDvalues(PID.PID_Controller controller, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showTarget = true, bool doublesided = true)
        {
            if (GUILayout.Button(string.Format("{0}: {1}{2}", inputName, inputValue.ToString("N" + displayPrecision.ToString()), inputUnits), GUILayout.Width(window.width - 50)))
            {
                controller.bShow = !controller.bShow;
            }
            if (controller.bShow)
            {
                if (showTarget)
                    GUILayout.Label(string.Format("Target: ", inputName) + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits);

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();

                controller.PGain = GeneralUI.labPlusNumBox(string.Format("Kp:", inputName), controller.PGain.ToString("G3"), 45);
                controller.IGain = GeneralUI.labPlusNumBox(string.Format("Ki:", inputName), controller.IGain.ToString("G3"), 45);
                controller.DGain = GeneralUI.labPlusNumBox(string.Format("Kd:", inputName), controller.DGain.ToString("G3"), 45);
                controller.Scalar = GeneralUI.labPlusNumBox(string.Format("Scalar:", inputName), controller.Scalar.ToString("G3"), 45);

                if (showPIDLimits)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (!invertOutput)
                    {
                        controller.OutMax = GeneralUI.labPlusNumBox(string.Format("Max {0}{1}:", outputName, outputUnits), controller.OutMax.ToString("G3"));
                        if (doublesided)
                            controller.OutMin = GeneralUI.labPlusNumBox(string.Format("Min {0}{1}:", outputName, outputUnits), controller.OutMin.ToString("G3"));
                        else
                            controller.OutMin = -controller.OutMax;
                        controller.ClampLower = GeneralUI.labPlusNumBox("I Clamp Lower:", controller.ClampLower.ToString("G3"));
                        controller.ClampUpper = GeneralUI.labPlusNumBox("I Clamp Upper:", controller.ClampUpper.ToString("G3"));
                    }
                    else
                    { // used when response * -1 is used to get the correct output
                        controller.OutMax = -1 * GeneralUI.labPlusNumBox(string.Format("Min {0}{1}:", outputName, outputUnits), (-controller.OutMax).ToString("G3"));
                        if (doublesided)
                            controller.OutMin = -1 * GeneralUI.labPlusNumBox(string.Format("Max {0}{1}:", outputName, outputUnits), (-controller.OutMin).ToString("G3"));
                        else
                            controller.OutMin = -controller.OutMax;
                        controller.ClampUpper = -1 * GeneralUI.labPlusNumBox("I Clamp Lower:", (-controller.ClampUpper).ToString("G3"));
                        controller.ClampLower = -1 * GeneralUI.labPlusNumBox("I Clamp Upper:", (-controller.ClampLower).ToString("G3"));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }
    }
}
