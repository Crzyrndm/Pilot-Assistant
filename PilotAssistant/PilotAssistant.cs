using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
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
        BankToYaw,
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
        public static double[] defaultBankToYawGains = { 0, 0, 0.01, -2, 2, -0.5, 0.5, 1, 1 };
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

            PIDList.Aileron.GetAsst().InMax = 180;
            PIDList.Aileron.GetAsst().InMin = -180;
            PIDList.Altitude.GetAsst().InMin = 0;
            PIDList.Throttle.GetAsst().InMin = 0;

            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpHandler);

            RenderingManager.AddToPostDrawQueue(5, drawGUI);
        }

        void Initialise()
        {
            controllers[(int)PIDList.HdgBank] = new PID_Controller(defaultHdgBankGains);
            controllers[(int)PIDList.BankToYaw] = new PID_Controller(defaultBankToYawGains);
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

        private void warpHandler()
        {
            FlightData.updateAttitude();
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
                bHdgWasActive = bVertWasActive = bWasThrottleActive = false;
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, drawGUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);
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

            if (bHdgActive)
            {
                PIDList.HdgBank.GetAsst().SetPoint = calculateTargetHeading();

                if (!headingEdit)
                    targetHeading = PIDList.HdgBank.GetAsst().SetPoint.ToString("N2");
            }
        }

        public void drawGUI()
        {
            if (!AppLauncherFlight.bDisplayAssistant)
                return;

            GUI.skin = GeneralUI.UISkin;

            // Window resizing (scroll views dont work nicely with GUILayout)
            // Have to put the width changes before the draw so the close button is correctly placed
            float width;
            if (showPIDLimits && controllers.Any(c => c.bShow)) // use two column view if show limits option and a controller is open
                width = 370;
            else
                width = 240;

            if (bShowHdg)
            {
                hdgScrollHeight = 0; // no controllers visible when in wing lvl mode unless ctrl surf's are there
                if (!bWingLeveller)
                    hdgScrollHeight += 55; // hdg & yaw headers
                if ((PIDList.HdgBank.GetAsst().bShow || PIDList.BankToYaw.GetAsst().bShow) && !bWingLeveller)
                    hdgScrollHeight += 150; // open controller
                else if (showControlSurfaces)
                {
                    hdgScrollHeight += 50; // aileron and rudder headers
                    if (PIDList.Aileron.GetAsst().bShow || PIDList.Rudder.GetAsst().bShow)
                        hdgScrollHeight += 100; // open controller
                }
            }
            if (bShowVert)
            {
                vertScrollHeight = 38; // Vspeed header
                if (bAltitudeHold)
                    vertScrollHeight += 27; // altitude header
                if ((PIDList.Altitude.GetAsst().bShow && bAltitudeHold) || PIDList.VertSpeed.GetAsst().bShow)
                    vertScrollHeight += 150; // open  controller
                else if (showControlSurfaces)
                {
                    vertScrollHeight += 27; // elevator header
                    if (PIDList.Elevator.GetAsst().bShow)
                        vertScrollHeight += 123; // open controller
                }
            }
            // main window
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.Height(0), GUILayout.Width(width));
            // tooltip window. Label skin is transparent so it's only drawing what's inside it
            if (tooltip != "" && showTooltips)
                GUILayout.Window(34246, new Rect(window.x + window.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));

            if (showPresets)
            {
                // move the preset window to sit to the right of the main window, with the tops level
                presetWindow.x = window.x + window.width;
                presetWindow.y = window.y;

                presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "Pilot Assistant Presets", GUILayout.Width(200), GUILayout.Height(0));
            }
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
                if (!bWingLeveller)
                {
                    // calculate the bank angle response based on the current heading
                    double hdgBankResponse = PIDList.HdgBank.GetAsst().ResponseD(CurrentAngleTargetRel(FlightData.heading, Utils.GetAsst(PIDList.HdgBank).SetPoint));
                    // aileron setpoint updated, bank angle also used for yaw calculations (don't go direct to rudder because we want yaw stabilisation *or* turn assistance)
                    PIDList.BankToYaw.GetAsst().SetPoint = PIDList.Aileron.GetAsst().SetPoint = hdgBankResponse;
                    PIDList.Rudder.GetAsst().SetPoint = -PIDList.BankToYaw.GetAsst().ResponseD(FlightData.yaw);
                }
                else
                {
                    bWasWingLeveller = true;
                    PIDList.Aileron.GetAsst().SetPoint = 0;
                    PIDList.Rudder.GetAsst().SetPoint = 0;
                }
                state.yaw = Mathf.Clamp(PIDList.Rudder.GetAsst().ResponseF(FlightData.yaw), -1, 1);

                float rollInput = 0;
                if (GameSettings.ROLL_LEFT.GetKey())
                    rollInput = -1;
                else if (GameSettings.ROLL_RIGHT.GetKey())
                    rollInput = 1;
                else if (!GameSettings.AXIS_ROLL.IsNeutral())
                    rollInput = GameSettings.AXIS_ROLL.GetAxis();
                if (FlightInputHandler.fetch.precisionMode)
                    rollInput *= 0.33f;
                state.roll = Mathf.Clamp(PIDList.Aileron.GetAsst().ResponseF(FlightData.roll) + rollInput, -1, 1);
            }

            if (bVertActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    PIDList.VertSpeed.GetAsst().SetPoint = -PIDList.Altitude.GetAsst().ResponseD(FlightData.thisVessel.altitude);

                PIDList.Elevator.GetAsst().SetPoint = -PIDList.VertSpeed.GetAsst().ResponseD(FlightData.vertSpeed);
                state.pitch = Mathf.Clamp(-PIDList.Elevator.GetAsst().ResponseF(FlightData.AoA), -1, 1);
            }
            if (bThrottleActive)
            {
                state.mainThrottle = Mathf.Clamp(-PIDList.Throttle.GetAsst().ResponseF(FlightData.thisVessel.srfSpeed), 0, 1);
            }
        }

        /// <summary>
        /// calculates the angle to feed corrected for 0/360 crossings
        /// eg. if the target is 350 and the current is 10, it will return 370 giving a diff of -20 degrees
        /// else you get +ve 340 and the turn is in the wrong direction
        /// </summary>
        double CurrentAngleTargetRel(double current, double target)
        {
            if (target - current < -180)
                return current - 360;
            else if (target - current > 180)
                return current + 360;
            else
                return current;
        }

        public static bool IsPaused()
        {
            return Instance.bPause;
        }

        Vector3 axisLock = new Vector3();
        private double calculateTargetHeading()
        {
            Vector3 fwd = Vector3.Cross(FlightData.planetUp, axisLock);
            double heading = -1 * Vector3.Angle(fwd, FlightData.planetNorth) * Math.Sign(Vector3.Dot(fwd, FlightData.planetEast));
            if (heading < 0)
                heading += 360;
            return heading;
        }

        private void setAxisLock(double heading)
        {
            double diff = heading - FlightData.heading;
            axisLock = Quaternion.AngleAxis((float)(diff - 90), (Vector3)FlightData.planetUp) * FlightData.surfVesForward;
        }

        private void hdgToggle()
        {
            bHdgWasActive = bHdgActive;
            if (bHdgActive)
            {
                setAxisLock(FlightData.heading);
                // Utils.GetAsst(PIDList.HdgBank).SetPoint = FlightData.heading;
                targetHeading = FlightData.heading.ToString("N2");

                bPause = false;
                headingEdit = false;

//                axisLock = FlightData.surfVelRight;
                setAxisLock(FlightData.heading);
            }
            else
            {
                PIDList.HdgBank.GetAsst().Clear();
                PIDList.BankToYaw.GetAsst().Clear();
                PIDList.Aileron.GetAsst().Clear();
                PIDList.Rudder.GetAsst().Clear();

                PIDList.HdgBank.GetAsst().skipDerivative = true;
                PIDList.BankToYaw.GetAsst().skipDerivative = true;
                PIDList.Aileron.GetAsst().skipDerivative = true;
                PIDList.Rudder.GetAsst().skipDerivative = true;
            }
        }

        private void vertToggle()
        {
            bVertWasActive = bVertActive;

            if (bVertActive)
            {
                PIDList.VertSpeed.GetAsst().Preset(-FlightData.AoA);
                PIDList.Elevator.GetAsst().Preset(-SurfSAS.Instance.pitchSet);

                if (bAltitudeHold)
                {
                    PIDList.Altitude.GetAsst().Preset(-FlightData.vertSpeed);
                    PIDList.Altitude.GetAsst().skipDerivative = true;

                    PIDList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude;
                    targetVert = FlightData.thisVessel.altitude.ToString("N1");
                }
                else
                {
                    PIDList.VertSpeed.GetAsst().SetPoint = FlightData.vertSpeed;
                    targetVert = FlightData.vertSpeed.ToString("N3");
                }
                bPause = false;
            }
            else
            {
                PIDList.Altitude.GetAsst().Clear();
                PIDList.VertSpeed.GetAsst().Clear();
                PIDList.Elevator.GetAsst().Clear();

                PIDList.VertSpeed.GetAsst().skipDerivative = true;
                PIDList.Elevator.GetAsst().skipDerivative = true;
            }
        }

        private void altToggle()
        {
            bWasAltitudeHold = bAltitudeHold;
            if (bAltitudeHold)
            {
                PIDList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude;
                targetVert = FlightData.thisVessel.altitude.ToString("N1");
            }
            else
            {
                PIDList.VertSpeed.GetAsst().SetPoint = FlightData.vertSpeed;
                targetVert = FlightData.vertSpeed.ToString("N2");
            }
        }

        private void wingToggle()
        {
            bWasWingLeveller = bWingLeveller;
            if (!bWingLeveller)
            {
                setAxisLock(FlightData.heading);
                targetHeading = PIDList.HdgBank.GetAsst().SetPoint.ToString("N2");
                headingEdit = false;
            }
        }

        private void throttleToggle()
        {
            bWasThrottleActive = bThrottleActive;
            if (bThrottleActive)
            {
                PIDList.Throttle.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                targetSpeed = FlightData.thisVessel.srfSpeed.ToString("N1");
            }
            else
                PIDList.Throttle.GetAsst().Clear();
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
                Messaging.statusMessage(bPause ? 0 : 1);
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
                PIDList.VertSpeed.GetAsst().SetPoint = 0;
                PIDList.Throttle.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
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
                if (bHdgActive && (GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || (!GameSettings.AXIS_YAW.IsNeutral() && Math.Abs(GameSettings.AXIS_YAW.GetAxis()) > 0.000001f)))
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

                    setAxisLock(hdg);
                    // Utils.GetAsst(PIDList.HdgBank).SetPoint = hdg;
                    // Utils.GetAsst(PIDList.BankToYaw).SetPoint = hdg;
                    hdg = Math.Round(hdg, 9);
                    targetHeading = hdg.ToString("N2");
                    headingEdit = false;
                }

                if (bVertActive && (GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || (!GameSettings.AXIS_PITCH.IsNeutral() && Math.Abs(GameSettings.AXIS_PITCH.GetAxis()) > 0.000001f)))
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
                        PIDList.Altitude.GetAsst().SetPoint = vert;
                    }
                    else
                        PIDList.VertSpeed.GetAsst().SetPoint = vert;

                    vert = Math.Round(vert, 9);
                    targetVert = vert.ToString("N3");
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

                    PIDList.Throttle.GetAsst().SetPoint = speed;

                    targetSpeed = Math.Max(speed, 0).ToString("N3");
                }
            }
        }

        internal static bool SASMonitor()
        {
            return (FlightData.thisVessel.ActionGroups[KSPActionGroup.SAS] || SurfSAS.ActivityCheck());
        }

        #region GUI
        bool headingEdit = false;

        private void displayWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                AppLauncherFlight.bDisplayAssistant = false;

            if (IsPaused())
                GUILayout.Box("CONTROL PAUSED", GeneralUI.UISkin.customStyles[(int)myStyles.labelAlert]);

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
            bShowHdg = GUILayout.Toggle(bShowHdg, "", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

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
                        headingEdit = false;
                        double newHdg;
                        if (double.TryParse(targetHeading, out newHdg) && newHdg >= 0 && newHdg <= 360)
                        {
                            setAxisLock(newHdg);
                            //Utils.GetAsst(PIDList.HdgBank).BumplessSetPoint = newHdg;
                            bHdgActive = bHdgWasActive = true; // skip toggle check to avoid being overwritten
                        }
                    }
                    string newString = GUILayout.TextField(targetHeading, GUILayout.Width(98));
                    if (targetHeading != newString)
                    {
                        targetHeading = newString;
                        headingEdit = true;
                    }
                    GUILayout.EndHorizontal();
                }

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(hdgScrollHeight));
                if (!bWingLeveller)
                {
                    drawPIDvalues(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0");
                    drawPIDvalues(PIDList.BankToYaw, "Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(PIDList.Aileron, "Bank", "\u00B0", FlightData.roll, 3, "Deflection", "\u00B0");
                    drawPIDvalues(PIDList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0");
                }
                GUILayout.EndScrollView();

                PIDList.Aileron.GetAsst().OutMin = Math.Min(Math.Max(PIDList.Aileron.GetAsst().OutMin, -1), 1);
                PIDList.Aileron.GetAsst().OutMax = Math.Min(Math.Max(PIDList.Aileron.GetAsst().OutMax, -1), 1);

                PIDList.Rudder.GetAsst().OutMin = Math.Min(Math.Max(PIDList.Rudder.GetAsst().OutMin, -1), 1);
                PIDList.Rudder.GetAsst().OutMax = Math.Min(Math.Max(PIDList.Rudder.GetAsst().OutMax, -1), 1);
            }
            #endregion

            #region Pitch GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowVert = GUILayout.Toggle(bShowVert, "", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

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
                        PIDList.Altitude.GetAsst().BumplessSetPoint = newVal;
                    else
                        PIDList.VertSpeed.GetAsst().BumplessSetPoint = newVal;

                    bVertActive = bVertWasActive = true; // skip the toggle check so value isn't overwritten
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(vertScrollHeight));

                if (bAltitudeHold)
                    drawPIDvalues(PIDList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true);
                drawPIDvalues(PIDList.VertSpeed, "Vertical Speed", "m/s", FlightData.vertSpeed, 2, "AoA", "\u00B0", true);

                if (showControlSurfaces)
                    drawPIDvalues(PIDList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true);

                PIDList.Elevator.GetAsst().OutMin = Math.Min(Math.Max(PIDList.Elevator.GetAsst().OutMin, -1), 1);
                PIDList.Elevator.GetAsst().OutMax = Math.Min(Math.Max(PIDList.Elevator.GetAsst().OutMax, -1), 1);

                GUILayout.EndScrollView();
            }
            #endregion

            #region Throttle GUI

            GUILayout.BeginHorizontal();
            // button background
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowThrottle = GUILayout.Toggle(bShowThrottle, "", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));
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
            PID_Controller controller = controllerid.GetAsst();
            controller.bShow = GUILayout.Toggle(controller.bShow, string.Format("{0}: {1}{2}", inputName, inputValue.ToString("N" + displayPrecision.ToString()), inputUnits), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));

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