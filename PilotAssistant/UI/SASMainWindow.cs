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
        private static bool[] ssasPIDDisplay = { false, false, false };

        private const string TEXT_FIELD_GROUP = "SASMainWindow";

        public static void Draw(bool show)
        {
            if (show)
            {
                GUI.skin = HighLogic.Skin;
                windowRect = GUILayout.Window(78934856, windowRect, DrawSASWindow, "SAS Module", GUILayout.Width(0), GUILayout.Height(0));
                
                SASPresetWindow.windowRect.x = windowRect.x + windowRect.width;
                SASPresetWindow.windowRect.y = windowRect.y;
                
                SASPresetWindow.Draw(showPresets);
            }
            else
            {
                GeneralUI.ClearLocks(TEXT_FIELD_GROUP);
                SASPresetWindow.Draw(false);
            }
        }

        private static void DrawSASWindow(int id)
        {
            // Start a text field group.
            GeneralUI.StartTextFieldGroup(TEXT_FIELD_GROUP);
            
            bool isOperational = SurfSAS.IsSSASOperational() || SurfSAS.IsStockSASOperational();
            bool isSSASMode = SurfSAS.IsSSASMode();
            GUILayout.BeginHorizontal();
            showPresets = GUILayout.Toggle(showPresets, "Presets", GeneralUI.ToggleButtonStyle);
            GUILayout.EndHorizontal();
            
            // SSAS/SAS
            GUILayout.BeginVertical(GeneralUI.GUISectionStyle, GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(isOperational, isOperational ? "On" : "Off", GeneralUI.ToggleButtonStyle, GUILayout.ExpandWidth(false)) != isOperational)
            {
                SurfSAS.ToggleOperational();
            }
            GUILayout.Label("SAS", GeneralUI.BoldLabelStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Mode:");
            bool tmpToggle1 = GUILayout.Toggle(!isSSASMode, "Stock SAS", GeneralUI.ToggleButtonStyle);
            bool tmpToggle2 = GUILayout.Toggle(isSSASMode, "SSAS", GeneralUI.ToggleButtonStyle);
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
                FlightData flightData = SurfSAS.GetFlightData();
                VesselAutopilot.VesselSAS sas = flightData.Vessel.Autopilot.SAS;

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
            if (GUILayout.Button(inputName, GeneralUI.ButtonStyle, GUILayout.ExpandWidth(true)))
                ssasPIDDisplay[(int)controllerID] = !ssasPIDDisplay[(int)controllerID]; 

            if (ssasPIDDisplay[(int)controllerID])
            {
                controller.PGain = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Kp:", controller.PGain, "F3", 45);
                controller.IGain = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Ki:", controller.IGain, "F3", 45);
                controller.DGain = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Kd:", controller.DGain, "F3", 45);
                controller.Scalar = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Scalar:", controller.Scalar, "F3", 45);
            }
        }

        private static void drawPIDValues(PIDclamp controller, string inputName, SASList id)
        {
            if (GUILayout.Button(inputName, GeneralUI.ButtonStyle, GUILayout.ExpandWidth(true)))
            {
                stockPIDDisplay[(int)id] = !stockPIDDisplay[(int)id];
            }

            if (stockPIDDisplay[(int)id])
            {
                controller.kp = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Kp:", controller.kp, "F3", 45);
                controller.ki = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Ki:", controller.ki, "F3", 45);
                controller.kd = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Kd:", controller.kd, "F3", 45);
                controller.clamp = GeneralUI.LabPlusNumBox(TEXT_FIELD_GROUP, "Scalar:", controller.clamp, "F3", 45);
            }
        }
    }
}
