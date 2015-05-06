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

    public enum VertMode
    {
        Disabled = -1,
        VSpeed = 0,
        Altitude = 1,
        RadarAltitude = 2
    }

    public enum HrztMode
    {
        Disabled = -1,
        WingsLevel = 0,
        Heading = 1
    }

    public enum ThrottleMode
    {
        Disabled = -1,
        Velocity = 0,
        Acceleration = 1
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistant : MonoBehaviour
    {
        #region Globals
        private static PilotAssistant instance;
        public static PilotAssistant Instance 
        {
            get { return instance; }
        }

        public PID_Controller[] controllers = new PID_Controller[8];

        public bool bPause = false;

        public HrztMode currentHrztMode = HrztMode.Disabled;
        HrztMode lastHrztMode = HrztMode.Disabled;
        GUIContent[] hrztLabels = new GUIContent[2] { new GUIContent("Lvl", "Mode: Wing Leveller"), new GUIContent("Hdg", "Mode: Heading Control") };

        public VertMode currentVertMode = VertMode.Disabled;
        VertMode lastVertMode = VertMode.Disabled;
        GUIContent[] vertLabels = new GUIContent[2] { new GUIContent("VSpeed", "Mode: Vertical Speed Control"), new GUIContent("Alt", "Mode: Altitude Control") };

        public ThrottleMode currentThrottleMode = ThrottleMode.Disabled;
        ThrottleMode lastThrottleMode = ThrottleMode.Disabled;
        GUIContent[] throttleLabels = new GUIContent[1] { new GUIContent("Velocity", "Mode: Velocity Control") };

        public Rect window = new Rect(10, 130, 10, 10);

        Vector2 scrollbarHdg = Vector2.zero;
        Vector2 scrollbarVert = Vector2.zero;

        public bool showPresets = false;
        public bool showPIDLimits = false;
        public bool showControlSurfaces = false;
        public bool doublesided = false;
        public bool showTooltips = true;

        string targetVert = "0.00";
        string targetHeading = "0.00";
        string targetSpeed = "0.00";

        // rate values for keyboard input
        double hrztScale = 0.4;
        double vertScale = 0.4; // altitude rate is x10
        double throttleScale = 0.4;

        // Direction control vars
        Vector3 currentDirectionTarget = Vector3.zero; // this is the vec the control is aimed at
        Vector3 newDirectionTarget = Vector3.zero; // this is the vec we are moving to
        double increment = 0; // this is the angle to shift per second
        bool hdgShiftIsRunning = false;
        bool stopHdgShift = false;

        // delayed heading input vars
        public double commitDelay = 0;
        double headingChangeToCommit; // The amount of heading change to commit when the timer expires
        double headingTimeToCommit; // update heading target when <= 0

        // don't update hdg display if true
        bool headingEdit = false;

        bool bShowSettings = false;
        bool bShowHdg = true;
        bool bShowVert = true;
        bool bShowThrottle = true;

        float hdgScrollHeight;
        float vertScrollHeight;

        string newPresetName = "";
        Rect presetWindow = new Rect(0, 0, 200, 10);

        float pitchSet = 0;

        // Kp, Ki, Kd, Min Out, Max Out, I Min, I Max, Scalar, Easing
        public static double[] defaultHdgBankGains = { 2, 0.1, 0, -30, 30, -0.5, 0.5, 1, 1 };
        public static double[] defaultBankToYawGains = { 0, 0, 0.01, -2, 2, -0.5, 0.5, 1, 1 };
        public static double[] defaultAileronGains = { 0.02, 0.005, 0.01, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultRudderGains = { 0.1, 0.08, 0.05, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultAltitudeGains = { 0.15, 0.01, 0, -50, 50, -0.01, 0.01, 1, 100 };
        public static double[] defaultVSpeedGains = { 2, 0.8, 2, -10, 10, -5, 5, 1, 10 };
        public static double[] defaultElevatorGains = { 0.05, 0.01, 0.1, -1, 1, -0.4, 0.4, 1, 1 };
        public static double[] defaultThrottleGains = { 0.2, 0.08, 0.1, -1, 0, -1, 0.4, 1, 1 };

        #endregion

        public void Start()
        {
            instance = this;
            Initialise();

            // register vessel
            if (FlightData.thisVessel == null)
                FlightData.thisVessel = FlightGlobals.ActiveVessel;

            PresetManager.loadCraftAsstPreset();

            // Input clamps aren't part of the presets (there's no reason for them to be...). Just some sanity checking
            PIDList.Aileron.GetAsst().InMax = 180;
            PIDList.Aileron.GetAsst().InMin = -180;
            PIDList.Altitude.GetAsst().InMin = 0;
            PIDList.Throttle.GetAsst().InMin = 0;
            PIDList.HdgBank.GetAsst().isHeadingControl = true; // fix for derivative freaking out when heading target flickers across 0/360

            // events
            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotEvent);
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpHandler);

            // add GUI callback
            RenderingManager.AddToPostDrawQueue(5, drawGUI);

            // start the WASD monitor
            StartCoroutine(InputResponse());
        }

        public void OnDestroy()
        {
            RenderingManager.RemoveFromPostDrawQueue(5, drawGUI);
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);
            PresetManager.saveToFile();

            instance = null; // static object is only for easy referencing between modules. Don't need to keep hold of it
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
            PresetManager.initDefaultPresets(new AsstPreset(controllers, "default"));

            PresetManager.saveDefaults();
        }

        private void vesselSwitch(Vessel v)
        {
            // kill the old events, switch vessels, add the new events, load the correct presets
            FlightData.thisVessel.OnPreAutopilotUpdate -= new FlightInputCallback(preAutoPilotEvent);
            FlightData.thisVessel.OnPostAutopilotUpdate -= new FlightInputCallback(vesselController);
            FlightData.thisVessel = v;
            FlightData.thisVessel.OnPostAutopilotUpdate += new FlightInputCallback(vesselController);
            FlightData.thisVessel.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotEvent);

            PresetManager.loadCraftAsstPreset();
        }

        private void warpHandler()
        {
            // reset any setpoints on leaving warp
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
            {
                currentHrztMode = HrztMode.Disabled;
                currentVertMode = VertMode.Disabled;
                currentThrottleMode = ThrottleMode.Disabled;
            }
        }


        #region Update / Input Monitoring
        public void Update()
        {
            keyMonitor();

            if (Utils.AsstIsPaused())
                return;

            // toggle monitoring
            if (currentHrztMode != lastHrztMode)
                hdgToggle();
            if (currentVertMode != lastVertMode)
                vertToggle();              
            if (currentThrottleMode != lastThrottleMode)
                throttleToggle();

            // Heading setpoint updates
            if (currentHrztMode != HrztMode.Disabled)
            {
                if (!FlightData.thisVessel.checkLanded())
                    PIDList.HdgBank.GetAsst().SetPoint = Utils.calculateTargetHeading(currentDirectionTarget);
                else
                    PIDList.HdgBank.GetAsst().SetPoint = FlightData.heading;

                if (!headingEdit)
                    targetHeading = PIDList.HdgBank.GetAsst().SetPoint.ToString("0.00");
            }
        }

        IEnumerator InputResponse()
        {
            while (HighLogic.LoadedSceneIsFlight)
            {
                yield return null;
                if (!Utils.AsstIsPaused())
                {
                    double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1; // normally *1, with alt is *10
                    if (FlightInputHandler.fetch.precisionMode)
                        scale = 0.1 / scale; // normally *0.1, with alt is *0.01

                    // ============================================================ Hrzt Controls ============================================================
                    if (currentHrztMode != HrztMode.Disabled && !FlightData.thisVessel.checkLanded())
                    {
                        if (GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_YAW))
                        {
                            if (GameSettings.YAW_LEFT.GetKey())
                                headingChangeToCommit -= hrztScale * scale;
                            else if (GameSettings.YAW_RIGHT.GetKey())
                                headingChangeToCommit += hrztScale * scale;
                            else
                                headingChangeToCommit += hrztScale * scale * GameSettings.AXIS_YAW.GetAxis();

                            headingChangeToCommit = headingChangeToCommit.headingClamp(180);
                            headingTimeToCommit = commitDelay;
                        }

                        if (headingTimeToCommit <= 0 && headingChangeToCommit != 0)
                        {
                            StartCoroutine(shiftHeadingTarget(Utils.calculateTargetHeading(newDirectionTarget) + headingChangeToCommit));

                            headingChangeToCommit = 0;
                        }
                        else if (headingTimeToCommit > 0)
                            headingTimeToCommit -= TimeWarp.deltaTime;
                    }
                    else
                        headingChangeToCommit = 0;

                    // ============================================================ Vertical Controls ============================================================
                    if (currentVertMode != VertMode.Disabled && (GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_PITCH)))
                    {
                        double vert = 0; // = double.Parse(targetVert);
                        if (currentVertMode == VertMode.Altitude)
                            vert = PIDList.Altitude.GetAsst().SetPoint;
                        else if (currentVertMode == VertMode.VSpeed)
                            vert = PIDList.VertSpeed.GetAsst().SetPoint;


                        if (currentVertMode == VertMode.Altitude)
                            vert /= 10; // saves having to specify the rates seperately

                        if (GameSettings.PITCH_DOWN.GetKey())
                            vert -= vertScale * scale;
                        else if (GameSettings.PITCH_UP.GetKey())
                            vert += vertScale * scale;
                        else if (!Utils.IsNeutral(GameSettings.AXIS_PITCH))
                            vert += vertScale * scale * GameSettings.AXIS_PITCH.GetAxis();

                        if (currentVertMode == VertMode.Altitude)
                        {
                            vert = Math.Max(vert * 10, 0);
                            PIDList.Altitude.GetAsst().SetPoint = vert;
                        }
                        else
                            PIDList.VertSpeed.GetAsst().SetPoint = vert;
                        targetVert = vert.ToString("0.00");
                    }

                    // ============================================================ Throttle Controls ============================================================
                    if (currentThrottleMode != ThrottleMode.Disabled && ((GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey())
                                    || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown()))
                    {
                        double speed = PIDList.Throttle.GetAsst().SetPoint;
                        if (GameSettings.THROTTLE_UP.GetKey())
                            speed += throttleScale * scale;
                        else if (GameSettings.THROTTLE_DOWN.GetKey())
                            speed -= throttleScale * scale;

                        if (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey())
                            speed = 0;
                        if (GameSettings.THROTTLE_FULL.GetKeyDown())
                            speed = 2400;

                        PIDList.Throttle.GetAsst().SetPoint = speed;

                        targetSpeed = Math.Max(speed, 0).ToString("0.00");
                    }
                }
            }
        }

        // stuff that isn't directly control related
        private void keyMonitor()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // reset locks on unpausing
                lastHrztMode = HrztMode.Disabled;
                lastVertMode = VertMode.Disabled;
                lastThrottleMode = ThrottleMode.Disabled;

                bPause = !bPause;
                Messaging.postMessage(bPause ? Messaging.pauseMessage : Messaging.unpauseMessage);
            }
            if (Utils.isFlightControlLocked())
                return;

            // update targets
            if (GameSettings.SAS_TOGGLE.GetKeyDown())
            {
                lastHrztMode = HrztMode.Disabled;
                lastVertMode = VertMode.Disabled;
            }

            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.X))
            {
                PIDList.VertSpeed.GetAsst().SetPoint = 0;
                PIDList.Throttle.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                currentVertMode = VertMode.VSpeed;
                targetVert = "0.00";
                targetSpeed = FlightData.thisVessel.srfSpeed.ToString("0.00");
                Messaging.postMessage(Messaging.levelMessage);
            }

            if (Input.GetKeyDown(KeyCode.Keypad9) && GameSettings.MODIFIER_KEY.GetKey())
                currentHrztMode = (currentHrztMode == HrztMode.Disabled) ? HrztMode.Heading : HrztMode.Disabled;
            if (Input.GetKeyDown(KeyCode.Keypad6) && GameSettings.MODIFIER_KEY.GetKey())
                currentVertMode = (currentVertMode == VertMode.Disabled) ? VertMode.VSpeed : VertMode.Disabled;
            if (Input.GetKeyDown(KeyCode.Keypad3) && GameSettings.MODIFIER_KEY.GetKey())
                currentThrottleMode = currentThrottleMode == ThrottleMode.Disabled ? ThrottleMode.Velocity : ThrottleMode.Disabled;
        }

        private void hdgToggle()
        {
            headingEdit = false;

            PIDList.HdgBank.GetAsst().skipDerivative = true;
            PIDList.BankToYaw.GetAsst().skipDerivative = true;
            PIDList.Aileron.GetAsst().skipDerivative = true;
            PIDList.Rudder.GetAsst().skipDerivative = true;

            switch (currentHrztMode)
            {
                case HrztMode.Disabled:
                    stopHdgShift = true;
                    PIDList.HdgBank.GetAsst().Clear();
                    PIDList.BankToYaw.GetAsst().Clear();
                    PIDList.Aileron.GetAsst().Clear();
                    PIDList.Rudder.GetAsst().Clear();
                    break;
                case HrztMode.Heading:
                    currentDirectionTarget = Utils.vecHeading(FlightData.heading);
                    StartCoroutine(shiftHeadingTarget(FlightData.heading));
                    bPause = false;
                    break;
                case HrztMode.WingsLevel:
                    bPause = false;
                    break;
            }
            lastHrztMode = currentHrztMode;
        }

        private void vertToggle()
        {
            PIDList.VertSpeed.GetAsst().skipDerivative = true;
            PIDList.Elevator.GetAsst().skipDerivative = true;
            PIDList.Altitude.GetAsst().skipDerivative = true;

            switch (currentVertMode)
            {
                case VertMode.Disabled:
                    {
                        PIDList.Altitude.GetAsst().Clear();
                        PIDList.VertSpeed.GetAsst().Clear();
                        PIDList.Elevator.GetAsst().Clear();
                        break;
                    }
                case VertMode.VSpeed:
                    {
                        bPause = false;
                        PIDList.VertSpeed.GetAsst().Preset(-FlightData.AoA);
                        PIDList.Elevator.GetAsst().Preset(pitchSet);
                        PIDList.VertSpeed.GetAsst().SetPoint = FlightData.vertSpeed + FlightData.AoA / PIDList.VertSpeed.GetAsst().PGain;
                        PIDList.VertSpeed.GetAsst().BumplessSetPoint = FlightData.vertSpeed;
                        targetVert = PIDList.VertSpeed.GetAsst().SetPoint.ToString("0.00");
                        break;
                    }
                case VertMode.Altitude:
                    {
                        bPause = false;
                        PIDList.Altitude.GetAsst().Preset(-FlightData.vertSpeed);
                        PIDList.VertSpeed.GetAsst().Preset(-FlightData.AoA);
                        PIDList.Elevator.GetAsst().Preset(pitchSet);
                        PIDList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude + FlightData.vertSpeed / PIDList.Altitude.GetAsst().PGain;
                        PIDList.Altitude.GetAsst().BumplessSetPoint = FlightData.thisVessel.altitude;
                        targetVert = PIDList.Altitude.GetAsst().SetPoint.ToString("0.00");
                        break;
                    }
                case VertMode.RadarAltitude:
                    {
                        bPause = false;
                        break;
                    }
            }

            lastVertMode = currentVertMode;
        }

        private void throttleToggle()
        {
            switch (currentThrottleMode)
            {
                case ThrottleMode.Disabled:
                    {
                        PIDList.Throttle.GetAsst().Clear();
                        break;
                    }
                case ThrottleMode.Velocity:
                    {
                        PIDList.Throttle.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                        targetSpeed = PIDList.Throttle.GetAsst().SetPoint.ToString("0.00");
                        break;
                    }
                case ThrottleMode.Acceleration:
                    {
                        break;
                    }
            }
            lastThrottleMode = currentThrottleMode;
        }
        #endregion

        #region Control / Fixed Update
        private void preAutoPilotEvent(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            FlightData.updateAttitude();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            pitchSet = state.pitch; // last pitch ouput, used for presetting the elevator
            if (Utils.AsstIsPaused() || FlightData.thisVessel.srfSpeed < 1 || !FlightData.thisVessel.IsControllable)
                return;

            // Heading Control
            if (currentHrztMode != HrztMode.Disabled)
            {
                if (currentHrztMode == HrztMode.Heading)
                {
                    // calculate the bank angle response based on the current heading
                    double hdgBankResponse = PIDList.HdgBank.GetAsst().ResponseD(Utils.CurrentAngleTargetRel(FlightData.progradeHeading, PIDList.HdgBank.GetAsst().SetPoint, 180));
                    // aileron setpoint updated, bank angle also used for yaw calculations (don't go direct to rudder because we want yaw stabilisation *or* turn assistance)
                    PIDList.BankToYaw.GetAsst().SetPoint = PIDList.Aileron.GetAsst().SetPoint = hdgBankResponse;
                    PIDList.Rudder.GetAsst().SetPoint = -PIDList.BankToYaw.GetAsst().ResponseD(FlightData.yaw);
                }
                else
                {
                    PIDList.Aileron.GetAsst().SetPoint = 0;
                    PIDList.Rudder.GetAsst().SetPoint = 0;
                }

                // don't want SAS inputs contributing here, so calculate manually
                float rollInput = 0;
                if (GameSettings.ROLL_LEFT.GetKey())
                    rollInput = -1;
                else if (GameSettings.ROLL_RIGHT.GetKey())
                    rollInput = 1;
                else if (!Utils.IsNeutral(GameSettings.AXIS_ROLL))
                    rollInput = GameSettings.AXIS_ROLL.GetAxis();

                if (FlightInputHandler.fetch.precisionMode)
                    rollInput *= 0.33f;

                if (!FlightData.thisVessel.checkLanded())
                {
                    state.roll = (PIDList.Aileron.GetAsst().ResponseF(FlightData.bank) + rollInput).Clamp(-1, 1);
                    state.yaw = PIDList.Rudder.GetAsst().ResponseF(FlightData.yaw).Clamp(-1, 1);
                }
            }

            if (currentVertMode != VertMode.Disabled)
            {
                // Set requested vertical speed
                if (currentVertMode == VertMode.Altitude)
                    PIDList.VertSpeed.GetAsst().SetPoint = -PIDList.Altitude.GetAsst().ResponseD(FlightData.thisVessel.altitude);

                PIDList.Elevator.GetAsst().SetPoint = -PIDList.VertSpeed.GetAsst().ResponseD(FlightData.vertSpeed);
                state.pitch = -PIDList.Elevator.GetAsst().ResponseF(FlightData.AoA).Clamp(-1, 1);
            }
            if (currentThrottleMode != ThrottleMode.Disabled)
            {
                if (PIDList.Throttle.GetAsst().SetPoint != 0)
                    state.mainThrottle = (-PIDList.Throttle.GetAsst().ResponseF(FlightData.thisVessel.srfSpeed)).Clamp(0, 1);
                else
                    state.mainThrottle = 0;
            }
        }

        IEnumerator shiftHeadingTarget(double newHdg)
        {
            headingEdit = false;
            stopHdgShift = false;
            currentDirectionTarget = Utils.vecHeading(FlightData.heading - (FlightData.bank / PIDList.HdgBank.GetAsst().PGain).headingClamp(360));
            newDirectionTarget = Utils.vecHeading(newHdg);
            increment = 0;

            if (hdgShiftIsRunning)
                yield break;
            hdgShiftIsRunning = true;

            while (!stopHdgShift && Math.Abs(Vector3.Angle(currentDirectionTarget, newDirectionTarget)) > 0.01)
            {
                double finalTarget = Utils.calculateTargetHeading(newDirectionTarget);
                double target = Utils.calculateTargetHeading(currentDirectionTarget);
                increment += PIDList.HdgBank.GetAsst().Easing * TimeWarp.fixedDeltaTime * 0.01;

                double remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                if (remainder < 0)
                    target += Math.Max(-1 * increment, remainder);
                else
                    target += Math.Min(increment, remainder);

                currentDirectionTarget = Utils.vecHeading(target);
                yield return new WaitForFixedUpdate();
            }
            if (!stopHdgShift)
                currentDirectionTarget = newDirectionTarget;
            hdgShiftIsRunning = false;
        }
        #endregion

        #region GUI
        public void drawGUI()
        {
            if (!AppLauncherFlight.bDisplayAssistant)
                return;

            GUI.skin = GeneralUI.UISkin;
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;

            // main window
            #region Main Window resizing (scroll views dont work nicely with GUILayout)
            // Have to put the width changes before the draw so the close button is correctly placed
            float width;
            if (showPIDLimits && controllers.Any(c => c.bShow)) // use two column view if show limits option and a controller is open
                width = 370;
            else
                width = 240;

            if (bShowHdg)
            {
                hdgScrollHeight = 0; // no controllers visible when in wing lvl mode unless ctrl surf's are there
                if (currentHrztMode != HrztMode.WingsLevel)
                    hdgScrollHeight += 55; // hdg & yaw headers
                if ((PIDList.HdgBank.GetAsst().bShow || PIDList.BankToYaw.GetAsst().bShow) && currentHrztMode != HrztMode.WingsLevel)
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
                if (currentVertMode == VertMode.Altitude)
                    vertScrollHeight += 27; // altitude header
                if ((PIDList.Altitude.GetAsst().bShow && currentVertMode == VertMode.Altitude) || PIDList.VertSpeed.GetAsst().bShow)
                    vertScrollHeight += 150; // open  controller
                else if (showControlSurfaces)
                {
                    vertScrollHeight += 27; // elevator header
                    if (PIDList.Elevator.GetAsst().bShow)
                        vertScrollHeight += 123; // open controller
                }
            }
            #endregion

            window = GUILayout.Window(34244, window, displayWindow, "Pilot Assistant", GUILayout.Height(0), GUILayout.Width(width));

            // tooltip window. Label skin is transparent so it's only drawing what's inside it
            if (tooltip != "" && showTooltips)
                GUILayout.Window(34246, new Rect(window.x + window.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));

            if (showPresets)
            {
                // move the preset window to sit to the right of the main window, with the tops level
                presetWindow.x = window.x + window.width;
                presetWindow.y = window.y;

                GUILayout.Window(34245, presetWindow, displayPresetWindow, "Pilot Assistant Presets", GUILayout.Width(200), GUILayout.Height(0));
            }
        }

        private void displayWindow(int id)
        {
            if (GUI.Button(new Rect(window.width - 16, 2, 14, 14), ""))
                AppLauncherFlight.bDisplayAssistant = false;

            if (Utils.AsstIsPaused())
                GUILayout.Box("CONTROL PAUSED", GeneralUI.UISkin.customStyles[(int)myStyles.labelAlert]);

            showPresets = GUILayout.Toggle(showPresets, showPresets ? "Hide Presets" : "Show Presets", GUILayout.Width(200));

            #region Hdg GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowHdg = GUILayout.Toggle(bShowHdg, bShowHdg ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

            if (currentHrztMode != HrztMode.Disabled)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Roll and Yaw Control", GUILayout.Width(186)))
                currentHrztMode = (currentHrztMode == HrztMode.Disabled) ? HrztMode.Heading : HrztMode.Disabled;

            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowHdg)
            {
                currentHrztMode = (HrztMode)GUILayout.SelectionGrid((int)currentHrztMode, hrztLabels, 2, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                if (currentHrztMode != HrztMode.WingsLevel)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Hdg: ", GUILayout.Width(90)))
                    {
                        double newHdg;
                        if (double.TryParse(targetHeading, out newHdg))
                        {
                            StartCoroutine(shiftHeadingTarget(newHdg.headingClamp(360)));
                            currentHrztMode = lastHrztMode = HrztMode.Heading; // skip toggle check to avoid being overwritten

                            GUI.FocusControl("Target Hdg: ");
                            GUI.UnfocusWindow();
                        }
                    }

                    double displayTargetDelta = 0; // active setpoint or absolute value to change (yaw L/R input)
                    string displayTarget = "0.00"; // target setpoint or setpoint to commit as target setpoint

                    if (headingChangeToCommit != 0)
                        displayTargetDelta = headingChangeToCommit;
                    else if (currentHrztMode == HrztMode.Heading)
                    {
                        if (!hdgShiftIsRunning)
                            displayTargetDelta = PIDList.HdgBank.GetAsst().SetPoint - FlightData.heading;
                        else
                            displayTargetDelta = Utils.calculateTargetHeading(newDirectionTarget) - FlightData.heading;

                        displayTargetDelta = displayTargetDelta.headingClamp(180);
                    }

                    if (headingEdit)
                        displayTarget = targetHeading;
                    else if (headingChangeToCommit == 0 || FlightData.thisVessel.checkLanded())
                        displayTarget = Utils.calculateTargetHeading(newDirectionTarget).ToString("0.00");
                    else
                        displayTarget = (Utils.calculateTargetHeading(newDirectionTarget) + headingChangeToCommit).headingClamp(360).ToString("0.00");

                    targetHeading = GUILayout.TextField(displayTarget, GUILayout.Width(51));
                    if (targetHeading != displayTarget)
                        headingEdit = true;
                    GUILayout.Label(displayTargetDelta.ToString("0.00"), GeneralUI.UISkin.customStyles[(int)myStyles.greenTextBox], GUILayout.Width(51));
                    GUILayout.EndHorizontal();
                }

                scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(hdgScrollHeight));
                if (currentHrztMode != HrztMode.WingsLevel)
                {
                    drawPIDvalues(PIDList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0");
                    drawPIDvalues(PIDList.BankToYaw, "Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(PIDList.Aileron, "Bank", "\u00B0", FlightData.bank, 3, "Deflection", "\u00B0");
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
            bShowVert = GUILayout.Toggle(bShowVert, bShowVert ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

            if (currentVertMode != VertMode.Disabled)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Vertical Control", GUILayout.Width(186)))
                currentVertMode = (currentVertMode == VertMode.Disabled) ? VertMode.VSpeed : VertMode.Disabled;
           
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowVert)
            {
                currentVertMode = (VertMode)GUILayout.SelectionGrid((int)currentVertMode, vertLabels, 2, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(currentVertMode == VertMode.Altitude ? "Target Altitude:" : "Target Speed:", GUILayout.Width(98)))
                {
                    ScreenMessages.PostScreenMessage("Target " + (currentVertMode == VertMode.Altitude ? "Altitude" : "Vertical Speed") + " updated");

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (currentVertMode == VertMode.Altitude)
                    {
                        PIDList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude + FlightData.vertSpeed / PIDList.Altitude.GetAsst().PGain;
                        PIDList.Altitude.GetAsst().BumplessSetPoint = newVal;
                    }
                    else
                    {
                        PIDList.VertSpeed.GetAsst().SetPoint = FlightData.thisVessel.verticalSpeed + FlightData.AoA / PIDList.VertSpeed.GetAsst().PGain;
                        PIDList.VertSpeed.GetAsst().BumplessSetPoint = newVal;
                    }

                    currentVertMode = lastVertMode = currentVertMode == VertMode.Disabled ? VertMode.VSpeed : currentVertMode;

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                scrollbarVert = GUILayout.BeginScrollView(scrollbarVert, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(vertScrollHeight));

                if (currentVertMode == VertMode.Altitude)
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
            bShowThrottle = GUILayout.Toggle(bShowThrottle, bShowThrottle ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));
            if (currentThrottleMode != ThrottleMode.Disabled)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Throttle Control", GUILayout.Width(186)))
                currentThrottleMode = currentThrottleMode == ThrottleMode.Disabled ? ThrottleMode.Velocity : ThrottleMode.Disabled;
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
                    PIDList.Throttle.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                    PIDList.Throttle.GetAsst().BumplessSetPoint = newVal;

                    currentThrottleMode = lastThrottleMode = ThrottleMode.Velocity; // skip the toggle check so value isn't overwritten

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                drawPIDvalues(PIDList.Throttle, "Speed", "m/s", FlightData.thisVessel.srfSpeed, 2, "Throttle", "", true);
                // can't have people bugging things out now can we...
                PIDList.Throttle.GetAsst().OutMin = PIDList.Throttle.GetAsst().OutMin.Clamp(-1, 0);
                PIDList.Throttle.GetAsst().OutMax = PIDList.Throttle.GetAsst().OutMax.Clamp(-1, 0);
            }

            #endregion

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            if (GUILayout.Button("Options", GUILayout.Width(205)))
            {
                bShowSettings = !bShowSettings;
            }
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            if (bShowSettings)
            {
                showPIDLimits = GUILayout.Toggle(showPIDLimits, showPIDLimits ? "Hide PID Limits" : "Show PID Limits", GUILayout.Width(200));
                showControlSurfaces = GUILayout.Toggle(showControlSurfaces, showControlSurfaces ? "Hide Control Surfaces" : "Show Control Surfaces", GUILayout.Width(200));
                doublesided = GUILayout.Toggle(doublesided, "Separate Min and Max limits", GUILayout.Width(200));
                showTooltips = GUILayout.Toggle(showTooltips, "Show Tooltips", GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                GUILayout.Label("Input delay", GUILayout.Width(98));
                string text = GUILayout.TextField(commitDelay.ToString("0.0"), GUILayout.Width(98));
                try
                {
                    commitDelay = double.Parse(text);
                }
                catch { } // if the conversion fails it just reverts to the last good value. No need for further action
                GUILayout.EndHorizontal();
            }

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
                showPresets = false;

            if (PresetManager.Instance.activeAsstPreset != null) // preset will be null after deleting an active preset
            {
                GUILayout.Label(string.Format("Active Preset: {0}", PresetManager.Instance.activeAsstPreset.name));
                if (PresetManager.Instance.activeAsstPreset.name != "default")
                {
                    if (GUILayout.Button("Update Preset"))
                        PresetManager.updateAsstPreset();
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newAsstPreset(ref newPresetName, controllers);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadAsstPreset(PresetManager.Instance.craftPresetDict["default"].AsstPreset);

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));

            foreach (AsstPreset p in PresetManager.Instance.AsstPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadAsstPreset(p);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    PresetManager.deleteAsstPreset(p);
                GUILayout.EndHorizontal();
            }
        }
        #endregion
    }
}