using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using KSP.IO;

namespace PilotAssistant
{
    using Presets;
    using Utility;
    using PID;

    public enum PIDList
    {
        HdgBank,
        HdgYaw,
        Aileron,
        Rudder,
        Altitude,
        VertSpeed,
        Elevator,
        Throttle
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
        public static PID_Controller[] controllers = new PID_Controller[8];

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
        // Throttle control
        bool bThrottleActive = false;
        bool bWasThrottleActive = false;

        public Rect window = new Rect(10, 130, 10, 10);

        Vector2 scrollbarHdg = Vector2.zero;
        Vector2 scrollbarVert = Vector2.zero;

        internal bool showPresets = false;
        internal bool showPIDLimits = false;
        internal bool showControlSurfaces = false;
        internal bool doublesided = false;
        internal bool showTooltips = true;

        string targetVert = "0";
        string targetHeading = "0";
        string targetSpeed = "0";

        bool bShowSettings = false;
        bool bShowHdg = true;
        bool bShowVert = true;
        bool bShowThrottle = true;

        float hdgScrollHeight;
        float vertScrollHeight;

        string newPresetName = "";
        Rect presetWindow = new Rect(0, 0, 200, 10);

        public static double[] defaultHdgBankGains = { 2, 0.1, 0, -30, 30, -0.5, 0.5, 1, 1 };
        public static double[] defaultHdgYawGains = { 0, 0, 0.01, -2, 2, -0.5, 0.5, 1, 1 };
        public static double[] defaultAileronGains = { 0.02, 0.005, 0.01, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultRudderGains = { 0.1, 0.08, 0.05, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultAltitudeGains = { 0.15, 0.01, 0, -50, 50, -0.01, 0.01, 1, 100 };
        public static double[] defaultVSpeedGains = { 2, 0.8, 2, -10, 10, -5, 5, 1, 10 };
        public static double[] defaultElevatorGains = { 0.05, 0.01, 0.1, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultThrottleGains = { 0.2, 0.08, 0.1, -1, 0, -1, 0.4, 1, 1 };

        public void Start()
        {
            instance = this;
            
            if (!init)
                Initialise();

            // register vessel
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            PresetManager.loadCraftAsstPreset();

            Utils.GetAsst(PIDList.Aileron).InMax = 180;
            Utils.GetAsst(PIDList.Aileron).InMin = -180;
            Utils.GetAsst(PIDList.Altitude).InMin = 0;
            Utils.GetAsst(PIDList.Throttle).InMin = 0;

            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            RenderingManager.AddToPostDrawQueue(5, drawGUI);
        }

        void Initialise()
        {
            controllers[(int)PIDList.HdgBank] = new PID_Controller(defaultHdgBankGains);
            controllers[(int)PIDList.HdgYaw] = new PID_Controller(defaultHdgYawGains);
            controllers[(int)PIDList.Aileron] = new PID_Controller(defaultAileronGains);
            controllers[(int)PIDList.Rudder] = new PID_Controller(defaultRudderGains);
            controllers[(int)PIDList.Altitude] = new PID_Controller(defaultAltitudeGains);
            controllers[(int)PIDList.VertSpeed] = new PID_Controller(defaultVSpeedGains);
            controllers[(int)PIDList.Elevator] = new PID_Controller(defaultElevatorGains);
            controllers[(int)PIDList.Throttle] = new PID_Controller(defaultThrottleGains);

            // Set up a default preset that can be easily returned to
            if (PresetManager.Instance.craftPresetList.ContainsKey("default"))
            {
                if (PresetManager.Instance.craftPresetList["default"].AsstPreset == null)
                    PresetManager.Instance.craftPresetList["default"].AsstPreset = new AsstPreset(controllers, "default");
            }
            else
                PresetManager.Instance.craftPresetList.Add("default", new CraftPreset("default", new AsstPreset(controllers, "default"), null, null, true));

            PresetManager.saveDefaults();

            init = true;
        }

        private void vesselSwitch(Vessel v)
        {
            FlightData.thisVessel.OnPostAutopilotUpdate -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(vesselController);

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

            if (bThrottleActive != bWasThrottleActive)
                throttleToggle();
        }

        public void drawGUI()
        {
            if (!AppLauncherFlight.bDisplayAssistant)
                return;

            GUI.skin = GeneralUI.UISkin;
            GeneralUI.Styles();

            // Window resizing (scroll views dont work nicely with GUILayout)
            // Have to put the width changes before the draw so the close button is correctly placed
            float width;
            if (showPIDLimits)
                width = 370;
            else
                width = 238;

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
            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.Height(0), GUILayout.Width(width));

            if (tooltip != "" && showTooltips)
                GUILayout.Window(34246, new Rect(window.x + window.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));

            presetWindow.x = window.x + window.width;
            presetWindow.y = window.y;
            if (showPresets)
                presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "Pilot Assistant Presets", GUILayout.Width(200), GUILayout.Height(0));
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
                state.yaw = Utils.GetAsst(PIDList.Rudder).ResponseF(FlightData.yaw);

                float rollInput = 0;
                if (GameSettings.ROLL_LEFT.GetKey())
                    rollInput = -1;
                else if (GameSettings.ROLL_RIGHT.GetKey())
                    rollInput = 1;
                else if (!GameSettings.AXIS_ROLL.IsNeutral())
                    rollInput = GameSettings.AXIS_ROLL.GetAxis();
                if (FlightInputHandler.fetch.precisionMode)
                    rollInput *= 0.33f;
                state.roll = Mathf.Clamp(Utils.GetAsst(PIDList.Aileron).ResponseF(FlightData.roll) + rollInput, -1, 1);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    Utils.GetAsst(PIDList.VertSpeed).SetPoint = -Utils.GetAsst(PIDList.Altitude).ResponseD(FlightData.thisVessel.altitude);

                Utils.GetAsst(PIDList.Elevator).SetPoint = -Utils.GetAsst(PIDList.VertSpeed).ResponseD(FlightData.thisVessel.verticalSpeed);
                state.pitch = -Utils.GetAsst(PIDList.Elevator).ResponseF(FlightData.AoA);
            }
            if (bThrottleActive)
            {
                state.mainThrottle = -Utils.GetAsst(PIDList.Throttle).ResponseF(FlightData.thisVessel.srfSpeed);
            }
        }


        public static bool IsPaused() { return Instance.bPause; }

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
                targetHeading = Utils.GetAsst(PIDList.HdgBank).SetPoint.ToString("N2");
            }
        }

        private void throttleToggle()
        {
            bWasThrottleActive = bThrottleActive;
            if (bThrottleActive)
            {
                Utils.GetAsst(PIDList.Throttle).SetPoint = FlightData.thisVessel.srfSpeed;
                targetSpeed = Utils.GetAsst(PIDList.Throttle).SetPoint.ToString("N1");
            }
            else
                Utils.GetAsst(PIDList.Throttle).Clear();
        }

        private void keyPressChanges()
        {
            bool mod = GameSettings.MODIFIER_KEY.GetKey();

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                bHdgWasActive = false; // reset locks on unpausing
                bVertWasActive = false;
                bWasThrottleActive = false;

                bPause = !bPause;
                
                if (bPause)
                    Messaging.statusMessage(0);
                else
                    Messaging.statusMessage(1);
            }
            if (Utils.isFlightControlLocked())
                return;
            
            // update targets
            if (GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                bHdgWasActive = false;
                bVertWasActive = false;
            }

            if (mod && Input.GetKeyDown(KeyCode.X))
            {
                Utils.GetAsst(PIDList.VertSpeed).SetPoint = 0;
                Utils.GetAsst(PIDList.Throttle).SetPoint = FlightData.thisVessel.srfSpeed;
                bAltitudeHold = false;
                bWasAltitudeHold = false;
                bWingLeveller = true;
                targetVert = "0";
                targetSpeed = FlightData.thisVessel.srfSpeed.ToString("F2");
                Messaging.statusMessage(4);
            }

            if (Input.GetKeyDown(KeyCode.Keypad9) && GameSettings.MODIFIER_KEY.GetKey())
                bHdgActive = !bHdgActive;
            if (Input.GetKeyDown(KeyCode.Keypad6) && GameSettings.MODIFIER_KEY.GetKey())
                bVertActive = !bVertActive;
            if (Input.GetKeyDown(KeyCode.Keypad3) && GameSettings.MODIFIER_KEY.GetKey())
                bThrottleActive = !bThrottleActive;

            if (!IsPaused())
            {
                double scale = mod ? 10 : 1;
                bool bFineControl = FlightInputHandler.fetch.precisionMode;
                if (bHdgActive && (GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || !GameSettings.AXIS_YAW.IsNeutral()))
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

                if (bVertActive && (GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !GameSettings.AXIS_PITCH.IsNeutral()))
                {
                    double vert = double.Parse(targetVert);
                    if (bAltitudeHold)
                        vert /= 10;

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

                if (bThrottleActive && (GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey()) || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown())
                {
                    double speed = double.Parse(targetSpeed);

                    if (GameSettings.THROTTLE_UP.GetKey())
                        speed += bFineControl ? 0.1 / scale : 1 * scale;
                    else if (GameSettings.THROTTLE_DOWN.GetKey())
                        speed -= bFineControl ? 0.1 / scale : 1 * scale;

                    if (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey())
                        speed = 0;
                    if (GameSettings.THROTTLE_FULL.GetKeyDown())
                        speed = 2400;

                    Utils.GetAsst(PIDList.Throttle).SetPoint = speed;

                    targetSpeed = Math.Max(speed, 0).ToString();
                }
            }
        }

        internal static bool SASMonitor()
        {
            return (FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] || SurfSAS.ActivityCheck());
        }

        #region GUI
        private void displayWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                AppLauncherFlight.bDisplayAssistant = false;

            if (IsPaused())
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
                doublesided = GUILayout.Toggle(doublesided, "Separate Min and Max limits", GUILayout.Width(200));
                showTooltips = GUILayout.Toggle(showTooltips, "Show Tooltips", GUILayout.Width(200));
            }

            #region Hdg GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowHdg = GUILayout.Toggle(bShowHdg, "", GeneralUI.toggleButton, GUILayout.Width(20));

            if (bHdgActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Roll and Yaw Control", GUILayout.Width(186)))
            {
                bHdgActive = !bHdgActive;
                bPause = false;
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
                        double newHdg;
                        if (double.TryParse(targetHeading, out newHdg) && newHdg >= 0 && newHdg <= 360)
                        {
                            Utils.GetAsst(PIDList.HdgBank).BumplessSetPoint = newHdg;
                            bHdgActive = bHdgWasActive = true; // skip toggle check to avoid being overwritten
                        }
                    }
                    targetHeading = GUILayout.TextField(targetHeading, GUILayout.Width(98));
                    GUILayout.EndHorizontal();
                }

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GUILayout.Height(hdgScrollHeight));
                if (!bWingLeveller)
                {
                    drawPIDvalues(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0");
                    drawPIDvalues(PIDList.HdgYaw, "Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(PIDList.Aileron, "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0");
                    drawPIDvalues(PIDList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0");
                }
                GUILayout.EndScrollView();

                Utils.GetAsst(PIDList.Aileron).OutMin = Math.Min(Math.Max(Utils.GetAsst(PIDList.Aileron).OutMin, -1), 1);
                Utils.GetAsst(PIDList.Aileron).OutMax = Math.Min(Math.Max(Utils.GetAsst(PIDList.Aileron).OutMax, -1), 1);

                Utils.GetAsst(PIDList.Rudder).OutMin = Math.Min(Math.Max(Utils.GetAsst(PIDList.Rudder).OutMin, -1), 1);
                Utils.GetAsst(PIDList.Rudder).OutMax = Math.Min(Math.Max(Utils.GetAsst(PIDList.Rudder).OutMax, -1), 1);
            }
            #endregion

            #region Pitch GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowVert = GUILayout.Toggle(bShowVert, "", GeneralUI.toggleButton, GUILayout.Width(20));

            if (bVertActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Vertical Control", GUILayout.Width(186)))
            {
                bVertActive = !bVertActive;
                bPause = false;
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
                        Utils.GetAsst(PIDList.Altitude).BumplessSetPoint = newVal;
                    else
                        Utils.GetAsst(PIDList.VertSpeed).BumplessSetPoint = newVal;

                    bVertActive = bVertWasActive = true; // skip the toggle check so value isn't overwritten
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GUILayout.Height(vertScrollHeight));

                if (bAltitudeHold)
                    drawPIDvalues(PIDList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true);
                drawPIDvalues(PIDList.VertSpeed, "Vertical Speed", "m/s", FlightData.thisVessel.verticalSpeed, 2, "AoA", "\u00B0", true);

                if (showControlSurfaces)
                    drawPIDvalues(PIDList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true);

                Utils.GetAsst(PIDList.Elevator).OutMin = Math.Min(Math.Max(Utils.GetAsst(PIDList.Elevator).OutMin, -1), 1);
                Utils.GetAsst(PIDList.Elevator).OutMax = Math.Min(Math.Max(Utils.GetAsst(PIDList.Elevator).OutMax, -1), 1);

                GUILayout.EndScrollView();
            }
            #endregion

            #region Throttle GUI

            GUILayout.BeginHorizontal();
            // button background
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowThrottle = GUILayout.Toggle(bShowThrottle, "", GeneralUI.toggleButton, GUILayout.Width(20));
            if (bThrottleActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Throttle Control", GUILayout.Width(186)))
            {
                bThrottleActive = !bThrottleActive;
                if (!bThrottleActive)
                    bPause = false;
            }
            
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowThrottle)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Target Speed:", GUILayout.Width(118)))
                {
                    ScreenMessages.PostScreenMessage("Target Speed updated");

                    double newVal;
                    double.TryParse(targetSpeed, out newVal);
                    Utils.GetAsst(PIDList.Throttle).BumplessSetPoint = newVal;

                    bThrottleActive = bWasThrottleActive = true; // skip the toggle check so value isn't overwritten
                }
                targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                drawPIDvalues(PIDList.Throttle, "Speed", "m/s", FlightData.thisVessel.srfSpeed, 2, "Throttle", "", true);
                // can't have people bugging things out now can we...
                Utils.GetAsst(PIDList.Throttle).OutMin = Math.Min(Math.Max(Utils.GetAsst(PIDList.Throttle).OutMin, -1), 0);
                Utils.GetAsst(PIDList.Throttle).OutMax = Math.Min(Math.Max(Utils.GetAsst(PIDList.Throttle).OutMax, -1), 0);
            }

            #endregion

            GUI.DragWindow();
            if (Event.current.type == EventType.Repaint)
                tooltip = GUI.tooltip;
        }

        
        string OutMaxTooltip = "The absolute maximum value the controller can output";
        string OutMinTooltip = "The absolute minimum value the controller can output";

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDvalues(PIDList controllerid, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showTarget = true)
        {
            PID_Controller controller = Utils.GetAsst(controllerid);
            controller.bShow = GUILayout.Toggle(controller.bShow, string.Format("{0}: {1}{2}", inputName, inputValue.ToString("N" + displayPrecision.ToString()), inputUnits), GeneralUI.toggleButton, GUILayout.Width(200));

            if (controller.bShow)
            {
                if (showTarget)
                    GUILayout.Label("Target: " + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();

                controller.PGain = GeneralUI.labPlusNumBox(GeneralUI.KpLabel, controller.PGain.ToString("G3"), 45);
                controller.IGain = GeneralUI.labPlusNumBox(GeneralUI.KiLabel, controller.IGain.ToString("G3"), 45);
                controller.DGain = GeneralUI.labPlusNumBox(GeneralUI.KdLabel, controller.DGain.ToString("G3"), 45);
                controller.Scalar = GeneralUI.labPlusNumBox(GeneralUI.ScalarLabel, controller.Scalar.ToString("G3"), 45);

                if (showPIDLimits)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (!invertOutput)
                    {
                        controller.OutMax = GeneralUI.labPlusNumBox(new GUIContent(string.Format("Max {0}{1}:", outputName, outputUnits), OutMaxTooltip), controller.OutMax.ToString("G3"));
                        if (doublesided)
                            controller.OutMin = GeneralUI.labPlusNumBox(new GUIContent(string.Format("Min {0}{1}:", outputName, outputUnits), OutMinTooltip), controller.OutMin.ToString("G3"));
                        else
                            controller.OutMin = -controller.OutMax;
                        if (doublesided)
                            controller.ClampLower = GeneralUI.labPlusNumBox(GeneralUI.IMinLabel, controller.ClampLower.ToString("G3"));
                        else
                            controller.ClampLower = -controller.ClampUpper;
                        controller.ClampUpper = GeneralUI.labPlusNumBox(GeneralUI.IMaxLabel, controller.ClampUpper.ToString("G3"));

                        controller.Easing = GeneralUI.labPlusNumBox(GeneralUI.EasingLabel, controller.Easing.ToString("G3"));
                    }
                    else
                    { // used when response * -1 is used to get the correct output
                        controller.OutMin = -1 * GeneralUI.labPlusNumBox(new GUIContent(string.Format("Max {0}{1}:", outputName, outputUnits), OutMaxTooltip), (-controller.OutMin).ToString("G3"));
                        if (doublesided)
                            controller.OutMax = -1 * GeneralUI.labPlusNumBox(new GUIContent(string.Format("Min {0}{1}:", outputName, outputUnits), OutMinTooltip), (-controller.OutMax).ToString("G3"));
                        else
                            controller.OutMax = -controller.OutMin;

                        if (doublesided)
                            controller.ClampUpper = -1 * GeneralUI.labPlusNumBox(GeneralUI.IMinLabel, (-controller.ClampUpper).ToString("G3"));
                        else
                            controller.ClampUpper = -controller.ClampLower;
                        controller.ClampLower = -1 * GeneralUI.labPlusNumBox(GeneralUI.IMaxLabel, (-controller.ClampLower).ToString("G3"));

                        controller.Easing = GeneralUI.labPlusNumBox(GeneralUI.EasingLabel, controller.Easing.ToString("G3"));
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
                PresetManager.loadPAPreset(PresetManager.Instance.craftPresetList["default"].AsstPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (AsstPreset p in PresetManager.Instance.PAPresetList)
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