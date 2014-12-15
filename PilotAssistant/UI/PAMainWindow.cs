using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Utility;

    static class PAMainWindow
    {
        private static Rect windowRect = new Rect(10, 50, 10, 10);

        private static bool showPresets = false;

        private static bool showPIDLimits = false;
        private static bool showControlSurfaces = false;

        private static string targetVert = "0";
        private static string targetAlt = "0";
        private static string targetHeading = "0";

        //private static bool showSettings = true;
        //private static bool showHdg = true;
        //private static bool showVert = true;

        private const int WINDOW_ID = 34244;

        public static void Draw()
        {
            GeneralUI.Styles();

            // GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            windowRect = GUILayout.Window(WINDOW_ID, windowRect, DrawWindow, "Pilot Assistant", GUILayout.Width(0), GUILayout.Height(0));

            PAPresetWindow.windowRect.x = windowRect.x + windowRect.width;
            PAPresetWindow.windowRect.y = windowRect.y;
            if (showPresets)
            {
                PAPresetWindow.Draw();
            }
        }

        public static void SetTargetHeading(double heading)
        {
            targetHeading = heading.ToString("N2");
        }

        public static double GetTargetHeading()
        {
            return double.Parse(targetHeading);
        }

        public static void SetTargetVerticalSpeed(double speed)
        {
            targetVert = speed.ToString("N3");
        }

        public static double GetTargetVerticalSpeed()
        {
            return double.Parse(targetVert);
        }

        public static void SetTargetAltitude(double altitude)
        {
            targetAlt = altitude.ToString("N1");
        }

        public static double GetTargetAltitude()
        {
            return double.Parse(targetAlt);
        }

        private static void DrawHeadingControls()
        {
            bool isHdgActive = PilotAssistant.IsHdgActive();
            bool isWingLvlActive = PilotAssistant.IsWingLvlActive();
            
            // Heading
            GUILayout.BeginVertical(GeneralUI.guiSectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isHdgActive, isHdgActive ? "On" : "Off", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(false)) != isHdgActive)
            {
                PilotAssistant.ToggleHdg();
                //PilotAssistant.bHdgActive = !PilotAssistant.bHdgActive;
                //PilotAssistant.bPause = false;
                //FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                //SurfSAS.ActivitySwitch(false);
            }
            GUILayout.Label("Roll and Yaw Control", GeneralUI.boldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            bool tmpToggle1 = GUILayout.Toggle(!isWingLvlActive, "Hdg control", GeneralUI.toggleButtonStyle);
            bool tmpToggle2 = GUILayout.Toggle(isWingLvlActive, "Wing lvl", GeneralUI.toggleButtonStyle);
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.ToggleWingLvl();
                //PilotAssistant.bWingLeveller = !PilotAssistant.bWingLeveller;
                
            GUILayout.EndHorizontal();
            if (!isWingLvlActive)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Hdg: ");
                targetHeading = GUILayout.TextField(targetHeading);
                if (GUILayout.Button("Set", GeneralUI.buttonStyle, GUILayout.ExpandWidth(false)))
                {
                    ScreenMessages.PostScreenMessage("Target Heading updated");
                    double newHdg;
                    if (double.TryParse(targetHeading, out newHdg) &&
                        newHdg >= 0 && newHdg <= 360)
                    {
                        PilotAssistant.SetHdgActive();
                        //PilotAssistant.controllers[(int)PIDList.HdgBank].SetPoint = newHdg;
                        //PilotAssistant.controllers[(int)PIDList.HdgYaw].SetPoint = newHdg;
                        //PilotAssistant.bHdgActive = PilotAssistant.bHdgWasActive = true; // skip toggle check to avoid being overwritten
                    }
                    else
                    {
                        // Bad input, reset UI to previous value
                        
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (!isWingLvlActive)
            {
                drawPIDValues(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0", false, true, false);
                drawPIDValues(PIDList.HdgYaw, "Bank => Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false, false);
            }
            if (showControlSurfaces)
            {
                drawPIDValues(PIDList.Aileron, "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0", false, true, false);
                drawPIDValues(PIDList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0", false, true, false);
            }
            GUILayout.EndVertical();            
        }

        private static void DrawVerticalControls()
        {
            bool isVertActive = PilotAssistant.IsVertActive();
            bool isAltitudeHoldActive = PilotAssistant.IsAltitudeHoldActive();
            // Vertical speed
            GUILayout.BeginVertical(GeneralUI.guiSectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isVertActive, isVertActive ? "On" : "Off", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(false)) != isVertActive)
            {
                PilotAssistant.ToggleVert();
                //PilotAssistant.bVertActive = !PilotAssistant.bVertActive;
                //if (!PilotAssistant.bVertActive)
                //    PilotAssistant.bPause = false;
                //FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                //SurfSAS.ActivitySwitch(false);
            }
            GUILayout.Label("Vertical Control", GeneralUI.boldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            bool tmpToggle1 = GUILayout.Toggle(isAltitudeHoldActive, "Altitude", GeneralUI.toggleButtonStyle);
            bool tmpToggle2 = GUILayout.Toggle(!isAltitudeHoldActive, "Vertical Speed", GeneralUI.toggleButtonStyle);
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.ToggleAltitudeHold();
                //PilotAssistant.bAltitudeHold = !PilotAssistant.bAltitudeHold;
            
            GUILayout.EndHorizontal();

            if (isAltitudeHoldActive)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Altitude: ");
                targetAlt = GUILayout.TextField(targetAlt);
                if (GUILayout.Button("Set", GeneralUI.buttonStyle, GUILayout.ExpandWidth(false)))
                {
                    ScreenMessages.PostScreenMessage("Target Altitude updated");

                    double newVal;
                    if (double.TryParse(targetAlt, out newVal))
                    {
                        PilotAssistant.SetAltitudeHoldActive();
                        //PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                        //PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
                    }
                    else
                    {
                        // Bad input, reset UI value
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Speed: ");
                targetVert = GUILayout.TextField(targetVert);
                if (GUILayout.Button("Set", GeneralUI.buttonStyle, GUILayout.ExpandWidth(false)))
                {
                    ScreenMessages.PostScreenMessage("Target Speed updated");

                    double newVal;
                    if (double.TryParse(targetVert, out newVal))
                    {
                        PilotAssistant.SetVertActive();
                        //PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                        //PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
                    }
                    else
                    {
                        // Bad input, reset UI value
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (isAltitudeHoldActive)
                drawPIDValues(PIDList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true, true, false);
            drawPIDValues(PIDList.VertSpeed, "Vertical Speed", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);
            if (showControlSurfaces)
                drawPIDValues(PIDList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true, true, false);
            GUILayout.EndVertical();
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.Height(0), GUILayout.Width(0), GUILayout.ExpandHeight(true));
            if (PilotAssistant.IsPaused())
            {
                GUILayout.Label("CONTROL PAUSED", GeneralUI.labelAlertStyle, GUILayout.ExpandWidth(true));
            }
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            showPresets = GUILayout.Toggle(showPresets, "Presets", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            showPIDLimits = GUILayout.Toggle(showPIDLimits, "PID Limits", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Ctrl Surfaces", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            DrawHeadingControls();

            DrawVerticalControls();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void drawPIDValues(PIDList controllerID, string inputName, string inputUnits, double inputValue,
                                          int displayPrecision, string outputName, string outputUnits,
                                          bool invertOutput = false, bool showTarget = true, bool doublesided = true)
        {
            PID.PID_Controller controller = PilotAssistant.GetController(controllerID); // controllers[(int)controllerID];
            string buttonText = string.Format("{0}: {1}{2}",
                                              inputName,
                                              inputValue.ToString("N" + displayPrecision),
                                              inputUnits);
            if (GUILayout.Button(buttonText, GeneralUI.buttonStyle, GUILayout.ExpandWidth(true)))
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
