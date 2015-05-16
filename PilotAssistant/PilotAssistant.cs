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
        Heading = 1
    }

    public enum ThrottleMode
    {
        ToggleOn = -1,
        Speed = 0,
        Acceleration = 1
    }

    class PilotAssistant
    {
        #region Globals
        private static PilotAssistant instance;
        public static PilotAssistant Instance 
        {
            get 
            {
                if (instance == null)
                    instance = new PilotAssistant();
                return instance;
            }
        }

        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            PilotAssistantFlightCore.Instance.StartCoroutine(routine);
        }

        public AsstController[] controllers = new AsstController[9];

        public bool bPause = false;
        public bool bLockInput = false;

        public bool HrztActive = false;
        public HrztMode CurrentHrztMode = HrztMode.Heading;
        GUIContent[] hrztLabels = new GUIContent[2] { new GUIContent("Lvl", "Mode: Wing Leveller"), new GUIContent("Hdg", "Mode: Heading Control") };

        public bool VertActive = false;
        public VertMode CurrentVertMode = VertMode.VSpeed;
        GUIContent[] vertLabels = new GUIContent[3] { new GUIContent("VSpd", "Mode: Vertical Speed Control"), new GUIContent("Alt", "Mode: Altitude Control"), new GUIContent("RAlt", "Mode: Radar Altitude Control") };

        public bool ThrtActive = false;
        public ThrottleMode CurrentThrottleMode = ThrottleMode.Speed;
        GUIContent[] throttleLabels = new GUIContent[2] { new GUIContent("Vel", "Mode: Velocity Control"), new GUIContent("Acc", "Mode: Acceleration Control") };

        public Rect window = new Rect(10, 130, 10, 10);

        public bool showPresets = false;
        public bool showPIDLimits = false;
        public bool showControlSurfaces = false;
        public bool doublesided = false;

        string targetVert = "0.00";
        string targetHeading = "0.00";
        string targetSpeed = "0.00";

        const string yawLockID = "Pilot Assistant Yaw Lock";
        const string pitchLockID = "Pilot Assistant Pitch Lock";

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

        bool bShowHdg = true;
        bool bShowVert = true;
        bool bShowThrottle = true;

        Vector2 HdgScrollbar = Vector2.zero;
        public float hdgScrollHeight = 55;
        public float maxHdgScrollbarHeight = 55;
        Vector2 VertScrollbar = Vector2.zero;
        public float vertScrollHeight = 55;
        public float maxVertScrollbarHeight = 55;
        Vector2 ThrtScrollbar = Vector2.zero;
        public float thrtScrollHeight = 55;
        public float maxThrtScrollbarHeight = 55;

        float dragStart = 0;
        float dragID = -1; // 1 = hdg, 2 = vert, 3 = thrt
        bool dragResizeActive = false;

        string newPresetName = "";
        Rect presetWindow = new Rect(0, 0, 200, 10);

        float pitchSet = 0;

        // Kp, Ki, Kd, Min Out, Max Out, I Min, I Max, Scalar, Easing
        public static double[] defaultHdgBankGains = { 2, 0.1, 0, -30, 30, -0.5, 0.5, 1, 1 };
        public static double[] defaultBankToYawGains = { 0, 0, 0.01, -2, 2, -0.5, 0.5, 1, 1 };
        public static double[] defaultAileronGains = { 0.02, 0.005, 0.01, -1, 1, -1, 1, 1, 1 };
        public static double[] defaultRudderGains = { 0.1, 0.08, 0.05, -1, 1, -1, 1, 1, 1 };
        public static double[] defaultAltitudeGains = { 0.15, 0.01, 0, -50, 50, 0, 0, 1, 100 };
        public static double[] defaultVSpeedGains = { 2, 0.8, 2, -15, 15, -10, 10, 1, 10 };
        public static double[] defaultElevatorGains = { 0.05, 0.01, 0.1, -1, 1, -1, 1, 1, 1 };
        public static double[] defaultSpeedGains = { 0.2, 0.08, 0.1, -10, 10, -10, 10, 1, 10 };
        public static double[] defaultAccelGains = { 0.2, 0.08, 0, -1, 0, -1, 1, 1, 1 };

        #endregion

        public void Start()
        {
            instance = this;
            Initialise();

            // Input clamps aren't part of the presets (there's no reason for them to be...). Just some sanity checking
            AsstList.Aileron.GetAsst().InMax = 180;
            AsstList.Aileron.GetAsst().InMin = -180;
            AsstList.Altitude.GetAsst().InMin = 0;
            AsstList.Speed.GetAsst().InMin = 0;
            AsstList.HdgBank.GetAsst().isHeadingControl = true; // fix for derivative freaking out when heading target flickers across 0/360

            // start the WASD monitor
            StartCoroutine(InputResponse());

            InputLockManager.RemoveControlLock(pitchLockID);
            InputLockManager.RemoveControlLock(yawLockID);
        }

        public void OnDestroy()
        {
            PresetManager.saveToFile();

            InputLockManager.RemoveControlLock(pitchLockID);
            InputLockManager.RemoveControlLock(yawLockID);

            instance = null; // static object is only for easy referencing between modules. Don't need to keep hold of it
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
            PresetManager.initDefaultPresets(new AsstPreset(controllers, "default"));

            PresetManager.saveDefaults();
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
            if (Utils.AsstIsPaused())
                return;

            // Heading setpoint updates
            if (HrztActive)
            {
                if (!FlightData.thisVessel.checkLanded())
                    AsstList.HdgBank.GetAsst().SetPoint = Utils.calculateTargetHeading(currentDirectionTarget);
                else
                    AsstList.HdgBank.GetAsst().SetPoint = FlightData.heading;

                if (!headingEdit)
                    targetHeading = AsstList.HdgBank.GetAsst().SetPoint.ToString("0.00");
            }
        }

        IEnumerator InputResponse()
        {
            while (HighLogic.LoadedSceneIsFlight)
            {
                yield return null;
                if (!(bLockInput || Utils.isFlightControlLocked()))
                {
                    if (!Utils.AsstIsPaused())
                    {
                        double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1; // normally *1, with alt is *10
                        if (FlightInputHandler.fetch.precisionMode)
                            scale = 0.1 / scale; // normally *0.1, with alt is *0.01

                        // ============================================================ Hrzt Controls ============================================================
                        if (HrztActive && !FlightData.thisVessel.checkLanded())
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
                        if (VertActive && (GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_PITCH)))
                        {
                            double vert = 0; // = double.Parse(targetVert);
                            if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                                vert = AsstList.Altitude.GetAsst().SetPoint / 10;
                            else if (CurrentVertMode == VertMode.VSpeed)
                                vert = AsstList.VertSpeed.GetAsst().SetPoint;

                            if (GameSettings.PITCH_DOWN.GetKey())
                                vert -= vertScale * scale;
                            else if (GameSettings.PITCH_UP.GetKey())
                                vert += vertScale * scale;
                            else if (!Utils.IsNeutral(GameSettings.AXIS_PITCH))
                                vert += vertScale * scale * GameSettings.AXIS_PITCH.GetAxis();

                            if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                            {
                                vert = Math.Max(vert * 10, 0);
                                AsstList.Altitude.GetAsst().SetPoint = vert;
                            }
                            else
                                AsstList.VertSpeed.GetAsst().SetPoint = vert;
                            targetVert = vert.ToString("0.00");
                        }

                        // ============================================================ Throttle Controls ============================================================
                        if (ThrtActive && ((GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey())
                                        || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown()))
                        {
                            double speed;
                            if (CurrentThrottleMode == ThrottleMode.Speed)
                                speed = AsstList.Speed.GetAsst().SetPoint;
                            else
                                speed = AsstList.Acceleration.GetAsst().SetPoint * 10;

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
                                AsstList.Speed.GetAsst().SetPoint = speed;
                                targetSpeed = Math.Max(speed, 0).ToString("0.00");
                            }
                            else
                            {
                                speed /= 10;
                                AsstList.Acceleration.GetAsst().SetPoint = speed;
                                targetSpeed = speed.ToString("0.0");
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
                        }
                        else
                        {
                            if (HrztActive)
                                InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);
                            if (VertActive)
                                InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);
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

            AsstList.HdgBank.GetAsst().skipDerivative = true;
            AsstList.BankToYaw.GetAsst().skipDerivative = true;
            AsstList.Aileron.GetAsst().skipDerivative = true;
            AsstList.Rudder.GetAsst().skipDerivative = true;

            if (!active)
            {
                InputLockManager.RemoveControlLock(yawLockID);
                stopHdgShift = true;
                AsstList.HdgBank.GetAsst().Clear();
                AsstList.BankToYaw.GetAsst().Clear();
                AsstList.Aileron.GetAsst().Clear();
                AsstList.Rudder.GetAsst().Clear();
            }
            else
            {
                if (active && !HrztActive)
                    InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);

                switch (newMode)
                {
                    case HrztMode.Heading:
                        if (setTarget)
                            StartCoroutine(shiftHeadingTarget(FlightData.heading));
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
            AsstList.VertSpeed.GetAsst().skipDerivative = true;
            AsstList.Elevator.GetAsst().skipDerivative = true;
            AsstList.Altitude.GetAsst().skipDerivative = true;

            if (!active)
            {
                InputLockManager.RemoveControlLock(pitchLockID);
                AsstList.Altitude.GetAsst().Clear();
                AsstList.VertSpeed.GetAsst().Clear();
                AsstList.Elevator.GetAsst().Clear();
            }
            else
            {
                if (active && !VertActive)
                    InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);

                switch (newMode)
                {
                    case VertMode.VSpeed:
                        {
                            bPause = false;
                            AsstList.VertSpeed.GetAsst().Preset(-FlightData.AoA);
                            AsstList.Elevator.GetAsst().Preset(pitchSet);
                            if (setTarget)
                            {
                                AsstList.VertSpeed.GetAsst().SetPoint = FlightData.vertSpeed + FlightData.AoA / AsstList.VertSpeed.GetAsst().PGain;
                                AsstList.VertSpeed.GetAsst().BumplessSetPoint = FlightData.vertSpeed;
                            }
                            targetVert = AsstList.VertSpeed.GetAsst().SetPoint.ToString("0.00");
                            break;
                        }
                    case VertMode.Altitude:
                        {
                            bPause = false;
                            AsstList.Altitude.GetAsst().Preset(-FlightData.vertSpeed);
                            AsstList.VertSpeed.GetAsst().Preset(-FlightData.AoA);
                            AsstList.Elevator.GetAsst().Preset(pitchSet);
                            if (setTarget)
                            {
                                AsstList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude + FlightData.vertSpeed / AsstList.Altitude.GetAsst().PGain;
                                AsstList.Altitude.GetAsst().BumplessSetPoint = FlightData.thisVessel.altitude;
                            }
                            targetVert = AsstList.Altitude.GetAsst().SetPoint.ToString("0.00");
                            break;
                        }
                    case VertMode.RadarAltitude:
                        {
                            if (setTarget)
                                AsstList.Altitude.GetAsst().SetPoint = FlightData.radarAlt;
                            targetVert = FlightData.radarAlt.ToString("0.00");
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
            AsstList.Acceleration.GetAsst().skipDerivative = true;
            AsstList.Speed.GetAsst().skipDerivative = true;

            if (!active)
            {
                AsstList.Acceleration.GetAsst().Clear();
                AsstList.Speed.GetAsst().Clear();
            }
            else
            {
                switch (newMode)
                {
                    case ThrottleMode.Speed:
                        {
                            if (setTarget)
                                AsstList.Speed.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                            targetSpeed = AsstList.Speed.GetAsst().SetPoint.ToString("0.00");
                            break;
                        }
                    case ThrottleMode.Acceleration:
                        {
                            if (setTarget)
                                AsstList.Acceleration.GetAsst().SetPoint = FlightData.acceleration;
                            targetSpeed = AsstList.Acceleration.GetAsst().SetPoint.ToString("0.00");
                            break;
                        }
                }
            }
            ThrtActive = active;
            CurrentThrottleMode = newMode;
        }
        #endregion

        #region Control / Fixed Update
        public void preAutoPilotEvent(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;
            FlightData.updateAttitude();
        }

        public void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (FlightData.thisVessel == null)
                return;

            pitchSet = state.pitch; // last pitch ouput, used for presetting the elevator
            if (Utils.AsstIsPaused() || FlightData.thisVessel.srfSpeed < 1 || !FlightData.thisVessel.IsControllable)
                return;

            // Heading Control
            if (HrztActive)
            {
                if (CurrentHrztMode == HrztMode.Heading)
                {
                    // calculate the bank angle response based on the current heading
                    double hdgBankResponse = AsstList.HdgBank.GetAsst().ResponseD(Utils.CurrentAngleTargetRel(FlightData.progradeHeading, AsstList.HdgBank.GetAsst().SetPoint, 180));
                    // aileron setpoint updated, bank angle also used for yaw calculations (don't go direct to rudder because we want yaw stabilisation *or* turn assistance)
                    AsstList.BankToYaw.GetAsst().SetPoint = AsstList.Aileron.GetAsst().SetPoint = hdgBankResponse;
                    AsstList.Rudder.GetAsst().SetPoint = -AsstList.BankToYaw.GetAsst().ResponseD(FlightData.yaw);
                }
                else
                {
                    AsstList.Aileron.GetAsst().SetPoint = 0;
                    AsstList.Rudder.GetAsst().SetPoint = 0;
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
                    state.roll = (AsstList.Aileron.GetAsst().ResponseF(FlightData.bank) + rollInput).Clamp(-1, 1);
                    state.yaw = AsstList.Rudder.GetAsst().ResponseF(FlightData.yaw).Clamp(-1, 1);
                }
            }

            if (VertActive)
            {
                if (CurrentVertMode != VertMode.RadarAltitude)
                {
                    if (CurrentVertMode == VertMode.Altitude)
                        AsstList.VertSpeed.GetAsst().SetPoint = -AsstList.Altitude.GetAsst().ResponseD(FlightData.thisVessel.altitude);
                    AsstList.Elevator.GetAsst().SetPoint = -AsstList.VertSpeed.GetAsst().ResponseD(FlightData.vertSpeed);
                }
                else
                {
                    AsstList.VertSpeed.GetAsst().SetPoint = getClimbRateForConstAltitude() - AsstList.Altitude.GetAsst().ResponseD(FlightData.radarAlt);
                    AsstList.Elevator.GetAsst().SetPoint = -AsstList.VertSpeed.GetAsst().ResponseD(FlightData.vertSpeed);
                }
                state.pitch = -AsstList.Elevator.GetAsst().ResponseF(FlightData.AoA).Clamp(-1, 1);
            }

            if (ThrtActive)
            {
                if (CurrentThrottleMode == ThrottleMode.Speed)
                {
                    if (AsstList.Speed.GetAsst().SetPoint != 0)
                        AsstList.Acceleration.GetAsst().SetPoint = -AsstList.Speed.GetAsst().ResponseD(FlightData.thisVessel.srfSpeed);
                    else
                        AsstList.Acceleration.GetAsst().SetPoint = AsstList.Acceleration.GetAsst().OutMax;
                }
                state.mainThrottle = (-AsstList.Acceleration.GetAsst().ResponseF(FlightData.acceleration)).Clamp(0, 1);
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
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget);
                target = Utils.calculateTargetHeading(currentDirectionTarget);
                remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                // set new direction
                newDirectionTarget = Utils.vecHeading(newHdg);
                // get new remainder, reset increment only if the sign changed
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget);
                target = Utils.calculateTargetHeading(currentDirectionTarget);
                double tempRemainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
                if (Math.Sign(remainder) != Math.Sign(tempRemainder))
                {
                    currentDirectionTarget = Utils.vecHeading((FlightData.heading - FlightData.bank / AsstList.HdgBank.GetAsst().PGain).headingClamp(360));
                    increment = 0;
                }
                yield break;
            }
            else
            {
                currentDirectionTarget = Utils.vecHeading((FlightData.heading - FlightData.bank / AsstList.HdgBank.GetAsst().PGain).headingClamp(360));
                newDirectionTarget = Utils.vecHeading(newHdg);
                increment = 0;
                hdgShiftIsRunning = true;
            }

            while (!stopHdgShift && Math.Abs(Vector3.Angle(currentDirectionTarget, newDirectionTarget)) > 0.01)
            {
                finalTarget = Utils.calculateTargetHeading(newDirectionTarget);
                target = Utils.calculateTargetHeading(currentDirectionTarget);
                increment += AsstList.HdgBank.GetAsst().Easing * TimeWarp.fixedDeltaTime * 0.01;

                remainder = finalTarget - Utils.CurrentAngleTargetRel(target, finalTarget, 180);
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

        double getClimbRateForConstAltitude()
        {
            return terrainSlope(75) * FlightData.thisVessel.horizontalSrfSpeed;
        }

        /// <summary>
        /// returns slope as the ratio of vertical distance to horizontal distance (ie. meters of rise per meter forward) between ground directly below and ground x degrees ahead
        /// </summary>
        double terrainSlope(double angle)
        {
            double RayDist = findTerrainDistAtAngle((float)angle, 10000);
            double AltAhead = 0;
            if (RayDist == -1)
                AltAhead = FlightData.thisVessel.altitude;
            else
            {
                AltAhead = RayDist * Math.Cos(angle * Math.PI / 180);
                if (FlightData.thisVessel.mainBody.ocean)
                    AltAhead = Math.Min(AltAhead, FlightData.thisVessel.altitude);
            }
            double DistAhead = AltAhead * Math.Tan(angle * Math.PI / 180);
            return (FlightData.radarAlt - AltAhead) / DistAhead;
        }

        /// <summary>
        /// raycast from vessel CoM along the given angle, returns the distance at which terrain is detected (-1 if never detected). Angle is degrees to rotate forwards from vertical
        /// </summary>
        float findTerrainDistAtAngle(float angle, float maxDist)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, -FlightData.surfVelRight) * -FlightData.planetUp;
            Vector3 origin = FlightData.thisVessel.CoM;
            RaycastHit hitInfo;
            if (FlightGlobals.ready && Physics.Raycast(origin, direction, out hitInfo, maxDist, ~1)) // ~1 masks off layer 0 which is apparently the parts on the current vessel. Seems to work
                return hitInfo.distance;
            return -1;
        }

        #endregion

        #region GUI
        public void drawGUI()
        {
            if (!AppLauncherFlight.bDisplayAssistant)
                return;

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
                    hdgScrollHeight += AsstList.HdgBank.GetAsst().bShow ? 168 : 29;
                    hdgScrollHeight += AsstList.BankToYaw.GetAsst().bShow ? 140 : 27;
                }
                if (showControlSurfaces)
                {
                    hdgScrollHeight += AsstList.Aileron.GetAsst().bShow ? 168 : 29;
                    hdgScrollHeight += AsstList.Rudder.GetAsst().bShow ? 168 : 27;
                }
            }
            if (bShowVert && dragID != 2)
            {
                vertScrollHeight = 0;
                if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                    vertScrollHeight += AsstList.Altitude.GetAsst().bShow ? 168 : 27;
                vertScrollHeight += AsstList.VertSpeed.GetAsst().bShow ? 168 : 29;
                if (showControlSurfaces)
                    vertScrollHeight += AsstList.Elevator.GetAsst().bShow ? 168 : 27;
            }
            if (bShowThrottle && dragID != 3)
            {
                thrtScrollHeight = 0;
                if (CurrentThrottleMode == ThrottleMode.Speed)
                    thrtScrollHeight += AsstList.Speed.GetAsst().bShow ? 168 : 27;
                thrtScrollHeight += AsstList.Acceleration.GetAsst().bShow ? 168 : 29;
            }
            #endregion

            window = GUILayout.Window(34244, window, displayWindow, "", GeneralUI.UISkin.box, GUILayout.Height(0), GUILayout.Width(width));

            // tooltip window. Label skin is transparent so it's only drawing what's inside it
            if (tooltip != "" && PilotAssistantFlightCore.showTooltips)
                GUILayout.Window(34246, new Rect(window.x + window.width, Screen.height - Input.mousePosition.y, 0, 0), tooltipWindow, "", GeneralUI.UISkin.label, GUILayout.Height(0), GUILayout.Width(300));

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
                AppLauncherFlight.bDisplayAssistant = false;
            GUILayout.EndHorizontal();

            if (Utils.AsstIsPaused())
                GUILayout.Box("CONTROL PAUSED", GeneralUI.UISkin.customStyles[(int)myStyles.labelAlert]);

            //showPresets = GUILayout.Toggle(showPresets, showPresets ? "Hide Presets" : "Show Presets", GUILayout.Width(200));

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
                HrztMode tempMode = (HrztMode)GUILayout.SelectionGrid((int)CurrentHrztMode, hrztLabels, 2, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                if (CurrentHrztMode != tempMode)
                    hdgModeChanged(tempMode, HrztActive);
                if (CurrentHrztMode == HrztMode.Heading)
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
                            displayTargetDelta = AsstList.HdgBank.GetAsst().SetPoint - FlightData.heading;
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

                HdgScrollbar = GUILayout.BeginScrollView(HdgScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(hdgScrollHeight, maxHdgScrollbarHeight)));
                if (CurrentHrztMode != HrztMode.WingsLevel)
                {
                    drawPIDvalues(AsstList.HdgBank, "Heading", "\u00B0", FlightData.heading, 2, "Bank", "\u00B0");
                    drawPIDvalues(AsstList.BankToYaw, "Yaw", "\u00B0", FlightData.yaw, 2, "Yaw", "\u00B0", true, false);
                }
                if (showControlSurfaces)
                {
                    drawPIDvalues(AsstList.Aileron, "Bank", "\u00B0", FlightData.bank, 3, "Deflection", "\u00B0");
                    drawPIDvalues(AsstList.Rudder, "Yaw", "\u00B0", FlightData.yaw, 3, "Deflection", "\u00B0");
                }
                GUILayout.EndScrollView();

                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (!dragResizeActive && Event.current.button == 0)
                    {
                        dragResizeActive = true;
                        dragID = 1;
                        dragStart = Input.mousePosition.y;
                        maxHdgScrollbarHeight = hdgScrollHeight = Math.Min(maxHdgScrollbarHeight, hdgScrollHeight);
                    }
                }
                if (dragResizeActive && dragID == 1)
                {
                    if (Input.GetMouseButtonUp(0))
                    {
                        dragResizeActive = false;
                        dragID = -1;
                    }
                    else
                    {
                        float height = Math.Max(Input.mousePosition.y, 0);
                        maxHdgScrollbarHeight += dragStart - height;
                        hdgScrollHeight = maxHdgScrollbarHeight = Mathf.Clamp(maxHdgScrollbarHeight, 10, 500);
                        if (maxHdgScrollbarHeight > 10)
                            dragStart = height;
                    }
                }
                
                AsstList.Aileron.GetAsst().OutMin = Math.Min(Math.Max(AsstList.Aileron.GetAsst().OutMin, -1), 1);
                AsstList.Aileron.GetAsst().OutMax = Math.Min(Math.Max(AsstList.Aileron.GetAsst().OutMax, -1), 1);

                AsstList.Rudder.GetAsst().OutMin = Math.Min(Math.Max(AsstList.Rudder.GetAsst().OutMin, -1), 1);
                AsstList.Rudder.GetAsst().OutMax = Math.Min(Math.Max(AsstList.Rudder.GetAsst().OutMax, -1), 1);
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
                VertMode tempMode = (VertMode)GUILayout.SelectionGrid((int)CurrentVertMode, vertLabels, 3, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                if (tempMode != CurrentVertMode)
                    vertModeChanged(tempMode, VertActive);
                GUILayout.BeginHorizontal();
                string buttonString = "Target ";
                if (CurrentVertMode == VertMode.VSpeed)
                    buttonString += "Speed";
                else if (CurrentVertMode == VertMode.Altitude)
                    buttonString += "Altitude";
                else if (CurrentVertMode == VertMode.RadarAltitude)
                    buttonString += "Radar Alt";

                if (GUILayout.Button(buttonString, GUILayout.Width(98)))
                {
                    ScreenMessages.PostScreenMessage(buttonString + " updated");

                    double newVal;
                    double.TryParse(targetVert, out newVal);
                    if (CurrentVertMode == VertMode.Altitude)
                    {
                        AsstList.Altitude.GetAsst().SetPoint = FlightData.thisVessel.altitude + FlightData.vertSpeed / AsstList.Altitude.GetAsst().PGain;
                        AsstList.Altitude.GetAsst().BumplessSetPoint = newVal;
                    }
                    else
                    {
                        AsstList.VertSpeed.GetAsst().SetPoint = FlightData.thisVessel.verticalSpeed + FlightData.AoA / AsstList.VertSpeed.GetAsst().PGain;
                        AsstList.VertSpeed.GetAsst().BumplessSetPoint = newVal;
                    }
                    vertModeChanged(CurrentVertMode, true, false);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
                GUILayout.EndHorizontal();

                VertScrollbar = GUILayout.BeginScrollView(VertScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(vertScrollHeight, maxVertScrollbarHeight)));
                if (CurrentVertMode == VertMode.RadarAltitude)
                    drawPIDvalues(AsstList.Altitude, "RAltitude", "m", FlightData.radarAlt, 2, "Speed ", "m/s", true);
                if (CurrentVertMode == VertMode.Altitude)
                    drawPIDvalues(AsstList.Altitude, "Altitude", "m", FlightData.thisVessel.altitude, 2, "Speed ", "m/s", true);
                drawPIDvalues(AsstList.VertSpeed, "Vertical Speed", "m/s", FlightData.vertSpeed, 2, "AoA", "\u00B0", true);

                if (showControlSurfaces)
                    drawPIDvalues(AsstList.Elevator, "Angle of Attack", "\u00B0", FlightData.AoA, 3, "Deflection", "\u00B0", true);

                AsstList.Elevator.GetAsst().OutMin = Math.Min(Math.Max(AsstList.Elevator.GetAsst().OutMin, -1), 1);
                AsstList.Elevator.GetAsst().OutMax = Math.Min(Math.Max(AsstList.Elevator.GetAsst().OutMax, -1), 1);
                
                GUILayout.EndScrollView();

                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (!dragResizeActive && Event.current.button == 0)
                    {
                        dragResizeActive = true;
                        dragID = 2;
                        dragStart = Input.mousePosition.y;
                        maxVertScrollbarHeight = vertScrollHeight = Math.Min(maxVertScrollbarHeight, vertScrollHeight);
                    }
                }
                if (dragResizeActive && dragID == 2)
                {
                    if (Input.GetMouseButtonUp(0))
                    {
                        dragResizeActive = false;
                        dragID = -1;
                    }
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
                ThrottleMode tempMode = (ThrottleMode)GUILayout.SelectionGrid((int)CurrentThrottleMode, throttleLabels, 2, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                if (tempMode != CurrentThrottleMode)
                    throttleModeChanged(tempMode, ThrtActive);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((CurrentThrottleMode != ThrottleMode.Acceleration) ? "Target Speed:" : "Target Accel", GUILayout.Width(118)))
                {
                    GeneralUI.postMessage((CurrentThrottleMode != ThrottleMode.Acceleration) ? "Target Speed updated" : "Target Acceleration updated");

                    double newVal;
                    double.TryParse(targetSpeed, out newVal);
                    if (CurrentThrottleMode != ThrottleMode.Acceleration)
                    {
                        AsstList.Speed.GetAsst().SetPoint = FlightData.thisVessel.srfSpeed;
                        AsstList.Speed.GetAsst().BumplessSetPoint = newVal;
                    }
                    else
                    {
                        AsstList.Acceleration.GetAsst().SetPoint = FlightData.acceleration;
                        AsstList.Acceleration.GetAsst().BumplessSetPoint = newVal;
                    }
                    throttleModeChanged(CurrentThrottleMode, true, false);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                ThrtScrollbar = GUILayout.BeginScrollView(ThrtScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(thrtScrollHeight, maxThrtScrollbarHeight)));
                if (CurrentThrottleMode == ThrottleMode.Speed)
                    drawPIDvalues(AsstList.Speed, "Speed", "m/s", FlightData.thisVessel.srfSpeed, 2, "Accel ", "m/s", true);
                drawPIDvalues(AsstList.Acceleration, "Acceleration", " m/s/s", FlightData.acceleration, 1, "Throttle ", "%", true);
                // can't have people bugging things out now can we...
                AsstList.Acceleration.GetAsst().OutMax = AsstList.Speed.GetAsst().OutMax.Clamp(-1, 0);
                AsstList.Acceleration.GetAsst().OutMax = AsstList.Speed.GetAsst().OutMax.Clamp(-1, 0);

                GUILayout.EndScrollView();

                if (GUILayout.RepeatButton("", GUILayout.Height(8)))
                {// drag resizing code from Dmagics Contracts window + used as a template
                    if (!dragResizeActive && Event.current.button == 0)
                    {
                        dragResizeActive = true;
                        dragID = 3;
                        dragStart = Input.mousePosition.y;
                        maxThrtScrollbarHeight = thrtScrollHeight = Math.Min(maxThrtScrollbarHeight, thrtScrollHeight);
                    }
                }
                if (dragResizeActive && dragID == 3)
                {
                    if (Input.GetMouseButtonUp(0))
                    {
                        dragResizeActive = false;
                        dragID = -1;
                    }
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

        
        string OutMaxTooltip = "The absolute maximum value the controller can output";
        string OutMinTooltip = "The absolute minimum value the controller can output";

        string tooltip = "";
        private void tooltipWindow(int id)
        {
            GUILayout.Label(tooltip, GeneralUI.UISkin.textArea);
        }

        private void drawPIDvalues(AsstList controllerid, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showTarget = true)
        {
            AsstController controller = controllerid.GetAsst();
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

            GUILayout.Box("", GUILayout.Height(10));

            if (GUILayout.Button("Reset to Defaults"))
                PresetManager.loadAsstPreset(PresetManager.Instance.craftPresetDict["default"].AsstPreset);

            GUILayout.Box("", GUILayout.Height(10));

            AsstPreset presetToDelete = null;
            foreach (AsstPreset p in PresetManager.Instance.AsstPresetList)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(p.name))
                    PresetManager.loadAsstPreset(p);
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