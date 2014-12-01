using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using AppLauncher;
    using UI;
    using Presets;

    internal enum SASList
    {
        Pitch,
        Hdg,
        Roll
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class AtmoSAS : MonoBehaviour
    {
        internal static List<PID_Controller> SASControllers = new List<PID_Controller>();

        internal static bool bInit = false;
        internal static bool bArmed = false;
        internal static bool bActive = false;
        internal bool[] bPause = new bool[3]; // pause on a per axis basis
        internal bool bAtmosphere = false;
        internal static bool bStockSAS = false;
        internal bool bWasStockSAS = false;
        internal bool bShowPresets = false;

        internal Rect SASwindow = new Rect(350, 50, 200, 30);
        internal Rect SASPresetwindow = new Rect(550, 50, 50, 50);

        internal static string newPresetName = "";

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
                List<double[]> stockSAS = new List<double[]>();

                PIDclamp c = FlightData.thisVessel.VesselSAS.pidLockedPitch;
                double[] p1 = { c.kp, c.ki, c.kd, c.clamp };
                stockSAS.Add(p1);
                c = FlightData.thisVessel.VesselSAS.pidLockedYaw;
                double[] p2 = { c.kp, c.ki, c.kd, c.clamp };
                stockSAS.Add(p2);
                c = FlightData.thisVessel.VesselSAS.pidLockedRoll;
                double[] p3 = { c.kp, c.ki, c.kd, c.clamp };
                stockSAS.Add(p3);
                PresetManager.defaultStockSASTuning = new PresetSAS(stockSAS, "Stock", true);

                PID_Controller pitch = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(pitch);
                PID_Controller yaw = new PID.PID_Controller(0.15, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(yaw);
                PID_Controller roll = new PID.PID_Controller(0.1, 0.0, 0.06, -1, 1, -0.2, 0.2, 3);
                SASControllers.Add(roll);
                PresetManager.defaultSASTuning = new PresetSAS(SASControllers, "Default", false);

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
            if (FlightData.thisVessel.staticPressure > 0 && !bAtmosphere)
            {
                bAtmosphere = true;
                if (FlightData.thisVessel.ctrlState.killRot)
                {
                    bActive = true;
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                }
            }
            else if (FlightData.thisVessel.staticPressure == 0 && bAtmosphere)
            {
                bAtmosphere = false;
                if (bActive)
                {
                    bActive = false;
                    FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                }
            }

            if (bStockSAS)
                SASwindow.height = 440;
            else
                SASwindow.height = 550;

            if (bShowPresets)
            {
                SASPresetwindow.x = SASwindow.x + SASwindow.width;
                SASPresetwindow.y = SASwindow.y;
            }

            pauseManager(); // manage activation of SAS axes depending on user input
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
                SASwindow = GUI.Window(78934856, SASwindow, drawSASWindow, "");
            }
            if (this.bShowPresets)
                SASPresetwindow = GUILayout.Window(78934857, SASPresetwindow, drawPresetWindow, "");

            if (bArmed && bActive)
                GUI.Box(new Rect(Screen.width / 2 + 100, Screen.height - 200, 55, 30), "Active");
        }

        public void FixedUpdate()
        {
            if (bInit && bArmed && bActive)
            {
                FlightData.updateAttitude();

                float pitchResponse = -1 * (float)SASControllers[(int)SASList.Pitch].Response(FlightData.pitch);
                float yawResponse = -1 * (float)SASControllers[(int)SASList.Hdg].Response(FlightData.heading);
                double rollRad = Math.PI / 180 * FlightData.roll;

                if (!bPause[(int)SASList.Pitch])
                    FlightData.thisVessel.ctrlState.pitch = pitchResponse * (float)Math.Cos(rollRad) - yawResponse * (float)Math.Sin(rollRad);

                if (!bPause[(int)SASList.Roll])
                    FlightData.thisVessel.ctrlState.roll = (float)SASControllers[(int)SASList.Roll].Response(FlightData.roll);

                if (!bPause[(int)SASList.Hdg])
                    FlightData.thisVessel.ctrlState.yaw = pitchResponse * (float)Math.Sin(rollRad) + yawResponse * (float)Math.Cos(rollRad);
            }
        }

        private void updateTarget()
        {
            SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
            SASControllers[(int)SASList.Hdg].SetPoint = FlightData.heading;
            SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
        }

        private void drawSASWindow(int id)
        {
            if (GUILayout.Button("SAS Presets"))
            {
                bShowPresets = !bShowPresets;
            }

            bStockSAS = GUILayout.Toggle(bStockSAS, "Use Stock SAS");
            if (bStockSAS != bWasStockSAS)
            {
                bWasStockSAS = bStockSAS;
                if (bStockSAS)
                {
                    PresetManager.loadSASPreset(PresetManager.defaultStockSASTuning);
                    PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
                }
                else
                {
                    PresetManager.loadSASPreset(PresetManager.defaultSASTuning);
                    PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
                }
            }

            if (!bStockSAS)
            {
                if (GUILayout.Button(bArmed ? "Disarm SAS" : "Arm SAS"))
                    bArmed = !bArmed;
                //GUILayout.Label("Atmospheric Mode: " + bAtmosphere.ToString());

                SASControllers[(int)SASList.Pitch].SetPoint = (float)labPlusNumBox2("Pitch:", SASControllers[(int)SASList.Pitch].SetPoint.ToString("N2"), 80);
                SASControllers[(int)SASList.Hdg].SetPoint = (float)labPlusNumBox2("Heading:", SASControllers[(int)SASList.Hdg].SetPoint.ToString("N2"), 80);
                SASControllers[(int)SASList.Roll].SetPoint = (float)labPlusNumBox2("Roll:", SASControllers[(int)SASList.Roll].SetPoint.ToString("N2"), 80);

                drawPIDvalues(SASControllers[(int)SASList.Pitch], "Pitch");
                drawPIDvalues(SASControllers[(int)SASList.Roll], "Roll");
                drawPIDvalues(SASControllers[(int)SASList.Hdg], "Yaw");
            }
            else
            {
                VesselSAS sas = FlightData.thisVessel.VesselSAS;

                drawStockPIDvalues(sas.pidLockedPitch, "Pitch");
                drawStockPIDvalues(sas.pidLockedRoll, "Roll");
                drawStockPIDvalues(sas.pidLockedYaw, "Yaw");
            }
            GUI.DragWindow();
        }

        private void drawPresetWindow(int id)
        {
            if ((PresetManager.activeSASPreset != null && !bStockSAS) || (PresetManager.activeStockSASPreset != null && bStockSAS))
            {
                GUILayout.Label(string.Format("Active Preset: {0}", bStockSAS ? PresetManager.activeStockSASPreset.name : PresetManager.activeSASPreset.name));
                if (PresetManager.activeSASPreset.name != "Default" || PresetManager.activeStockSASPreset.name != "Stock")
                {
                    if (GUILayout.Button("Update Preset"))
                    {
                        if (bStockSAS)
                        {
                            List<double[]> stockSAS = new List<double[]>();

                            PIDclamp c = FlightData.thisVessel.VesselSAS.pidLockedPitch;
                            double[] p1 = { c.kp, c.ki, c.kd, c.clamp };
                            stockSAS.Add(p1);
                            c = FlightData.thisVessel.VesselSAS.pidLockedYaw;
                            double[] p2 = { c.kp, c.ki, c.kd, c.clamp };
                            stockSAS.Add(p2);
                            c = FlightData.thisVessel.VesselSAS.pidLockedRoll;
                            double[] p3 = { c.kp, c.ki, c.kd, c.clamp };
                            stockSAS.Add(p3);
                            PresetManager.activeStockSASPreset.Update(stockSAS);
                        }
                        else
                            PresetManager.activeSASPreset.Update(SASControllers);
                        PresetManager.saveCFG();
                    }
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
            {
                if (newPresetName != "")
                {
                    foreach (PresetSAS p in PresetManager.SASPresetList)
                    {
                        if (newPresetName == p.name)
                            return;
                    }

                    if (bStockSAS)
                    {
                        List<double[]> stockSAS = new List<double[]>();

                        PIDclamp c = FlightData.thisVessel.VesselSAS.pidLockedPitch;
                        double[] p1 = { c.kp, c.ki, c.kd, c.clamp };
                        stockSAS.Add(p1);
                        c = FlightData.thisVessel.VesselSAS.pidLockedYaw;
                        double[] p2 = { c.kp, c.ki, c.kd, c.clamp };
                        stockSAS.Add(p2);
                        c = FlightData.thisVessel.VesselSAS.pidLockedRoll;
                        double[] p3 = { c.kp, c.ki, c.kd, c.clamp };
                        stockSAS.Add(p3);
                        PresetManager.SASPresetList.Add(new PresetSAS(stockSAS, newPresetName, true));
                        PresetManager.activeStockSASPreset = PresetManager.SASPresetList[PresetManager.SASPresetList.Count - 1];
                    }
                    else
                    {
                        PresetManager.SASPresetList.Add(new PresetSAS(SASControllers, newPresetName, false));
                        PresetManager.activeSASPreset = PresetManager.SASPresetList[PresetManager.SASPresetList.Count - 1];
                    }
                    newPresetName = "";
                    PresetManager.saveCFG();
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
            {
                if (bStockSAS)
                {
                    PresetManager.loadStockSASPreset(PresetManager.defaultStockSASTuning);
                    PresetManager.activeStockSASPreset = PresetManager.defaultStockSASTuning;
                }
                else
                {
                    PresetManager.loadSASPreset(PresetManager.defaultSASTuning);
                    PresetManager.activeSASPreset = PresetManager.defaultSASTuning;
                }
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            // Stock Presets
            foreach (PresetSAS p in PresetManager.SASPresetList)
            {
                GUILayout.BeginHorizontal();
                if (!p.bStockSAS)
                    continue;

                if (GUILayout.Button(p.name))
                {
                    PresetManager.loadStockSASPreset(p);
                    PresetManager.activeStockSASPreset = p;
                }
                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    if (PresetManager.activeStockSASPreset == p)
                        PresetManager.activeStockSASPreset = null;
                    PresetManager.SASPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }

            // Atmo presets
            foreach (PresetSAS p in PresetManager.SASPresetList)
            {
                if (p.bStockSAS)
                    continue;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                {
                    PresetManager.loadSASPreset(p);
                    PresetManager.activeSASPreset = p;
                }
                if (GUILayout.Button("x", GUILayout.Width(25)))
                {
                    if (PresetManager.activeSASPreset == p)
                        PresetManager.activeSASPreset = null;
                    PresetManager.SASPresetList.Remove(p);
                    PresetManager.saveCFG();
                }
                GUILayout.EndHorizontal();
            }
        }

        private void pauseManager()
        {
            if (GameSettings.PITCH_DOWN.GetKeyDown() || GameSettings.PITCH_UP.GetKeyDown() || GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = true;
                bPause[(int)SASList.Hdg] = true;
            }
            if (GameSettings.PITCH_DOWN.GetKeyUp() || GameSettings.PITCH_UP.GetKeyUp() || GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = false;
                bPause[(int)SASList.Hdg] = false;
                SASControllers[(int)SASList.Pitch].SetPoint = FlightData.pitch;
                SASControllers[(int)SASList.Hdg].SetPoint = FlightData.heading;
            }

            if (GameSettings.ROLL_LEFT.GetKeyDown() || GameSettings.ROLL_RIGHT.GetKeyDown())
                bPause[(int)SASList.Roll] = true;
            if (GameSettings.ROLL_LEFT.GetKeyUp() || GameSettings.ROLL_RIGHT.GetKeyUp())
            {
                bPause[(int)SASList.Roll] = false;
                SASControllers[(int)SASList.Roll].SetPoint = FlightData.roll;
            }

            //if (GameSettings.YAW_LEFT.GetKeyDown() || GameSettings.YAW_RIGHT.GetKeyDown())
            //    bPause[(int)SASList.Yaw] = true;
            //if (GameSettings.YAW_LEFT.GetKeyUp() || GameSettings.YAW_RIGHT.GetKeyUp())
            //{
            //    bPause[(int)SASList.Yaw] = false;
            //    SASControllers[(int)SASList.Yaw].SetPoint = FlightData.heading;
            //}

            if (GameSettings.SAS_HOLD.GetKeyDown())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Hdg] = true;
                FlightData.thisVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
            }
            if (GameSettings.SAS_HOLD.GetKeyUp())
            {
                bPause[(int)SASList.Pitch] = bPause[(int)SASList.Roll] = bPause[(int)SASList.Hdg] = false;
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

        private void drawStockPIDvalues(PIDclamp controller, string inputName)
        {
            GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(SASwindow.width - 50));

            controller.kp = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.kp.ToString("G3"), 80);
            controller.ki = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.ki.ToString("G3"), 80);
            controller.kd = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.kd.ToString("G3"), 80);
            controller.clamp = labPlusNumBox(string.Format("{0} Scalar: ", inputName), controller.clamp.ToString("G3"), 80);
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

        private double labPlusNumBox2(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
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
            }
            if (GUILayout.Button("-", btnStyle2, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val -= 1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }
    }
}