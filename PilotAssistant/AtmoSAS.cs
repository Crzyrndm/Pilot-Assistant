using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using AppLauncher;
    using UI;

    internal enum SASList
    {
        Pitch,
        Yaw,
        Roll
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class AtmoSAS : MonoBehaviour
    {
        internal List<PID_Controller> SASControllers = new List<PID_Controller>();

        internal bool bInit = false;
        internal bool bArmed = false;
        internal bool bActive = false;
        internal bool[] bPause = new bool[3]; // pause on a per axis basis
        internal bool bAtmosphere = false;

        internal Rect SASwindow = new Rect(350, 50, 245, 30);

        internal Vector2 scroll = new Vector2(0, 0);

        internal GUIStyle labelStyle;
        internal GUIStyle textStyle;
        internal GUIStyle btnStyle1;
        internal GUIStyle btnStyle2;

        public void Initialise()
        {
            // register vessel if not already
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            // grab stock PID values
            if (FlightData.thisVessel.VesselSAS.pidLockedPitch != null)
            {
                PIDclamp c = FlightData.thisVessel.VesselSAS.pidLockedPitch;
                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 5);
                SASControllers.Add(pitch);

                c = FlightData.thisVessel.VesselSAS.pidLockedYaw;
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 5);
                SASControllers.Add(yaw);

                c = FlightData.thisVessel.VesselSAS.pidLockedRoll;
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 5);
                SASControllers.Add(roll);

                bInit = true;
                bPause[0] = bPause[1] = bPause[2] = false;
            }
        }

        public void Update()
        {
            if (!bInit)
                Initialise();

            // SAS activated by user
            if (bArmed && !bActive && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bActive = true;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                updateTarget();
            }
            else if (bActive && GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bActive = false;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }

            // Atmospheric mode tracks horizon, don't want in space
            if (FlightData.thisVessel.staticPressure > 0)
                bAtmosphere = true;
            else
                bAtmosphere = false;

            pauseManager();
        }

        public void OnGUI()
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

            if (AppLauncherInstance.bDisplaySAS)
            {
                SASwindow = GUILayout.Window(78934856, SASwindow, drawSASWindow, "");
            }
            if (bActive)
                GUI.Box(new Rect(Screen.width / 2 + 100, Screen.height - 200, 55, 30), "Active");
        }

        public void FixedUpdate()
        {
            if (bInit && bActive)
            {
                FlightData.updateAttitude();

                float pitchResponse = -1 * (float)SASControllers[(int)SASList.Pitch].Response(FlightData.pitch);
                float yawResponse = -1 * (float)SASControllers[(int)SASList.Yaw].Response(FlightData.heading);
                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!bPause[(int)SASList.Pitch])
                    FlightData.thisVessel.ctrlState.pitch = pitchResponse * (float)Math.Cos(rollRad) + yawResponse * (float)Math.Sin(rollRad);

                if (!bPause[(int)SASList.Roll])
                    FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll);

                if (!bPause[(int)SASList.Yaw])
                    FlightData.thisVessel.ctrlState.yaw = pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad);
            }
        }

        private void updateTarget()
        {
            FlightData.updateAttitude();
            SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
            SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
        }

        private void drawSASWindow(int id)
        {
            if (GUILayout.Button(bArmed ? "Disarm SAS" : "Arm SAS"))
                bArmed = !bArmed;
            //GUILayout.Label("Atmospheric Mode: " + bAtmosphere.ToString());

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(MainWindow.window.height - 100), GUILayout.MinHeight(150));

            drawPIDvalues(SASControllers[(int)SASList.Pitch], "Pitch");
            drawPIDvalues(SASControllers[(int)SASList.Roll], "Roll");
            drawPIDvalues(SASControllers[(int)SASList.Yaw], "Yaw");

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private void pauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown())
                bPause[(int)SASList.Pitch] = true;
            if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = false;
                SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                bPause[(int)SASList.Roll] = true;
            if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Roll] = false;
                SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
            }

            if (GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
                bPause[(int)SASList.Yaw] = true;
            if (GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Yaw] = false;
                SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
            }

            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Yaw] = true;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Yaw] = false;
                updateTarget();
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
        }

        private void drawPIDvalues(PID.PID_Controller controller, string inputName)
        {
            GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(SASwindow.width - 50));

            controller.PGain = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString("G3"), 80);
            controller.IGain = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString("G3"), 80);
            controller.DGain = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString("G3"), 80);
            controller.Scalar = labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.Scalar.ToString("G3"), 80);
        }

        private double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
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