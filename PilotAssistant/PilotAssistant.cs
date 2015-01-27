using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;

namespace PilotAssistant
{
    using Presets;
    using Utility;
    using PID;

    [Flags]
    public enum PIDList
    {
        HdgBank,
        HdgYaw,
        Aileron,
        Rudder,
        Altitude,
        VertSpeed,
        Elevator
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistant : MonoBehaviour
    {
        private static PilotAssistant instance;
        public static PilotAssistant Instance 
        {
            get { return instance; }
        }

        static bool init = false; // create the default the first time through
        public static PID_Controller[] controllers = new PID_Controller[7];

        bool bPause = false;

        // RollController
        public bool bHdgActive = false;
        bool bHdgWasActive = false;
        // PitchController
        public bool bVertActive = false;
        bool bVertWasActive = false;
        // Altitude / vertical speed
        bool bAltitudeHold = false;
        bool bWasAltitudeHold = false;
        // Wing leveller / Heading control
        bool bWingLeveller = false;
        bool bWasWingLeveller = false;

        Rect window = new Rect(10, 50, 10, 10);

        Vector2 scrollbarHdg = Vector2.zero;
        Vector2 scrollbarVert = Vector2.zero;

        bool showPresets = false;

        bool showPIDLimits = false;
        bool showControlSurfaces = false;

        string targetVert = "0";
        string targetHeading = "0";

        bool bShowSettings = true;
        bool bShowHdg = true;
        bool bShowVert = true;

        float hdgScrollHeight;
        float vertScrollHeight;

        string newPresetName = "";
        Rect presetWindow = new Rect(0, 0, 200, 10);

        public void Start()
        {
            instance = this;
            
            if (!init)
                Initialise();

            PresetManager.loadCraftAsstPreset();
            
            // register vessel
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            // Init UI
            GeneralUI.InitColors();

            RenderingManager.AddToPostDrawQueue(5, drawGUI);
        }

        void Initialise()
        {
            controllers[(int)PIDList.HdgBank] = new PID.PID_Controller(2, 0.1, 0, -30, 30, -0.5, 0.5);
            controllers[(int)PIDList.HdgYaw] = new PID.PID_Controller(0, 0, 0.01, -2, 2, -0.5, 0.5);
            controllers[(int)PIDList.Aileron] = new PID.PID_Controller(0.02, 0.005, 0.01, -1, 1, -0.4, 0.4);
            controllers[(int)PIDList.Rudder] = new PID.PID_Controller(0.1, 0.08, 0.05, -1, 1, -0.4, 0.4);
            controllers[(int)PIDList.Altitude] = new PID.PID_Controller(0.15, 0.01, 0, -50, 50, -0.01, 0.01);
            controllers[(int)PIDList.VertSpeed] = new PID.PID_Controller(2, 0.8, 2, -10, 10, -5, 5);
            controllers[(int)PIDList.Elevator] = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.4, 0.4);

            // PID inits
            Utils.GetAsst(PIDList.Aileron).InMax = 180;
            Utils.GetAsst(PIDList.Aileron).InMin = -180;
            Utils.GetAsst(PIDList.Altitude).InMin = 0;

            // Set up a default preset that can be easily returned to
            if (PresetManager.Instance.craftPresetList.ContainsKey("default"))
            {
                if (PresetManager.Instance.craftPresetList["default"].PresetPA == null)
                    PresetManager.Instance.craftPresetList["default"].PresetPA = new PresetPA(controllers, "default");
            }
            else
                PresetManager.Instance.craftPresetList.Add("default", new CraftPreset("default", new PresetPA(controllers, "default"), null, null));

            PresetManager.saveDefaults();

            init = true;
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnAutopilotUpdate -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnAutopilotUpdate += new FlightInputCallback(vesselController);

            PresetManager.loadCraftAsstPreset();
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, drawGUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            PresetManager.saveToFile();
            bHdgActive = false;
            bVertActive = false;
        }

        public void Update()
        {
            keyPressChanges();

            if (IsPaused())
                return;

            if (bHdgActive != bHdgWasActive)
                hdgToggle();

            if (bVertActive != bVertWasActive)
                vertToggle();

            if (bAltitudeHold != bWasAltitudeHold)
                altToggle();

            if (bWingLeveller != bWasWingLeveller)
                wingToggle();
        }

        public void drawGUI()
        {
            if (!AppLauncherFlight.bDisplayAssistant)
                return;

            if (GeneralUI.UISkin == null)
                GeneralUI.UISkin = UnityEngine.GUI.skin;

            Draw();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            FlightData.updateAttitude();

            if (IsPaused())
                return;
            
            // Heading Control
            if (bHdgActive)
            {
                if (!bWingLeveller && (FlightData.thisVessel.latitude < 88 && FlightData.thisVessel.latitude > -88))
                {
                    if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading >= -180 && Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading <= 180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading);
                    }
                    else if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading < -180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading - 360);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading - 360);
                    }
                    else if (Utils.GetAsst(PIDList.HdgBank).SetPoint - FlightData.heading > 180)
                    {
                        Utils.GetAsst(PIDList.Aileron).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading + 360);
                        Utils.GetAsst(PIDList.HdgYaw).SetPoint = Utils.GetAsst(PIDList.HdgBank).ResponseD(FlightData.heading + 360);
                    }

                    Utils.GetAsst(PIDList.Rudder).SetPoint = -Utils.GetAsst(PIDList.HdgYaw).ResponseD(FlightData.yaw);
                }
                else
                {
                    bWasWingLeveller = true;
                    Utils.GetAsst(PIDList.Aileron).SetPoint = 0;
                    Utils.GetAsst(PIDList.Rudder).SetPoint = 0;
                }
                state.roll = (float)Utils.Clamp(Utils.GetAsst(PIDList.Aileron).ResponseF(FlightData.roll) + state.roll, -1, 1);
                state.yaw = Utils.GetAsst(PIDList.Rudder).ResponseF(FlightData.yaw);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    Utils.GetAsst(PIDList.VertSpeed).SetPoint = -Utils.GetAsst(PIDList.Altitude).ResponseD(FlightData.thisVessel.altitude);

                Utils.GetAsst(PIDList.Elevator).SetPoint = -Utils.GetAsst(PIDList.VertSpeed).ResponseD(FlightData.thisVessel.verticalSpeed);
                state.pitch = -Utils.GetAsst(PIDList.Elevator).ResponseF(FlightData.AoA);
            }
        }


        public static bool IsPaused() { return Instance.bPause || SASMonitor(); }

        private void hdgToggle()
        {
            bHdgWasActive = bHdgActive;
            if (bHdgActive)
            {
                Utils.GetAsst(PIDList.HdgBank).SetPoint = FlightData.heading;
                targetHeading = FlightData.heading.ToString("N2");

                bPause = false;
            }
            else
            {
                Utils.GetAsst(PIDList.HdgBank).Clear();
                Utils.GetAsst(PIDList.HdgYaw).Clear();
                Utils.GetAsst(PIDList.Aileron).Clear();
                Utils.GetAsst(PIDList.Rudder).Clear();
            }
        }

        private void vertToggle()
        {
            bVertWasActive = bVertActive;
            if (bVertActive)
            {
                if (bAltitudeHold)
                {
                    Utils.GetAsst(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                    targetVert = Utils.GetAsst(PIDList.Altitude).SetPoint.ToString("N1");
                }
                else
                {
                    Utils.GetAsst(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                    targetVert = Utils.GetAsst(PIDList.VertSpeed).SetPoint.ToString("N3");
                }

                bPause = false;
            }
            else
            {
                Utils.GetAsst(PIDList.Altitude).Clear();
                Utils.GetAsst(PIDList.HdgBank).Clear();
                Utils.GetAsst(PIDList.Elevator).Clear();
            }
        }

        private void altToggle()
        {
            bWasAltitudeHold = bAltitudeHold;
            if (bAltitudeHold)
            {
                Utils.GetAsst(PIDList.Altitude).SetPoint = FlightData.thisVessel.altitude;
                targetVert = Utils.GetAsst(PIDList.Altitude).SetPoint.ToString("N1");
            }
            else
            {
                Utils.GetAsst(PIDList.VertSpeed).SetPoint = FlightData.thisVessel.verticalSpeed;
                targetVert = Utils.GetAsst(PIDList.VertSpeed).SetPoint.ToString("N2");
            }
        }

        private void wingToggle()
        {
            bWasWingLeveller = bWingLeveller;
            if (!bWingLeveller)
            {
                Utils.GetAsst(PIDList.HdgBank).SetPoint = FlightData.heading;
                Utils.GetAsst(PIDList.HdgYaw).SetPoint = FlightData.heading;
                targetHeading = Utils.GetAsst(PIDList.HdgBank).SetPoint.ToString("N2");
            }
        }

        private void keyPressChanges()
        {
            if (InputLockManager.IsLocked(ControlTypes.ALL_SHIP_CONTROLS))
                return;

            bool mod = GameSettings.MODIFIER_KEY.GetKey();

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bHdgWasActive = false; // reset heading/vert lock on unpausing
                bVertWasActive = false;
                bPause = !bPause;
                if (!bPause)
                {
                    SurfSAS.setStockSAS(false);
                    SurfSAS.ActivitySwitch(false);
                }
                
                if (bPause)
                    Messaging.statusMessage(0);
                else
                    Messaging.statusMessage(1);
            }

            if (GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bHdgWasActive = false; // reset heading/vert lock on unpausing
                bVertWasActive = false;
            }

            if (mod && Input.GetKeyDown(KeyCode.X))
            {
                Utils.GetAsst(PIDList.VertSpeed).SetPoint = 0;
                bAltitudeHold = false;
                bWasAltitudeHold = false;
                bWingLeveller = true;
                targetVert = "0";
                Messaging.statusMessage(4);
            }

            if (!IsPaused())
            {
                double scale = mod ? 10 : 1;
                bool bFineControl = FlightInputHandler.fetch.precisionMode;
                if (bHdgActive)
                {
                    double hdg = double.Parse(targetHeading);
                    if (GameSettings.YAW_LEFT.GetKey())
                        hdg -= bFineControl ? 0.04 / scale : 0.4 * scale;
                    else if (GameSettings.YAW_RIGHT.GetKey())
                        hdg += bFineControl ? 0.04 / scale : 0.4 * scale;
                    else if (!GameSettings.AXIS_YAW.IsNeutral())
                        hdg += (bFineControl ? 0.04 / scale : 0.4 * scale) * GameSettings.AXIS_YAW.GetAxis();
                    
                    if (hdg < 0)
                        hdg += 360;
                    else if (hdg > 360)
                        hdg -= 360;
                    
                    Utils.GetAsst(PIDList.HdgBank).SetPoint = hdg;
                    Utils.GetAsst(PIDList.HdgYaw).SetPoint = hdg;
                    targetHeading = hdg.ToString();
                }

                if (bVertActive)
                {
                    double vert = double.Parse(targetVert);
                    if (GameSettings.PITCH_DOWN.GetKey())
                        vert -= bFineControl ? 0.04 / scale : 0.4 * scale;
                    else if (GameSettings.PITCH_UP.GetKey())
                        vert += bFineControl ? 0.04 / scale : 0.4 * scale;
                    else if (!GameSettings.AXIS_PITCH.IsNeutral())
                        vert += (bFineControl ? 0.04 / scale : 0.4 * scale) * GameSettings.AXIS_PITCH.GetAxis();

                    if (bAltitudeHold)
                    {
                        vert = Math.Max(vert * 10, 0);
                        Utils.GetAsst(PIDList.Altitude).SetPoint = vert;
                    }
                    else
                        Utils.GetAsst(PIDList.VertSpeed).SetPoint = vert;

                    targetVert = vert.ToString();
                }
            }
        }

        internal static bool SASMonitor()
        {
            return (FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] || SurfSAS.ActivityCheck());
        }

        #region GUI

        public void Draw()
        {
            GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            // Window resizing (scroll views dont work nicely with GUILayout)
            // Have to put the width changes before the draw so the close button is correctly placed
            if (showPIDLimits)
                window.width = 370;
            else
                window.width = 233;

            if (bShowHdg)
            {
                hdgScrollHeight = 0;
                if (!bWingLeveller)
                    hdgScrollHeight += 55;
                if ((Utils.GetAsst(PIDList.HdgBank).bShow || Utils.GetAsst(PIDList.HdgYaw).bShow) && !bWingLeveller)
                    hdgScrollHeight += 150;
                else if (showControlSurfaces)
                {
                    hdgScrollHeight += 50;
                    if (Utils.GetAsst(PIDList.Aileron).bShow || Utils.GetAsst(PIDList.Rudder).bShow)
                        hdgScrollHeight += 100;
                }
            }
            if (bShowVert)
            {
                vertScrollHeight = 38;
                if (bAltitudeHold)
                    vertScrollHeight += 27;
                if ((Utils.GetAsst(PIDList.Altitude).bShow && bAltitudeHold) || (Utils.GetAsst(PIDList.VertSpeed).bShow))
                    vertScrollHeight += 150;
                else if (showControlSurfaces)
                {
                    vertScrollHeight += 27;
                    if (Utils.GetAsst(PIDList.Elevator).bShow)
                        vertScrollHeight += 123;
                }
            }
            
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.Height(0), GUILayout.MinWidth(233));

            presetWindow.x = window.x + window.width;
            presetWindow.y = window.y;
            if (showPresets)
                presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "Pilot Assistant Presets", GUILayout.Width(200), GUILayout.Height(0));
        }

        private void displayWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                AppLauncherFlight.bDisplayAssistant = false;

            if (bPause || SASMonitor())
                GUILayout.Box("CONTROL PAUSED", GeneralUI.labelAlertStyle);

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button("Options", GUILayout.Width(205)))
            {
                bShowSettings = !bShowSettings;
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            if (bShowSettings)
            {
                showPresets = GUILayout.Toggle(showPresets, showPresets ? "Hide Presets" : "Show Presets", GUILayout.Width(200));

                showPIDLimits = GUILayout.Toggle(showPIDLimits, showPIDLimits ? "Hide PID Limits" : "Show PID Limits", GUILayout.Width(200));
                showControlSurfaces = GUILayout.Toggle(showControlSurfaces, showControlSurfaces ? "Hide Control Surfaces" : "Show Control Surfaces", GUILayout.Width(200));
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
            if (bHdgActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;

            bool toggleCheck = GUILayout.Toggle(bHdgActive, "");
            if (toggleCheck != bHdgActive)
            {
                bHdgActive = toggleCheck;
                bPause = false;
                SurfSAS.setStockSAS(false);
                SurfSAS.ActivitySwitch(false);
            }
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowHdg)
            {
                bWingLeveller = GUILayout.Toggle(bWingLeveller, bWingLeveller ? "Mode: Wing Leveller" : "Mode: Hdg Control", GUILayout.Width(200));
                if (!bWingLeveller)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Hdg: ", GUILayout.Width(98)))
                    {
                        ScreenMessages.PostScreenMessage("Target Heading updated");
                        double newHdg;
                        double.TryParse(targetHeading, out newHdg);
                        if (newHdg >= 0 && newHdg <= 360)
                        {
                            Utils.GetAsst(PIDList.HdgBank).SetPoint = newHdg;
                            Utils.GetAsst(PIDList.HdgYaw).SetPoint = newHdg;
                            bHdgActive = bHdgWasActive = true; // skip toggle check to avoid being overwritten
                        }
                    }
                    targetHeading = GUILayout.TextField(targetHeading, GUILayout.Width(98));
                    GUILayout.EndHorizontal();
                }

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GUILayout.Height(hdgScrollHeight));
                if (!bWingLeveller)
                {
                    drawPIDvalues(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0", false, true, false);
                    drawPIDvalues(PIDList.HdgYaw, "Bank => Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(PIDList.Aileron, "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0", false, true, false);
                    drawPIDvalues(PIDList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0", false, true, false);
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
            if (bVertActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;

            toggleCheck = GUILayout.Toggle(bVertActive, "");
            if (toggleCheck != bVertActive)
            {
                bVertActive = toggleCheck;
                if (!toggleCheck)
                    bPause = false;
                SurfSAS.setStockSAS(false);
                SurfSAS.ActivitySwitch(false);
            }
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowVert)
            {
                bAltitudeHold = GUILayout.Toggle(bAltitudeHold, bAltitudeHold ? "Mode: Altitude" : "Mode: Vertical Speed", GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(bAltitudeHold ? "Target Altitude:" : "Target Speed:", GUILayout.Width(98)))
                {
                    ScreenMessages.PostScreenMessage("Target " + (PilotAssistant.Instance.bAltitudeHold ? "Altitude" : "Vertical Speed") + " updated");

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (bAltitudeHold)
                        Utils.GetAsst(PIDList.Altitude).SetPoint = newVal;
                    else
                        Utils.GetAsst(PIDList.VertSpeed).SetPoint = newVal;

                    bVertActive = bVertWasActive = true; // skip the toggle check so value isn't overwritten
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GUILayout.Height(vertScrollHeight));

                if (bAltitudeHold)
                    drawPIDvalues(PIDList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true, true, false);
                drawPIDvalues(PIDList.VertSpeed, "Vertical Speed", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);

                if (showControlSurfaces)
                    drawPIDvalues(PIDList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true, true, false);

                GUILayout.EndScrollView();
            }
            #endregion

            GUI.DragWindow();
        }

        private void drawPIDvalues(PIDList controllerid, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showTarget = true, bool doublesided = true)
        {
            PID_Controller controller = Utils.GetAsst(controllerid);
            controller.bShow = GUILayout.Toggle(controller.bShow, string.Format("{0}: {1}{2}", inputName, inputValue.ToString("N" + displayPrecision.ToString()), inputUnits), GeneralUI.toggleButton, GUILayout.Width(window.width - 50));

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
                        controller.OutMin = -1 * GeneralUI.labPlusNumBox(string.Format("Max {0}{1}:", outputName, outputUnits), (-controller.OutMin).ToString("G3"));
                        if (doublesided)
                            controller.OutMax = -1 * GeneralUI.labPlusNumBox(string.Format("Min {0}{1}:", outputName, outputUnits), (-controller.OutMax).ToString("G3"));
                        else
                            controller.OutMax = -controller.OutMin;

                        controller.ClampUpper = -1 * GeneralUI.labPlusNumBox("I Clamp Lower:", (-controller.ClampUpper).ToString("G3"));
                        controller.ClampLower = -1 * GeneralUI.labPlusNumBox("I Clamp Upper:", (-controller.ClampLower).ToString("G3"));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private void displayPresetWindow(int id)
        {
            if (GUI.Button(new Rect(presetWindow.width - 16, 2, 14, 14), ""))
            {
                showPresets = false;
            }

            if (PresetManager.Instance.activePAPreset != null)
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activePAPreset.name));
                if (PresetManager.Instance.activePAPreset.name != "default")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.updatePAPreset(controllers);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newPAPreset(ref newPresetName, controllers);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadPAPreset(PresetManager.Instance.craftPresetList["default"].PresetPA);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (PresetPA p in PresetManager.Instance.PAPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadPAPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deletePAPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion
    }
}