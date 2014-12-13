using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.UI
{
    using Utility;

    static class PAMainWindow
    {
        internal static Rect window = new Rect(10, 50, 10, 10);

        internal static bool showPresets = false;

        internal static bool showPIDLimits = false;
        internal static bool showControlSurfaces = false;

        internal static string targetVert = "0";
        internal static string targetAlt = "0";
        internal static string targetHeading = "0";

        internal static bool bShowSettings = true;
        internal static bool bShowHdg = true;
        internal static bool bShowVert = true;

        public static void Draw()
        {
            GeneralUI.Styles();

            // GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.Width(0), GUILayout.Height(0));

            PAPresetWindow.presetWindow.x = window.x + window.width;
            PAPresetWindow.presetWindow.y = window.y;
            if (showPresets)
            {
                PAPresetWindow.Draw();
            }
        }

        private static void displayWindow(int id)
        {
            GUILayout.BeginVertical(GUILayout.Height(0), GUILayout.Width(0), GUILayout.ExpandHeight(true));
            if (PilotAssistant.bPause)
            {
                GUILayout.Label("CONTROL PAUSED", GeneralUI.labelAlertStyle, GUILayout.ExpandWidth(true));
            }
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            showPresets = GUILayout.Toggle(showPresets, "Presets", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            showPIDLimits = GUILayout.Toggle(showPIDLimits, "PID Limits", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Ctrl Surfaces", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

#region New heading
            // Heading
            GUILayout.BeginVertical(GeneralUI.guiSectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(PilotAssistant.bHdgActive, PilotAssistant.bHdgActive ? "On" : "Off", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(false)) != PilotAssistant.bHdgActive)
            {
                PilotAssistant.bHdgActive = !PilotAssistant.bHdgActive;
                PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.Label("Roll and Yaw Control", GeneralUI.boldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            bool tmpToggle1 = GUILayout.Toggle(!PilotAssistant.bWingLeveller, "Hdg control", GeneralUI.toggleButtonStyle);
            bool tmpToggle2 = GUILayout.Toggle(PilotAssistant.bWingLeveller, "Wing lvl", GeneralUI.toggleButtonStyle);
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.bWingLeveller = !PilotAssistant.bWingLeveller;
                
            GUILayout.EndHorizontal();
            if (!PilotAssistant.bWingLeveller)
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
                        PilotAssistant.controllers[(int)PIDList.HdgBank].SetPoint = newHdg;
                        PilotAssistant.controllers[(int)PIDList.HdgYaw].SetPoint = newHdg;
                        PilotAssistant.bHdgActive = PilotAssistant.bHdgWasActive = true; // skip toggle check to avoid being overwritten
                    }
                    else
                    {
                        // Bad input, reset UI to previous value
                        
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (!PilotAssistant.bWingLeveller)
            {
                drawPIDValuesNew(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0", false, true, false);
                drawPIDValuesNew(PIDList.HdgYaw, "Bank => Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false, false);
            }
            if (showControlSurfaces)
            {
                drawPIDValuesNew(PIDList.Aileron, "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0", false, true, false);
                drawPIDValuesNew(PIDList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0", false, true, false);
            }
            GUILayout.EndVertical();
#endregion New heading
#region New vertical
            // Vertical speed
            GUILayout.BeginVertical(GeneralUI.guiSectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(PilotAssistant.bVertActive, PilotAssistant.bVertActive ? "On" : "Off", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(false)) != PilotAssistant.bVertActive)
            {
                PilotAssistant.bVertActive = !PilotAssistant.bVertActive;
                if (!PilotAssistant.bVertActive)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.Label("Vertical Control", GeneralUI.boldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            tmpToggle1 = GUILayout.Toggle(PilotAssistant.bAltitudeHold, "Altitude", GeneralUI.toggleButtonStyle);
            tmpToggle2 = GUILayout.Toggle(!PilotAssistant.bAltitudeHold, "Vertical Speed", GeneralUI.toggleButtonStyle);
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.bAltitudeHold = !PilotAssistant.bAltitudeHold;
            
            GUILayout.EndHorizontal();

            if (PilotAssistant.bAltitudeHold)
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
                        PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                        PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
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
                        PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                        PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
                    }
                    else
                    {
                        // Bad input, reset UI value
                    }
                }
                GUILayout.EndHorizontal();
            }
            if (PilotAssistant.bAltitudeHold)
                drawPIDValuesNew(PIDList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true, true, false);
            drawPIDValuesNew(PIDList.VertSpeed, "Vertical Speed", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);
            if (showControlSurfaces)
                drawPIDValuesNew(PIDList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true, true, false);
            GUILayout.EndVertical();
#endregion New vertical

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private static void drawPIDValuesNew(PIDList controllerID, string inputName, string inputUnits, double inputValue,
                                             int displayPrecision, string outputName, string outputUnits,
                                             bool invertOutput = false, bool showTarget = true, bool doublesided = true)
        {
            PID.PID_Controller controller = PilotAssistant.controllers[(int)controllerID];
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
