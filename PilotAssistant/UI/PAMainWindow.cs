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
        internal static string targetAlt = "0";
        internal static string targetHeading = "0";

        internal static bool bShowSettings = true;
        internal static bool bShowHdg = true;
        internal static bool bShowVert = true;

        internal static float hdgScrollHeight;
        internal static float vertScrollHeight;

        public static void Draw()
        {
            // DON'T WANT: GUI.skin = GeneralUI.UISkin;
            GUI.skin = HighLogic.Skin;
            GeneralUI.Styles();

            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.MinWidth(200));

            PAPresetWindow.presetWindow.x = window.x + window.width;
            PAPresetWindow.presetWindow.y = window.y;
            if (showPresets)
            {
                PAPresetWindow.Draw();
            }

            window.height = 0; // Force height recalculation
        }

        private static void displayWindow(int id)
        {
            // TODO: Move to GeneralUI
            GUIStyle mybox = new GUIStyle(GUI.skin.box);
            mybox.normal.textColor = mybox.focused.textColor = Color.white;
            mybox.hover.textColor = mybox.active.textColor = Color.yellow;
            mybox.onNormal.textColor = mybox.onFocused.textColor = mybox.onHover.textColor = mybox.onActive.textColor = Color.green;
            mybox.padding = new RectOffset(4, 4, 4, 4);
            GUIStyle mytoggle = new GUIStyle(GUI.skin.button);
            mytoggle.normal.textColor = mytoggle.focused.textColor = Color.white;
            mytoggle.hover.textColor = mytoggle.active.textColor = mytoggle.onActive.textColor = Color.yellow;
            mytoggle.onNormal.textColor = mytoggle.onFocused.textColor = mytoggle.onHover.textColor = Color.green;
            mytoggle.padding = new RectOffset(4, 4, 4, 4);
            GUIStyle TabLabelStyle = new GUIStyle(GUI.skin.label);
            TabLabelStyle.fontStyle = FontStyle.Bold;
            TabLabelStyle.alignment = TextAnchor.UpperCenter;
            // Probably the little "close" button. 
            /*if (GUILayout.Button(new Rect(window.width - 16, 2, 14, 14), ""))
            {
                AppLauncher.AppLauncherInstance.bDisplayAssistant = false;
            }*/

            GUILayout.BeginVertical(GUILayout.Height(200), GUILayout.Width(200), GUILayout.ExpandHeight(true));
            if (PilotAssistant.bPause)
            {
                GUILayout.Label("CONTROL PAUSED", GeneralUI.labelAlertStyle);
            }
            // GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (false && GUILayout.Button("Options", GUILayout.ExpandWidth(true), GUILayout.Height(30)))
            {
                bShowSettings = !bShowSettings;
            }
            GUILayout.BeginHorizontal();
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            showPresets = GUILayout.Toggle(showPresets, "Show/hide Presets", mytoggle, GUILayout.ExpandWidth(true));
            showPIDLimits = GUILayout.Toggle(showPIDLimits, "Show PID Limits", mytoggle, GUILayout.ExpandWidth(true));
            showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Show Control Surfaces", mytoggle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

#region New heading
            // Heading
            GUILayout.BeginVertical(mybox);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Roll and Yaw Control", TabLabelStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Toggle(PilotAssistant.bHdgActive, PilotAssistant.bHdgActive ? "On" : "Off", mytoggle, GUILayout.Height(30)) != PilotAssistant.bHdgActive)
            {
                PilotAssistant.bHdgActive = !PilotAssistant.bHdgActive;
                PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:", GUILayout.Width(40));
            bool tmpToggle1 = GUILayout.Toggle(!PilotAssistant.bWingLeveller, "Hdg control", mytoggle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            bool tmpToggle2 = GUILayout.Toggle(PilotAssistant.bWingLeveller, "Wing leveller", mytoggle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.bWingLeveller = !PilotAssistant.bWingLeveller;
                
            GUILayout.EndHorizontal();
            if (!PilotAssistant.bWingLeveller)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Hdg: ", GUILayout.ExpandWidth(true), GUILayout.Height(30));
                targetHeading = GUILayout.TextField(targetHeading, GUILayout.ExpandWidth(true), GUILayout.Height(30));
                if (GUILayout.Button("Set", GUILayout.ExpandWidth(true), GUILayout.Height(30)))
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
            GUILayout.BeginVertical(mybox);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Vertical Control", TabLabelStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Toggle(PilotAssistant.bVertActive, PilotAssistant.bVertActive ? "On" : "Off", mytoggle, GUILayout.Height(30)) != PilotAssistant.bVertActive)
            {
                PilotAssistant.bVertActive = !PilotAssistant.bVertActive;
                if (!PilotAssistant.bVertActive)
                    PilotAssistant.bPause = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                SurfSAS.bActive = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:", GUILayout.Width(40));
            tmpToggle1 = GUILayout.Toggle(PilotAssistant.bAltitudeHold, "Altitude", mytoggle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            tmpToggle2 = GUILayout.Toggle(!PilotAssistant.bAltitudeHold, "Vertical Speed", mytoggle, GUILayout.ExpandWidth(true), GUILayout.Height(30));
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                PilotAssistant.bAltitudeHold = !PilotAssistant.bAltitudeHold;
            
            GUILayout.EndHorizontal();

            if (PilotAssistant.bAltitudeHold)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Target Altitude: ", GUILayout.ExpandWidth(true), GUILayout.Height(30));
                targetAlt = GUILayout.TextField(targetAlt, GUILayout.ExpandWidth(true), GUILayout.Height(30));
                if (GUILayout.Button("Set", GUILayout.ExpandWidth(true), GUILayout.Height(30)))
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
                GUILayout.Label("Target Speed: ", GUILayout.ExpandWidth(true), GUILayout.Height(30));
                targetVert = GUILayout.TextField(targetVert, GUILayout.ExpandWidth(true), GUILayout.Height(30));
                if (GUILayout.Button("Set", GUILayout.ExpandWidth(true), GUILayout.Height(30)))
                {
                    ScreenMessages.PostScreenMessage("Target Speed updated");

                    double newVal;
                    if (double.TryParse(targetVert, out newVal))
                    {
                        PilotAssistant.controllers[(int)PIDList.Altitude].SetPoint = newVal;
                        PilotAssistant.bVertActive = PilotAssistant.bVertWasActive = true; // skip the toggle check so value isn't overwritten
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
            if (GUILayout.Button(buttonText, GUILayout.ExpandWidth(true), GUILayout.Height(30)))
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
