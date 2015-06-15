using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    using PID;
    using Presets;
    using Utility;

    public enum AsstList
    {
        HdgBank,
        BankToYaw,
        Aileron,
        Rudder,
        Altitude,
        VertSpeed,
        Elevator,
        Speed,
        Acceleration
    }

    public enum VertMode
    {
        ToggleOn = -1,
        VSpeed = 0,
        Altitude = 1,
        RadarAltitude = 2
    }

    public enum HrztMode
    {
        ToggleOn = -1,
        WingsLevel = 0,
        Heading = 1,
        HeadingNum = 2
    }

    public enum ThrottleMode
    {
        ToggleOn = -1,
        Speed = 0,
        Acceleration = 1
    }

    public class PilotAssistant
    {
        #region Globals
        public AsstVesselModule vesRef;
        Vessel controlledVessel;
        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        public AsstController[] controllers = new AsstController[9];

        public bool bPause = false;
        public bool bLockInput = false;

        public bool HrztActive = false;
        public HrztMode CurrentHrztMode = HrztMode.Heading;
        static GUIContent[] hrztLabels = new GUIContent[3] { new GUIContent("Lvl", "Mode: Wing Leveller"), new GUIContent("Hdg", "Mode: Heading Control - Dirction"), new GUIContent("Hdg#", "Mode: Heading control - Value") };

        public bool VertActive = false;
        public VertMode CurrentVertMode = VertMode.VSpeed;
        static GUIContent[] vertLabels = new GUIContent[3] { new GUIContent("VSpd", "Mode: Vertical Speed Control"), new GUIContent("Alt", "Mode: Altitude Control"), new GUIContent("RAlt", "Mode: Radar Altitude Control") };

        public bool ThrtActive = false;
        public ThrottleMode CurrentThrottleMode = ThrottleMode.Speed;
        static GUIContent[] throttleLabels = new GUIContent[2] { new GUIContent("Vel", "Mode: Velocity Control"), new GUIContent("Acc", "Mode: Acceleration Control") };

        public static Rect window = new Rect(10, 130, 10, 10);

        public static bool showPresets = false;
        public static bool showPIDLimits = false;
        public static bool showControlSurfaces = false;
        public static bool doublesided = false;

        string targetVert = "0.00";
        string targetHeading = "0.00";
        string targetSpeed = "0.00";

        const string yawLockID = "Pilot Assistant Yaw Lock";
        public static bool yawLockEngaged = false;
        const string pitchLockID = "Pilot Assistant Pitch Lock";
        public static bool pitchLockEngaged = false;

        // rate values for keyboard input
        const double hrztScale = 0.4;
        const double vertScale = 0.4; // altitude rate is x10
        const double throttleScale = 0.4;

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

        bool bShowHdg = true;
        bool bShowVert = true;
        bool bShowThrottle = true;

        bool bMinimiseHdg = false;
        bool bMinimiseVert = false;
        bool bMinimiseThrt = false;

        Vector2 HdgScrollbar = Vector2.zero;
        public float hdgScrollHeight = 55;
        public static float maxHdgScrollbarHeight = 55;
        Vector2 VertScrollbar = Vector2.zero;
        public float vertScrollHeight = 55;
        public static float maxVertScrollbarHeight = 55;
        Vector2 ThrtScrollbar = Vector2.zero;
        public float thrtScrollHeight = 55;
        public static float maxThrtScrollbarHeight = 55;

        float dragStart = 0;
        float dragID = 0; // 0 = inactive, 1 = hdg, 2 = vert, 3 = thrt

        string newPresetName = "";
        static Rect presetWindow = new Rect(0, 0, 200, 10);

        float pitchSet = 0;

        // Kp, Ki, Kd, Min Out, Max Out, I Min, I Max, Scalar, Easing
        public static readonly double[] defaultHdgBankGains = { 2, 0.1, 0, -30, 30, -0.5, 0.5, 1, 1 };
        public static readonly double[] defaultBankToYawGains = { 0, 0, 0.01, -2, 2, -0.5, 0.5, 1, 1 };
        public static readonly double[] defaultAileronGains = { 0.02, 0.005, 0.01, -1, 1, -1, 1, 1, 1 };
        public static readonly double[] defaultRudderGains = { 0.1, 0.08, 0.05, -1, 1, -1, 1, 1, 1 };
        public static readonly double[] defaultAltitudeGains = { 0.15, 0.01, 0, -50, 50, 0, 0, 1, 100 };
        public static readonly double[] defaultVSpeedGains = { 2, 0.8, 2, -15, 15, -10, 10, 1, 10 };
        public static readonly double[] defaultElevatorGains = { 0.05, 0.01, 0.1, -1, 1, -1, 1, 2, 1 };
        public static readonly double[] defaultSpeedGains = { 0.2, 0.0, 0.0, -10, 10, -10, 10, 1, 10 };
        public static readonly double[] defaultAccelGains = { 0.2, 0.08, 0.0, -1, 0, -1, 1, 1, 1 };

        #endregion

        public void Start(AsstVesselModule vesRef)
        {
            this.vesRef = vesRef;
            controlledVessel = vesRef.vesselRef;
            Initialise();

            // Input clamps aren't part of the presets (there's no reason for them to be...). Just some sanity checking
            AsstList.Aileron.GetAsst(this).InMax = 180;
            AsstList.Aileron.GetAsst(this).InMin = -180;
            AsstList.Altitude.GetAsst(this).InMin = 0;
            AsstList.Speed.GetAsst(this).InMin = 0;
            AsstList.HdgBank.GetAsst(this).isHeadingControl = true; // fix for derivative freaking out when heading target flickers across 0/360

            // start the WASD monitor
            StartCoroutine(InputResponse());

            InputLockManager.RemoveControlLock(pitchLockID);
            InputLockManager.RemoveControlLock(yawLockID);
            pitchLockEngaged = false;
            yawLockEngaged = false;

            PresetManager.loadCraftAsstPreset(this);
        }

        public void OnDestroy()
        {
            InputLockManager.RemoveControlLock(pitchLockID);
            InputLockManager.RemoveControlLock(yawLockID);
            pitchLockEngaged = false;
            yawLockEngaged = false;
        }

        void Initialise()
        {
            controllers[(int)AsstList.HdgBank] = new AsstController(AsstList.HdgBank, defaultHdgBankGains);
            controllers[(int)AsstList.BankToYaw] = new AsstController(AsstList.BankToYaw, defaultBankToYawGains);
            controllers[(int)AsstList.Aileron] = new AsstController(AsstList.Aileron, defaultAileronGains);
            controllers[(int)AsstList.Rudder] = new AsstController(AsstList.Rudder, defaultRudderGains);
            controllers[(int)AsstList.Altitude] = new AsstController(AsstList.Altitude, defaultAltitudeGains);
            controllers[(int)AsstList.VertSpeed] = new AsstController(AsstList.VertSpeed, defaultVSpeedGains);
            controllers[(int)AsstList.Elevator] = new AsstController(AsstList.Elevator, defaultElevatorGains);
            controllers[(int)AsstList.Speed] = new AsstController(AsstList.Speed, defaultSpeedGains);
            controllers[(int)AsstList.Acceleration] = new AsstController(AsstList.Acceleration, defaultAccelGains);

            // Set up a default preset that can be easily returned to
            if (PresetManager.Instance.craftPresetDict["default"].AsstPreset == null)
                PresetManager.initDefaultPresets(new AsstPreset(controllers, "default"));
        }

        public void warpHandler()
        {
            // reset any setpoints on leaving warp
            if (TimeWarp.CurrentRateIndex == 0 && TimeWarp.CurrentRate != 1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
            {
                HrztActive = false;
                VertActive = false;
                ThrtActive = false;
            }
        }

        #region Update / Input Monitoring
        public void Update()
        {
            if (bPause)
                return;

            // Heading setpoint updates
            if (HrztActive)
            {
                if (vesRef.vesselRef.checkLanded())
                    newDirectionTarget = currentDirectionTarget = Utils.vecHeading(vesRef.vesselData.heading, vesRef.vesselData);
                if (CurrentHrztMode == HrztMode.Heading)
                {
                    AsstList.HdgBank.GetAsst(this).SetPoint = Utils.calculateTargetHeading(currentDirectionTarget, vesRef.vesselData);

                    if (!headingEdit)
                        targetHeading = AsstList.HdgBank.GetAsst(this).SetPoint.ToString("0.00");
                }
            }
        }

        IEnumerator InputResponse()
        {
            while (HighLogic.LoadedSceneIsFlight)
            {
                yield return null;
                if (vesRef.isActiveVessel() && !(bLockInput || Utils.isFlightControlLocked()))
                {
                    if (!bPause)
                    {
                        double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1; // normally *1, with alt is *10
                        if (FlightInputHandler.fetch.precisionMode)
                            scale = 0.1 / scale; // normally *0.1, with alt is *0.01

                        // ============================================================ Hrzt Controls ============================================================
                        if (HrztActive && !controlledVessel.checkLanded())
                        {
                            if (Utils.hasYawInput())
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
                                StartCoroutine(shiftHeadingTarget(Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData) + headingChangeToCommit));

                                headingChangeToCommit = 0;
                            }
                            else if (headingTimeToCommit > 0)
                                headingTimeToCommit -= TimeWarp.deltaTime;
                        }
                        else
                            headingChangeToCommit = 0;

                        // ============================================================ Vertical Controls ============================================================
                        if (VertActive && Utils.hasPitchInput())
                        {
                            double vert = 0; // = double.Parse(targetVert);
                            if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                                vert = AsstList.Altitude.GetAsst(this).SetPoint / 10;
                            else if (CurrentVertMode == VertMode.VSpeed)
                                vert = AsstList.VertSpeed.GetAsst(this).SetPoint;

                            if (GameSettings.PITCH_DOWN.GetKey())
                                vert -= vertScale * scale;
                            else if (GameSettings.PITCH_UP.GetKey())
                                vert += vertScale * scale;
                            else if (!Utils.IsNeutral(GameSettings.AXIS_PITCH))
                                vert += vertScale * scale * GameSettings.AXIS_PITCH.GetAxis();

                            if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                            {
                                vert = Math.Max(vert * 10, 0);
                                AsstList.Altitude.GetAsst(this).SetPoint = vert;
                            }
                            else
                                AsstList.VertSpeed.GetAsst(this).SetPoint = vert;
                            targetVert = vert.ToString("0.00");
                        }

                        // ============================================================ Throttle Controls ============================================================
                        if (ThrtActive && Utils.hasThrottleInput())
                        {
                            double speed;
                            if (CurrentThrottleMode == ThrottleMode.Speed)
                                speed = AsstList.Speed.GetAsst(this).SetPoint;
                            else
                                speed = AsstList.Acceleration.GetAsst(this).SetPoint * 10;

                            if (GameSettings.THROTTLE_UP.GetKey())
                                speed += throttleScale * scale;
                            else if (GameSettings.THROTTLE_DOWN.GetKey())
                                speed -= throttleScale * scale;
                            if (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey())
                                speed = 0;

                            if (CurrentThrottleMode == ThrottleMode.Speed)
                            {
                                if (GameSettings.THROTTLE_FULL.GetKeyDown())
                                    speed = 2400;
                                AsstList.Speed.GetAsst(this).SetPoint = speed;
                                targetSpeed = Math.Max(speed, 0).ToString("0.00");
                            }
                            else
                            {
                                speed /= 10;
                                AsstList.Acceleration.GetAsst(this).SetPoint = speed;
                                targetSpeed = speed.ToString("0.00");
                            }
                        }
                    }
                    if (BindingManager.bindings[(int)bindingIndex.Pause].isPressed && !MapView.MapIsEnabled)
                    {
                        bPause = !bPause;
                        if (!bPause)
                        {
                            hdgModeChanged(CurrentHrztMode, HrztActive);
                            vertModeChanged(CurrentVertMode, VertActive);
                            throttleModeChanged(CurrentThrottleMode, ThrtActive);
                        }
                        GeneralUI.postMessage(bPause ? "Pilot Assistant: Control Paused" : "Pilot Assistant: Control Unpaused");

                        if (bPause)
                        {
                            InputLockManager.RemoveControlLock(yawLockID);
                            InputLockManager.RemoveControlLock(pitchLockID);
                            pitchLockEngaged = false;
                            yawLockEngaged = false;
                        }
                        else
                        {
                            if (HrztActive)
                            {
                                InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);
                                yawLockEngaged = true;
                            }
                            if (VertActive)
                            {
                                InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);
                                pitchLockEngaged = true;
                            }
                        }
                    }

                    if (BindingManager.bindings[(int)bindingIndex.HdgTgl].isPressed)
                        hdgModeChanged(CurrentHrztMode, !HrztActive);
                    if (BindingManager.bindings[(int)bindingIndex.VertTgl].isPressed)
                        vertModeChanged(CurrentVertMode, !VertActive);
                    if (BindingManager.bindings[(int)bindingIndex.ThrtTgl].isPressed)
                        throttleModeChanged(CurrentThrottleMode, !ThrtActive);
                }
            }
        }

        private void hdgModeChanged(HrztMode newMode, bool active, bool setTarget = true)
        {
            headingEdit = false;

            AsstList.HdgBank.GetAsst(this).skipDerivative = true;
            AsstList.BankToYaw.GetAsst(this).skipDerivative = true;
            AsstList.Aileron.GetAsst(this).skipDerivative = true;
            AsstList.Rudder.GetAsst(this).skipDerivative = true;

            if (!active)
            {
                InputLockManager.RemoveControlLock(yawLockID);
                yawLockEngaged = false;
                stopHdgShift = true;
                AsstList.HdgBank.GetAsst(this).Clear();
                AsstList.BankToYaw.GetAsst(this).Clear();
                AsstList.Aileron.GetAsst(this).Clear();
                AsstList.Rudder.GetAsst(this).Clear();
            }
            else
            {
                if (active && !HrztActive)
                {
                    InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);
                    yawLockEngaged = true;
                }

                switch (newMode)
                {
                    case HrztMode.HeadingNum:
                    case HrztMode.Heading:
                        if (setTarget)
                            StartCoroutine(shiftHeadingTarget(vesRef.vesselData.heading));
                        bPause = false;
                        break;
                    case HrztMode.WingsLevel:
                        bPause = false;
                        break;
                }
            }
            HrztActive = active;
            CurrentHrztMode = newMode;
        }

        private void vertModeChanged(VertMode newMode, bool active, bool setTarget = true)
        {
            AsstList.VertSpeed.GetAsst(this).skipDerivative = true;
            AsstList.Elevator.GetAsst(this).skipDerivative = true;
            AsstList.Altitude.GetAsst(this).skipDerivative = true;

            if (!active)
            {
                InputLockManager.RemoveControlLock(pitchLockID);
                pitchLockEngaged = false;
                AsstList.Altitude.GetAsst(this).Clear();
                AsstList.VertSpeed.GetAsst(this).Clear();
                AsstList.Elevator.GetAsst(this).Clear();
            }
            else
            {
                if (active && !VertActive)
                {
                    InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);
                    pitchLockEngaged = true;
                }

                switch (newMode)
                {
                    case VertMode.VSpeed:
                        {
                            bPause = false;
                            AsstList.VertSpeed.GetAsst(this).Preset(-vesRef.vesselData.AoA);
                            AsstList.Elevator.GetAsst(this).Preset(pitchSet);
                            if (setTarget)
                            {
                                AsstList.VertSpeed.GetAsst(this).SetPoint = vesRef.vesselData.vertSpeed + vesRef.vesselData.AoA / AsstList.VertSpeed.GetAsst(this).PGain;
                                AsstList.VertSpeed.GetAsst(this).BumplessSetPoint = vesRef.vesselData.vertSpeed;
                            }
                            targetVert = AsstList.VertSpeed.GetAsst(this).SetPoint.ToString("0.00");
                            break;
                        }
                    case VertMode.Altitude:
                        {
                            bPause = false;
                            AsstList.Altitude.GetAsst(this).Preset(-vesRef.vesselData.vertSpeed);
                            AsstList.VertSpeed.GetAsst(this).Preset(-vesRef.vesselData.AoA);
                            AsstList.Elevator.GetAsst(this).Preset(pitchSet);
                            if (setTarget)
                            {
                                AsstList.Altitude.GetAsst(this).SetPoint = controlledVessel.altitude + vesRef.vesselData.vertSpeed / AsstList.Altitude.GetAsst(this).PGain;
                                AsstList.Altitude.GetAsst(this).BumplessSetPoint = controlledVessel.altitude;
                            }
                            targetVert = AsstList.Altitude.GetAsst(this).SetPoint.ToString("0.00");
                            break;
                        }
                    case VertMode.RadarAltitude:
                        {
                            if (setTarget)
                                AsstList.Altitude.GetAsst(this).SetPoint = vesRef.vesselData.radarAlt;
                            targetVert = AsstList.Altitude.GetAsst(this).SetPoint.ToString("0.00");
                            bPause = false;
                            break;
                        }
                }
            }
            VertActive = active;
            CurrentVertMode = newMode;
        }

        private void throttleModeChanged(ThrottleMode newMode, bool active, bool setTarget = true)
        {
            AsstList.Acceleration.GetAsst(this).skipDerivative = true;
            AsstList.Speed.GetAsst(this).skipDerivative = true;

            if (!active)
            {
                AsstList.Acceleration.GetAsst(this).Clear();
                AsstList.Speed.GetAsst(this).Clear();
            }
            else
            {
                switch (newMode)
                {
                    case ThrottleMode.Speed:
                        {
                            if (setTarget)
                                AsstList.Speed.GetAsst(this).SetPoint = controlledVessel.srfSpeed;
                            targetSpeed = AsstList.Speed.GetAsst(this).SetPoint.ToString("0.00");
                            break;
                        }
                    case ThrottleMode.Acceleration:
                        {
                            if (setTarget)
                                AsstList.Acceleration.GetAsst(this).SetPoint = vesRef.vesselData.acceleration;
                            targetSpeed = AsstList.Acceleration.GetAsst(this).SetPoint.ToString("0.00");
                            break;
                        }
                }
            }
            ThrtActive = active;
            CurrentThrottleMode = newMode;
        }
        #endregion

        #region Control / Fixed Update

        public void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            pitchSet = state.pitch; // last pitch ouput, used for presetting the elevator
            if (bPause || controlledVessel.srfSpeed < 1 || !controlledVessel.IsControllable)
                return;
            bool useIntegral = !controlledVessel.checkLanded() && controlledVessel.IsControllable;
            // Heading Control
            if (HrztActive)
            {
                if (CurrentHrztMode == HrztMode.Heading || CurrentHrztMode == HrztMode.HeadingNum)
                {
                    // calculate the bank angle response based on the current heading
                    double hdgBankResponse;
                    if (CurrentHrztMode == HrztMode.Heading)
                        hdgBankResponse = AsstList.HdgBank.GetAsst(this).ResponseD(Utils.CurrentAngleTargetRel(vesRef.vesselData.progradeHeading, AsstList.HdgBank.GetAsst(this).SetPoint, 180), useIntegral);
                    else
                        hdgBankResponse = AsstList.HdgBank.GetAsst(this).ResponseD(vesRef.vesselData.progradeHeading, useIntegral);
                    // aileron setpoint updated, bank angle also used for yaw calculations (don't go direct to rudder because we want yaw stabilisation *or* turn assistance)
                    AsstList.BankToYaw.GetAsst(this).SetPoint = AsstList.Aileron.GetAsst(this).SetPoint = hdgBankResponse;
                    AsstList.Rudder.GetAsst(this).SetPoint = -AsstList.BankToYaw.GetAsst(this).ResponseD(vesRef.vesselData.yaw, useIntegral);
                }
                else
                {
                    AsstList.Aileron.GetAsst(this).SetPoint = 0;
                    AsstList.Rudder.GetAsst(this).SetPoint = 0;
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

                if (!vesRef.vesselRef.checkLanded())
                {
                    state.roll = (AsstList.Aileron.GetAsst(this).ResponseF(-vesRef.vesselData.bank, useIntegral) + rollInput).Clamp(-1, 1);
                    state.yaw = AsstList.Rudder.GetAsst(this).ResponseF(vesRef.vesselData.yaw, useIntegral).Clamp(-1, 1);
                }
            }

            if (VertActive)
            {
                if (CurrentVertMode != VertMode.RadarAltitude)
                {
                    if (CurrentVertMode == VertMode.Altitude)
                        AsstList.VertSpeed.GetAsst(this).SetPoint = Utils.Clamp(-AsstList.Altitude.GetAsst(this).ResponseD(vesRef.vesselRef.altitude, useIntegral), vesRef.vesselRef.srfSpeed * -0.9, vesRef.vesselRef.srfSpeed * 0.9);
                    AsstList.Elevator.GetAsst(this).SetPoint = -AsstList.VertSpeed.GetAsst(this).ResponseD(vesRef.vesselData.vertSpeed, useIntegral);
                }
                else
                {
                    AsstList.VertSpeed.GetAsst(this).SetPoint = Utils.Clamp(getClimbRateForConstAltitude() - AsstList.Altitude.GetAsst(this).ResponseD(vesRef.vesselData.radarAlt * Vector3.Dot(vesRef.vesselData.surfVelForward, controlledVessel.srf_velocity.normalized), useIntegral), -controlledVessel.srfSpeed * 0.95, controlledVessel.srfSpeed * 0.95);
                    AsstList.Elevator.GetAsst(this).SetPoint = -AsstList.VertSpeed.GetAsst(this).ResponseD(vesRef.vesselData.vertSpeed, useIntegral);
                }
                state.pitch = -AsstList.Elevator.GetAsst(this).ResponseF(vesRef.vesselData.AoA, useIntegral).Clamp(-1, 1);
            }

            if (ThrtActive)
            {
                if (controlledVessel.ActionGroups[KSPActionGroup.Brakes])
                    state.mainThrottle = 0;
                else if (CurrentThrottleMode == ThrottleMode.Speed)
                {
                    if (!(AsstList.Speed.GetAsst(this).SetPoint == 0 && controlledVessel.srfSpeed < -AsstList.Acceleration.GetAsst(this).OutMin))
                    {
                        AsstList.Acceleration.GetAsst(this).SetPoint = -AsstList.Speed.GetAsst(this).ResponseD(controlledVessel.srfSpeed, useIntegral);
                        state.mainThrottle = (-AsstList.Acceleration.GetAsst(this).ResponseF(vesRef.vesselData.acceleration, useIntegral)).Clamp(0, 1);
                    }
                    else
                        state.mainThrottle = 0;
                }
                else
                    state.mainThrottle = (-AsstList.Acceleration.GetAsst(this).ResponseF(vesRef.vesselData.acceleration, useIntegral)).Clamp(0, 1);
            }
        }

        IEnumerator shiftHeadingTarget(double newHdg)
        {
            double finalTarget, target, remainder;
            headingEdit = false;
            stopHdgShift = false;
            if (hdgShiftIsRunning)
            {
                // get current remainder
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData);
                target = Utils.calculateTargetHeading(currentDirectionTarget, vesRef.vesselData);
                remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                // set new direction
                newDirectionTarget = Utils.vecHeading(newHdg, vesRef.vesselData);
                // get new remainder, reset increment only if the sign changed
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData);
                double tempRemainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                if (Math.Sign(remainder) != Math.Sign(tempRemainder))
                {
                    currentDirectionTarget = Utils.vecHeading((vesRef.vesselData.heading + vesRef.vesselData.bank / AsstList.HdgBank.GetAsst(this).PGain).headingClamp(360), vesRef.vesselData);
                    increment = 0;
                }
                yield break;
            }
            else
            {
                currentDirectionTarget = Utils.vecHeading((vesRef.vesselData.heading - vesRef.vesselData.bank / AsstList.HdgBank.GetAsst(this).PGain).headingClamp(360), vesRef.vesselData);
                newDirectionTarget = Utils.vecHeading(newHdg, vesRef.vesselData);
                increment = 0;
                hdgShiftIsRunning = true;
            }

            while (!stopHdgShift && Math.Abs(Vector3.Angle(currentDirectionTarget, newDirectionTarget)) > 0.01)
            {
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData);
                target = Utils.calculateTargetHeading(currentDirectionTarget, vesRef.vesselData);
                increment += AsstList.HdgBank.GetAsst(this).Easing * TimeWarp.fixedDeltaTime * 0.01;

                remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                if (remainder < 0)
                    target += Math.Max(-1 * increment, remainder);
                else
                    target += Math.Min(increment, remainder);

                currentDirectionTarget = Utils.vecHeading(target, vesRef.vesselData);
                yield return new WaitForFixedUpdate();
            }
            if (!stopHdgShift)
                currentDirectionTarget = newDirectionTarget;
            hdgShiftIsRunning = false;
        }

        double getClimbRateForConstAltitude()
        {
            // work out angle for ~1s to approach the point
            double angle = Math.Min(Math.Atan(4 * controlledVessel.horizontalSrfSpeed / vesRef.vesselData.radarAlt), 1.55); // 1.55 is ~89 degrees
            if (double.IsNaN(angle) || angle < 0.25) // 0.25 is 14.3 degrees
                return 0; // fly without predictive if high/slow
            else
            {
                double slope = 0;
                terrainSlope(angle, out slope);
                return slope * controlledVessel.horizontalSrfSpeed;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="angle">angle in radians to ping. 0 is straight down</param>
        /// <param name="slope">the calculated terrain slope</param>
        /// <returns>true if an object was encountered</returns>
        bool terrainSlope(double angle, out double slope)
        {
            slope = 0;
            angle += vesRef.vesselData.pitch * Math.PI / 180;
            double RayDist = findTerrainDistAtAngle((float)(angle * 180 / Math.PI), 10000);
            double AltAhead = 0;
            if (RayDist == -1)
                return false;
            else
            {
                AltAhead = RayDist * Math.Cos(angle);
                if (controlledVessel.mainBody.ocean)
                    AltAhead = Math.Min(AltAhead, controlledVessel.altitude);
            }
            slope = (vesRef.vesselData.radarAlt - AltAhead) / (AltAhead * Math.Tan(angle));
            return true;
        }

        /// <summary>
        /// raycast from vessel CoM along the given angle, returns the distance at which terrain is detected (-1 if never detected). Angle is degrees to rotate forwards from vertical
        /// </summary>
        float findTerrainDistAtAngle(float angle, float maxDist)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, -vesRef.vesselData.surfVelRight) * -vesRef.vesselData.planetUp;
            Vector3 origin = controlledVessel.rootPart.transform.position;
            RaycastHit hitInfo;
            if (FlightGlobals.ready && Physics.Raycast(origin, direction, out hitInfo, maxDist, ~1)) // ~1 masks off layer 0 which is apparently the parts on the current vessel. Seems to work
                return hitInfo.distance;
            return -1;
        }

        #endregion

        #region GUI
        public void drawGUI()
        {
            if (!PilotAssistantFlightCore.bDisplayAssistant)
                return;

            if (Event.current.type == EventType.Layout)
            {
                bMinimiseHdg = maxHdgScrollbarHeight == 10;
                bMinimiseVert = maxVertScrollbarHeight == 10;
                bMinimiseThrt = maxThrtScrollbarHeight == 10;
            }

            // main window
            #region Main Window resizing (scroll views dont work nicely with GUILayout)
            // Have to put the width changes before the draw so the close button is correctly placed
            float width;
            if (showPIDLimits && controllers.Any(c => controllerVisible(c))) // use two column view if show limits option and a controller is open
                width = 340;
            else
                width = 210;
            if (bShowHdg && dragID != 1)
            {
                hdgScrollHeight = 0; // no controllers visible when in wing lvl mode unless ctrl surf's are there
                if (CurrentHrztMode != HrztMode.WingsLevel)
                {
                    hdgScrollHeight += AsstList.HdgBank.GetAsst(this).bShow ? 168 : 29;
                    hdgScrollHeight += AsstList.BankToYaw.GetAsst(this).bShow ? 140 : 27;
                }
                if (showControlSurfaces)
                {
                    hdgScrollHeight += AsstList.Aileron.GetAsst(this).bShow ? 168 : 29;
                    hdgScrollHeight += AsstList.Rudder.GetAsst(this).bShow ? 168 : 27;
                }
            }
            if (bShowVert && dragID != 2)
            {
                vertScrollHeight = 0;
                if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                    vertScrollHeight += AsstList.Altitude.GetAsst(this).bShow ? 168 : 27;
                vertScrollHeight += AsstList.VertSpeed.GetAsst(this).bShow ? 168 : 29;
                if (showControlSurfaces)
                    vertScrollHeight += AsstList.Elevator.GetAsst(this).bShow ? 168 : 27;
            }
            if (bShowThrottle && dragID != 3)
            {
                thrtScrollHeight = 0;
                if (CurrentThrottleMode == ThrottleMode.Speed)
                    thrtScrollHeight += AsstList.Speed.GetAsst(this).bShow ? 168 : 27;
                thrtScrollHeight += AsstList.Acceleration.GetAsst(this).bShow ? 168 : 29;
            }
            #endregion

            window = GUILayout.Window(34244, window, displayWindow, "", GeneralUI.UISkin.box, GUILayout.Height(0), GUILayout.Width(width));

            // tooltip window. Label skin is transparent so it's only drawing what's inside it
            if (tooltip != "" && PilotAssistantFlightCore.showTooltips)
                GUILayout.Window(34246, new Rect(window.x + window.width, Screen.height - Input.mousePosition.y, 300, 0), tooltipWindow, "", GeneralUI.UISkin.label);

            if (showPresets)
            {
                // move the preset window to sit to the right of the main window, with the tops level
                presetWindow.x = window.x + window.width;
                presetWindow.y = window.y;

                presetWindow = GUILayout.Window(34245, presetWindow, displayPresetWindow, "", GeneralUI.UISkin.box, GUILayout.Width(200));
            }
        }

        private bool controllerVisible(AsstController controller)
        {
            if (!controller.bShow)
                return false;
            switch (controller.ctrlID)
            {
                case AsstList.HdgBank:
                case AsstList.BankToYaw:
                    return bShowHdg && CurrentHrztMode != HrztMode.WingsLevel;
                case AsstList.Aileron:
                case AsstList.Rudder:
                    return bShowHdg && showControlSurfaces;
                case AsstList.Altitude:
                    return bShowVert && (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude);
                case AsstList.VertSpeed:
                    return bShowVert;
                case AsstList.Elevator:
                    return bShowVert && showControlSurfaces;
                case AsstList.Speed:
                    return bShowThrottle && CurrentThrottleMode == ThrottleMode.Speed;
                case AsstList.Acceleration:
                    return bShowThrottle;
                default:
                    return true;
            }
        }

        private void displayWindow(int id)
        {
            GUILayout.BeginHorizontal();

            bLockInput = GUILayout.Toggle(bLockInput, new GUIContent("L", "Lock Keyboard Input"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            showPIDLimits = GUILayout.Toggle(showPIDLimits, new GUIContent("L", "Show/Hide PID Limits"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            showControlSurfaces = GUILayout.Toggle(showControlSurfaces, new GUIContent("C", "Show/Hide Control Surfaces"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            doublesided = GUILayout.Toggle(doublesided, new GUIContent("S", "Separate Min and Max limits"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            PilotAssistantFlightCore.showTooltips = GUILayout.Toggle(PilotAssistantFlightCore.showTooltips, new GUIContent("T", "Show/Hide Tooltips"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            GUILayout.FlexibleSpace();
            showPresets = GUILayout.Toggle(showPresets, new GUIContent("P", "Show/Hide Presets"), GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle]);
            if (GUILayout.Button("X", GeneralUI.UISkin.customStyles[(int)myStyles.redButtonText]))
                PilotAssistantFlightCore.bDisplayAssistant = false;
            GUILayout.EndHorizontal();

            if (bPause)
                GUILayout.Box("CONTROL PAUSED", GeneralUI.UISkin.customStyles[(int)myStyles.labelAlert]);

            #region Hdg GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowHdg = GUILayout.Toggle(bShowHdg, bShowHdg ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

            if (HrztActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Roll and Yaw Control", GUILayout.Width(186)))
                hdgModeChanged(CurrentHrztMode, !HrztActive);

            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowHdg)
            {
                if (!bMinimiseHdg)
                {
                    HrztMode tempMode = (HrztMode)GUILayout.SelectionGrid((int)CurrentHrztMode, hrztLabels, 3, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                    if (CurrentHrztMode != tempMode)
                        hdgModeChanged(tempMode, HrztActive);
                }
                if (CurrentHrztMode == HrztMode.Heading || CurrentHrztMode == HrztMode.HeadingNum)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Hdg: ", GUILayout.Width(90)))
                    {
                        double newHdg;
                        if (double.TryParse(targetHeading, out newHdg))
                        {
                            StartCoroutine(shiftHeadingTarget(newHdg.headingClamp(360)));
                            hdgModeChanged(CurrentHrztMode, true, false);

                            GUI.FocusControl("Target Hdg: ");
                            GUI.UnfocusWindow();
                        }
                    }

                    double displayTargetDelta = 0; // active setpoint or absolute value to change (yaw L/R input)
                    string displayTarget = "0.00"; // target setpoint or setpoint to commit as target setpoint

                    if (headingChangeToCommit != 0)
                        displayTargetDelta = headingChangeToCommit;
                    else if (CurrentHrztMode == HrztMode.Heading)
                    {
                        if (!hdgShiftIsRunning)
                            displayTargetDelta = AsstList.HdgBank.GetAsst(this).SetPoint - vesRef.vesselData.heading;
                        else
                            displayTargetDelta = Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData) - vesRef.vesselData.heading;

                        displayTargetDelta = displayTargetDelta.headingClamp(180);
                    }

                    if (headingEdit)
                        displayTarget = targetHeading;
                    else if (headingChangeToCommit == 0 || controlledVessel.checkLanded())
                        displayTarget = Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData).ToString("0.00");
                    else
                        displayTarget = (Utils.calculateTargetHeading(newDirectionTarget, vesRef.vesselData) + headingChangeToCommit).headingClamp(360).ToString("0.00");

                    targetHeading = GUILayout.TextField(displayTarget, GUILayout.Width(51));
                    if (targetHeading != displayTarget)
                        headingEdit = true;
                    GUILayout.Label(displayTargetDelta.ToString("0.00"), GeneralUI.UISkin.customStyles[(int)myStyles.greenTextBox], GUILayout.Width(51));
                    GUILayout.EndHorizontal();
                }

                if (!bMinimiseHdg)
                {
                    HdgScrollbar = GUILayout.BeginScrollView(HdgScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(hdgScrollHeight, maxHdgScrollbarHeight)));
                    if (CurrentHrztMode != HrztMode.WingsLevel)
                    {
                        drawPIDvalues(AsstList.HdgBank, "Heading", "\u00B0", vesRef.vesselData.heading, 2, "Bank", "\u00B0");
                        drawPIDvalues(AsstList.BankToYaw, "Yaw", "\u00B0", vesRef.vesselData.yaw, 2, "Yaw", "\u00B0", true, false);
                    }
                    if (showControlSurfaces)
                    {
                        drawPIDvalues(AsstList.Aileron, "Bank", "\u00B0", vesRef.vesselData.bank, 3, "Deflection", "\u00B0");
                        drawPIDvalues(AsstList.Rudder, "Yaw", "\u00B0", vesRef.vesselData.yaw, 3, "Deflection", "\u00B0");
                    }
                    GUILayout.EndScrollView();
                }
                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (dragID == 0 && Event.current.button == 0)
                    {
                        dragID = 1;
                        dragStart = Input.mousePosition.y;
                        maxHdgScrollbarHeight = hdgScrollHeight = Math.Min(maxHdgScrollbarHeight, hdgScrollHeight);
                    }
                }
                if (dragID == 1)
                {
                    if (Input.GetMouseButtonUp(0))
                        dragID = 0;
                    else
                    {
                        float height = Math.Max(Input.mousePosition.y, 0);
                        maxHdgScrollbarHeight += dragStart - height;
                        hdgScrollHeight = maxHdgScrollbarHeight = Mathf.Clamp(maxHdgScrollbarHeight, 10, 500);
                        if (maxHdgScrollbarHeight > 10)
                            dragStart = height;
                    }
                }
                
                AsstList.Aileron.GetAsst(this).OutMin = Math.Min(Math.Max(AsstList.Aileron.GetAsst(this).OutMin, -1), 1);
                AsstList.Aileron.GetAsst(this).OutMax = Math.Min(Math.Max(AsstList.Aileron.GetAsst(this).OutMax, -1), 1);

                AsstList.Rudder.GetAsst(this).OutMin = Math.Min(Math.Max(AsstList.Rudder.GetAsst(this).OutMin, -1), 1);
                AsstList.Rudder.GetAsst(this).OutMax = Math.Min(Math.Max(AsstList.Rudder.GetAsst(this).OutMax, -1), 1);
            }
            #endregion
            #region Pitch GUI

            GUILayout.BeginHorizontal();

            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowVert = GUILayout.Toggle(bShowVert, bShowVert ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));

            if (VertActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Vertical Control", GUILayout.Width(186)))
                vertModeChanged(CurrentVertMode, !VertActive);
           
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();
            if (bShowVert)
            {
                if (!bMinimiseVert)
                {
                    VertMode tempMode = (VertMode)GUILayout.SelectionGrid((int)CurrentVertMode, vertLabels, 3, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                    if (tempMode != CurrentVertMode)
                        vertModeChanged(tempMode, VertActive);
                }
                GUILayout.BeginHorizontal();
                string buttonString = "Target ";
                if (CurrentVertMode == VertMode.VSpeed)
                    buttonString += "Speed";
                else if (CurrentVertMode == VertMode.Altitude)
                    buttonString += "Altitude";
                else if (CurrentVertMode == VertMode.RadarAltitude)
                    buttonString += "Radar Alt";

                if (GUILayout.Button(buttonString, GUILayout.Width(118)))
                {
                    ScreenMessages.PostScreenMessage(buttonString + " updated");

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                    {
                        if (CurrentVertMode == VertMode.Altitude)
                            AsstList.Altitude.GetAsst(this).SetPoint = controlledVessel.altitude;
                        AsstList.Altitude.GetAsst(this).BumplessSetPoint = newVal;
                    }
                    else
                    {
                        AsstList.VertSpeed.GetAsst(this).SetPoint = controlledVessel.verticalSpeed + vesRef.vesselData.AoA / AsstList.VertSpeed.GetAsst(this).PGain;
                        AsstList.VertSpeed.GetAsst(this).BumplessSetPoint = newVal;
                    }
                    vertModeChanged(CurrentVertMode, true, false);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                if (!bMinimiseVert)
                {
                    VertScrollbar = GUILayout.BeginScrollView(VertScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(vertScrollHeight, maxVertScrollbarHeight)));
                    if (CurrentVertMode == VertMode.RadarAltitude)
                        drawPIDvalues(AsstList.Altitude, "RAltitude", "m", vesRef.vesselData.radarAlt, 2, "Speed ", "m/s", true);
                    if (CurrentVertMode == VertMode.Altitude)
                        drawPIDvalues(AsstList.Altitude, "Altitude", "m", controlledVessel.altitude, 2, "Speed ", "m/s", true);
                    drawPIDvalues(AsstList.VertSpeed, "Vertical Speed", "m/s", vesRef.vesselData.vertSpeed, 2, "AoA", "\u00B0", true);

                    if (showControlSurfaces)
                        drawPIDvalues(AsstList.Elevator, "Angle of Attack", "\u00B0", vesRef.vesselData.AoA, 3, "Deflection", "\u00B0", true);

                    AsstList.Elevator.GetAsst(this).OutMin = Math.Min(Math.Max(AsstList.Elevator.GetAsst(this).OutMin, -1), 1);
                    AsstList.Elevator.GetAsst(this).OutMax = Math.Min(Math.Max(AsstList.Elevator.GetAsst(this).OutMax, -1), 1);

                    GUILayout.EndScrollView();
                }

                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (dragID == 0 && Event.current.button == 0)
                    {
                        dragID = 2;
                        dragStart = Input.mousePosition.y;
                        maxVertScrollbarHeight = vertScrollHeight = Math.Min(maxVertScrollbarHeight, vertScrollHeight);
                    }
                }
                if (dragID == 2)
                {
                    if (Input.GetMouseButtonUp(0))
                        dragID = 0;
                    else
                    {
                        float height = Math.Max(Input.mousePosition.y, 0);
                        maxVertScrollbarHeight += dragStart - height;
                        vertScrollHeight = maxVertScrollbarHeight = Mathf.Clamp(maxVertScrollbarHeight, 10, 500);
                        if (maxVertScrollbarHeight > 10)
                            dragStart = height;
                    }
                }
            }
            #endregion

            #region Throttle GUI

            GUILayout.BeginHorizontal();
            // button background
            GUI.backgroundColor = GeneralUI.HeaderButtonBackground;
            bShowThrottle = GUILayout.Toggle(bShowThrottle, bShowThrottle ? "-" : "+", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(20));
            if (ThrtActive)
                GUI.backgroundColor = GeneralUI.ActiveBackground;
            else
                GUI.backgroundColor = GeneralUI.InActiveBackground;
            if (GUILayout.Button("Throttle Control", GUILayout.Width(186)))
                throttleModeChanged(CurrentThrottleMode, !ThrtActive);
            // reset colour
            GUI.backgroundColor = GeneralUI.stockBackgroundGUIColor;
            GUILayout.EndHorizontal();

            if (bShowThrottle)
            {
                if (!bMinimiseThrt)
                {
                    ThrottleMode tempMode = (ThrottleMode)GUILayout.SelectionGrid((int)CurrentThrottleMode, throttleLabels, 2, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                    if (tempMode != CurrentThrottleMode)
                        throttleModeChanged(tempMode, ThrtActive);
                }
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((CurrentThrottleMode != ThrottleMode.Acceleration) ? "Target Speed:" : "Target Accel", GUILayout.Width(118)))
                {
                    GeneralUI.postMessage((CurrentThrottleMode != ThrottleMode.Acceleration) ? "Target Speed updated" : "Target Acceleration updated");

                    double newVal;
                    double.TryParse(targetSpeed, out newVal);
                    if (CurrentThrottleMode != ThrottleMode.Acceleration)
                    {
                        AsstList.Speed.GetAsst(this).SetPoint = controlledVessel.srfSpeed;
                        AsstList.Speed.GetAsst(this).BumplessSetPoint = newVal;
                    }
                    else
                    {
                        AsstList.Acceleration.GetAsst(this).SetPoint = vesRef.vesselData.acceleration;
                        AsstList.Acceleration.GetAsst(this).BumplessSetPoint = newVal;
                    }
                    throttleModeChanged(CurrentThrottleMode, true, false);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                if (!bMinimiseThrt)
                {
                    ThrtScrollbar = GUILayout.BeginScrollView(ThrtScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(thrtScrollHeight, maxThrtScrollbarHeight)));
                    if (CurrentThrottleMode == ThrottleMode.Speed)
                        drawPIDvalues(AsstList.Speed, "Speed", "m/s", controlledVessel.srfSpeed, 2, "Accel ", "m/s", true);
                    drawPIDvalues(AsstList.Acceleration, "Acceleration", " m/s/s", vesRef.vesselData.acceleration, 1, "Throttle ", "%", true);
                    // can't have people bugging things out now can we...
                    AsstList.Acceleration.GetAsst(this).OutMax = AsstList.Speed.GetAsst(this).OutMax.Clamp(-1, 0);
                    AsstList.Acceleration.GetAsst(this).OutMax = AsstList.Speed.GetAsst(this).OutMax.Clamp(-1, 0);

                    GUILayout.EndScrollView();
                }

                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (dragID == 0 && Event.current.button == 0)
                    {
                        dragID = 3;
                        dragStart = Input.mousePosition.y;
                        maxThrtScrollbarHeight = thrtScrollHeight = Math.Min(maxThrtScrollbarHeight, thrtScrollHeight);
                    }
                }
                if (dragID == 3)
                {
                    if (Input.GetMouseButtonUp(0))
                        dragID = 0;
                    else
                    {
                        float height = Math.Max(Input.mousePosition.y, 0);
                        maxThrtScrollbarHeight += dragStart - height;
                        thrtScrollHeight = maxThrtScrollbarHeight = Mathf.Clamp(maxThrtScrollbarHeight, 10, 500);
                        if (maxThrtScrollbarHeight > 10)
                            dragStart = height;
                    }
                }
            }
            #endregion

            GUI.DragWindow();
            if (Event.current.type == EventType.Repaint)
                tooltip = GUI.tooltip;
        }

        
        const string OutMaxTooltip = "The absolute maximum value the controller can output";
        const string OutMinTooltip = "The absolute minimum value the controller can output";

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDvalues(AsstList controllerid, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showTarget = true)
        {
            AsstController controller = controllerid.GetAsst(this);
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
                        PresetManager.updateAsstPreset(this);
                }
                GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(180));
            }

            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(25)))
                PresetManager.newAsstPreset(ref newPresetName, controllers, controlledVessel);
            GUILayout.EndHorizontal();

            GUILayout.Box("", GUILayout.Height(10));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadAsstPreset(PresetManager.Instance.craftPresetDict["default"].AsstPreset, this);

            GUILayout.Box("", GUILayout.Height(10));

            AsstPreset presetToDelete = null;
            foreach (AsstPreset p in PresetManager.Instance.AsstPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadAsstPreset(p, this);
                else if (GUILayout.Button("x", GUILayout.Width(25)))
                    presetToDelete = p;
                GUILayout.EndHorizontal();
            }
            if (presetToDelete != null)
            {
                PresetManager.deleteAsstPreset(presetToDelete);
                presetWindow.height = 0;
            }
        }
        #endregion
    }
}