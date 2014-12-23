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

        private static bool[] stockPIDDisplay = { false, false, false };

        private const string TEXT_FIELD_GROUP = "SASMainWindow";

        public static void Draw()
        {
            GeneralUI.Styles();

            windowRect = GUILayout.Window(78934856, windowRect, DrawSASWindow, "SAS Module", GUILayout.Width(0), GUILayout.Height(0));

            SASPresetWindow.windowRect.x = windowRect.x + windowRect.width;
            SASPresetWindow.windowRect.y = windowRect.y;

            if (SurfSAS.IsSSASMode())
            {
                if (SurfSAS.IsSSASOperational())
                    GUI.backgroundColor = GeneralUI.ActiveBackground;
                else
                    GUI.backgroundColor = GeneralUI.InActiveBackground;

                if (GUI.Button(new Rect(Screen.width / 2 + 50, Screen.height - 200, 50, 30), "SSAS"))
                {
                    SurfSAS.ToggleOperational();
                }
                GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            }

            if (showPresets)
                SASPresetWindow.Draw();
        }

        private static void DrawSASWindow(int id)
        {
            // Start a text field group.
            GeneralUI.StartTextFieldGroup(TEXT_FIELD_GROUP);
            
            bool isOperational = SurfSAS.IsSSASOperational() || SurfSAS.IsStockSASOperational();
            bool isSSASMode = SurfSAS.IsSSASMode();
            GUILayout.BeginHorizontal();
            showPresets = GUILayout.Toggle(showPresets, "Presets", GeneralUI.toggleButtonStyle);
            GUILayout.EndHorizontal();
            
            // SSAS/SAS
            GUILayout.BeginVertical(GeneralUI.guiSectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isOperational, isOperational ? "On" : "Off", GeneralUI.toggleButtonStyle, GUILayout.ExpandWidth(false)) != isOperational)
            {
                SurfSAS.ToggleOperational();
            }
            GUILayout.Label("SAS", GeneralUI.boldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            bool tmpToggle1 = GUILayout.Toggle(!isSSASMode, "Stock SAS", GeneralUI.toggleButtonStyle);
            bool tmpToggle2 = GUILayout.Toggle(isSSASMode, "SSAS", GeneralUI.toggleButtonStyle);
            // tmpToggle1 and tmpToggle2 are true when the user clicks the non-active mode, i.e. the mode changes. 
            if (tmpToggle1 && tmpToggle2)
                SurfSAS.ToggleSSASMode();
            
            GUILayout.EndHorizontal();

            if (isSSASMode)
            {
                double pitch = SurfSAS.GetController(SASList.Pitch).SetPoint;
                double roll = SurfSAS.GetController(SASList.Roll).SetPoint;
                double hdg = SurfSAS.GetController(SASList.Yaw).SetPoint;

                bool tmp1 = SurfSAS.IsSSASAxisEnabled(SASList.Pitch);
                bool tmp2 = SurfSAS.IsSSASAxisEnabled(SASList.Roll);
                bool tmp3 = SurfSAS.IsSSASAxisEnabled(SASList.Yaw);
                SurfSAS.GetController(SASList.Pitch).SetPoint = GeneralUI.TogPlusNumBox(TEXT_FIELD_GROUP, "Pitch:", ref tmp1, pitch, 80, 60, 80, -80);
                SurfSAS.GetController(SASList.Roll).SetPoint = GeneralUI.TogPlusNumBox(TEXT_FIELD_GROUP, "Roll:", ref tmp2, roll, 80, 60, 180, -180);
                SurfSAS.GetController(SASList.Yaw).SetPoint = GeneralUI.TogPlusNumBox(TEXT_FIELD_GROUP, "Heading:", ref tmp3, hdg, 80, 60, 360, 0);
                SurfSAS.SetSSASAxisEnabled(SASList.Pitch, tmp1);
                SurfSAS.SetSSASAxisEnabled(SASList.Roll, tmp2);
                SurfSAS.SetSSASAxisEnabled(SASList.Yaw, tmp3);

                drawPIDValues(SASList.Pitch, "Pitch");
                drawPIDValues(SASList.Roll, "Roll");
                drawPIDValues(SASList.Yaw, "Yaw");
            }
            else
            {
                VesselAutopilot.VesselSAS sas = Utility.FlightData.thisVessel.Autopilot.SAS;

                drawPIDValues(sas.pidLockedPitch, "Pitch", SASList.Pitch);
                drawPIDValues(sas.pidLockedRoll, "Roll", SASList.Roll);
                drawPIDValues(sas.pidLockedYaw, "Yaw", SASList.Yaw);
            }

            // Autolock vessel controls on focus.
            GeneralUI.AutolockTextFieldGroup(TEXT_FIELD_GROUP, ControlTypes.ALL_SHIP_CONTROLS | ControlTypes.TIMEWARP);

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
                controller.PGain = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Kp:", controller.PGain, "G3", 45);
                controller.IGain = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Ki:", controller.IGain, "G3", 45);
                controller.DGain = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Kd:", controller.DGain, "G3", 45);
                controller.Scalar = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Scalar:", controller.Scalar, "G3", 45);
            }
        }

        private static void drawPIDValues(PIDclamp controller, string inputName, SASList id)
        {
            if (GUILayout.Button(inputName, GeneralUI.buttonStyle, GUILayout.ExpandWidth(true)))
            {
                stockPIDDisplay[(int)id] = !stockPIDDisplay[(int)id];
            }

            if (stockPIDDisplay[(int)id])
            {
                controller.kp = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Kp:", controller.kp, "G3", 45);
                controller.ki = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Ki:", controller.ki, "G3", 45);
                controller.kd = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Kd:", controller.kd, "G3", 45);
                controller.clamp = GeneralUI.labPlusNumBox(TEXT_FIELD_GROUP, "Scalar:", controller.clamp, "G3", 45);
            }
        }
    }
}
