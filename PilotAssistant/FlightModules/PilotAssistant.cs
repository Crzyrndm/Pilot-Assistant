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
        Pitch = 0,
        VSpeed = 1,
        Altitude = 2,
        RadarAltitude = 3
    }

    public enum HrztMode
    {
        ToggleOn = -1,
        Bank = 0,
        Heading = 1,
        HeadingNum = 2
    }

    public enum ThrottleMode
    {
        ToggleOn = -1,
        Direct = 0,
        Acceleration = 1,
        Speed = 2
    }

    public enum SpeedUnits
    {
        mSec,
        kmph,
        mph,
        knots,
        mach
    }

    public enum SpeedRef
    {
        True,
        Indicated,
        Equivalent,
        Mach
    }

    public class PilotAssistant
    {
        #region Globals
        public AsstVesselModule vesModule;
        void StartCoroutine(IEnumerator routine) // quick access to coroutine now it doesn't inherit Monobehaviour
        {
            vesModule.StartCoroutine(routine);
        }

        public AsstController[] controllers = new AsstController[9];
        double currentThrottlePct; // need to keep a record of this as the vessel ctrlstate value is not holding enough significance to slow down adjustments

        public bool bPause = false;
        public bool bLockInput = false;

        public bool HrztActive = false;
        public HrztMode CurrentHrztMode = HrztMode.Heading;
        static GUIContent[] hrztLabels = new GUIContent[3] { new GUIContent("Bank", "Mode: Bank Angle Control\r\n\r\nMaintains a targeted bank angle. Negative values for banking left, positive values for banking right"),
                                                                new GUIContent("Dir", "Mode: Direction Control\r\n\r\nDirection control maintains a set facing as the vessel travels around a planet. Fly in a straight line long enough and you will get back to where you started so long as sideslip is minimal.\r\nLimits maximum bank angle"),
                                                                new GUIContent("Hdg", "Mode: Heading control\r\n\r\nHeading control follows a constant compass heading. Useful for local navigation but is difficult to use over long distances due to the effects of planetary curvature.\r\nLimits maximum bank angle") };

        public bool VertActive = false;
        public VertMode CurrentVertMode = VertMode.VSpeed;
        static GUIContent[] vertLabels = new GUIContent[4] { new GUIContent("Pitch", "Mode: Pitch Control\r\n\r\nMaintains a targeted pitch angle"),
                                                                new GUIContent("VSpd", "Mode: Vertical Speed Control\r\n\r\nManages vessel angle of attack to control ascent rate.\r\nLimits vessel angle of attack"),
                                                                new GUIContent("Alt", "Mode: Altitude Control\r\n\r\nManages vessel altitude ascent rate to attain a set altitude relative to sea level.\r\nLimits vessel ascent rate"),
                                                                new GUIContent("RAlt", "Mode: Radar Altitude Control\r\n\r\nManages vessel altitude ascent rate to attain a set altitude relative to the terrain.\r\nLimits vessel ascent rate") };

        public bool ThrtActive = false;
        public ThrottleMode CurrentThrottleMode = ThrottleMode.Speed;
        static GUIContent[] throttleLabels = new GUIContent[3] { new GUIContent("Dir", "Mode: Direct Throttle Control\r\n\r\nSets vessel throttle to specified percentage"),
                                                                    new GUIContent("Acc", "Mode: Acceleration Control\r\n\r\nManages vessel throttle to attain a desired acceleration"),
                                                                    new GUIContent("Spd", "Mode: Speed Control\r\n\r\nManages vessel acceleration to attain a set speed.\r\nLimits acceleration")};

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
        const double throttleScale = 0.4; // acceleration rate is x0.1

        // Direction control vars
        Quaternion currentTarget = Quaternion.identity; // this is the body relative rotation the control is aimed at
        Quaternion newTarget = Quaternion.identity; // this is the body relative rotation we are moving to
        double increment = 0; // this is the angle to shift per second
        bool hdgShiftIsRunning = false;
        bool stopHdgShift = false;

        // don't update hdg display if true
        bool headingEdit = true;

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
        public static readonly double[] defaultHdgBankGains = { 2, 0, 0, -30, 30, -1, 1, 1, 1 };
        public static readonly double[] defaultBankToYawGains = { 0, 0, 0, -2, 2, -0.5, 0.5, 1, 1 };
        public static readonly double[] defaultAileronGains = { 0.02, 0.005, 0.01, -1, 1, -1, 1, 1, 1 };
        public static readonly double[] defaultRudderGains = { 0.1, 0.025, 0.05, -1, 1, -1, 1, 1, 1 };
        public static readonly double[] defaultAltitudeGains = { 0.15, 0, 0, -50, 50, 0, 0, 1, 100 };
        public static readonly double[] defaultVSpeedGains = { 2, 0.8, 2, -15, 15, -10, 10, 1, 10 };
        public static readonly double[] defaultElevatorGains = { 0.05, 0.01, 0.1, -1, 1, -1, 1, 2, 1 };
        public static readonly double[] defaultSpeedGains = { 0.2, 0.0, 0.0, -10, 10, -10, 10, 1, 10 };
        public static readonly double[] defaultAccelGains = { 0.2, 0.08, 0.0, -1, 0, -1, 1, 1, 1 };

        // speed mode change
        bool speedSelectWindowVisible;
        Rect speedSelectWindow;

        SpeedRef speedRef = SpeedRef.True;
        GUIContent[] speedRefLabels = new GUIContent[3] {new GUIContent("TAS"),
                                                        new GUIContent("IAS"),
                                                        new GUIContent("EAS")};
        SpeedUnits units = SpeedUnits.mSec;
        GUIContent[] speedUnitLabels = new GUIContent[5] {new GUIContent("m/s"),
                                                        new GUIContent("km/h"),
                                                        new GUIContent("mph"),
                                                        new GUIContent("kts"),
                                                        new GUIContent("mach")};
        /** Speed and acceleration accounting for TAS/IAS/EAS since calculating acceleration for modes other than TAS is not just a simple multiplier **/
        double adjustedAcceleration, adjustedSpeed;

        #endregion
        public PilotAssistant(AsstVesselModule avm)
        {
            vesModule = avm;
        }

        public void Start()
        {
            Initialise();

            InputLockManager.RemoveControlLock(pitchLockID);
            InputLockManager.RemoveControlLock(yawLockID);
            pitchLockEngaged = false;
            yawLockEngaged = false;

            PresetManager.loadCraftAsstPreset(this);
        }

        public void OnDestroy()
        {
            if (vesModule.vesselRef == FlightGlobals.ActiveVessel)
            {
                InputLockManager.RemoveControlLock(pitchLockID);
                InputLockManager.RemoveControlLock(yawLockID);
                pitchLockEngaged = false;
                yawLockEngaged = false;
            }
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

            AsstList.HdgBank.GetAsst(this).invertOutput = true;
            AsstList.Aileron.GetAsst(this).invertInput = true;
            AsstList.Altitude.GetAsst(this).invertOutput = true;
            AsstList.VertSpeed.GetAsst(this).invertOutput = true;
            AsstList.Elevator.GetAsst(this).invertOutput = true;
            AsstList.Speed.GetAsst(this).invertOutput = true;
            AsstList.Acceleration.GetAsst(this).invertOutput = true;

            AsstList.Aileron.GetAsst(this).InMax = 180;
            AsstList.Aileron.GetAsst(this).InMin = -180;
            AsstList.Altitude.GetAsst(this).InMin = 0;
            AsstList.Speed.GetAsst(this).InMin = 0;
            AsstList.HdgBank.GetAsst(this).isHeadingControl = true; // fix for derivative freaking out when heading target flickers across 0/360
            AsstList.Aileron.GetAsst(this).isHeadingControl = true;
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

        public void vesselSwitch(Vessel v)
        {
            if (HrztActive)
                InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);
            if (VertActive)
                InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);
        }

        #region Update / Input Monitoring
        public void Update()
        {
            InputResponse();

            if (bPause)
                return;

            // Heading setpoint updates
            if (HrztActive)
            {
                if (vesModule.vesselRef.LandedOrSplashed)
                    newTarget = currentTarget = Utils.getPlaneRotation(vesModule.vesselRef.transform.right, vesModule);
                if (CurrentHrztMode == HrztMode.Heading)
                {
                    AsstList.HdgBank.GetAsst(this).SetPoint = Utils.calculateTargetHeading(currentTarget, vesModule);

                    if (!headingEdit)
                        targetHeading = AsstList.HdgBank.GetAsst(this).SetPoint.ToString("0.00");
                }
            }

            if (speedSelectWindowVisible && Input.GetMouseButtonDown(0)) 
            {
                Vector2 mouse = Input.mousePosition;
                mouse.y = Screen.height - mouse.y;
                if (!speedSelectWindow.Contains(mouse))
                    speedSelectWindowVisible = false;
            }
        }

        public void InputResponse()
        {
            if (!vesModule.vesselRef.isActiveVessel || bLockInput || Utils.isFlightControlLocked() || vesModule.vesselRef.HoldPhysics)
                return;

            if (BindingManager.bindings[(int)bindingIndex.Pause].isPressed && !MapView.MapIsEnabled)
                TogglePauseCtrlState();

            if (BindingManager.bindings[(int)bindingIndex.HdgTgl].isPressed)
                hdgModeChanged(CurrentHrztMode, !HrztActive);
            if (BindingManager.bindings[(int)bindingIndex.VertTgl].isPressed)
                vertModeChanged(CurrentVertMode, !VertActive);
            if (BindingManager.bindings[(int)bindingIndex.ThrtTgl].isPressed)
                throttleModeChanged(CurrentThrottleMode, !ThrtActive);

            if (bPause)
                return;

            double scale = GameSettings.MODIFIER_KEY.GetKey() ? 10 : 1; // normally *1, with LAlt is *10
            if (FlightInputHandler.fetch.precisionMode)
                scale = 0.1 / scale; // normally *0.1, with alt is *0.01

            // ============================================================ Hrzt Controls ============================================================
            if (HrztActive && !vesModule.vesselRef.LandedOrSplashed && Utils.hasYawInput())
            {
                double hdg = GameSettings.YAW_LEFT.GetKey() ? -hrztScale * scale : 0;
                hdg += GameSettings.YAW_RIGHT.GetKey() ? hrztScale * scale : 0;
                hdg += hrztScale * scale * GameSettings.AXIS_YAW.GetAxis();

                switch (CurrentHrztMode)
                {
                    case HrztMode.Bank:
                        AsstList.Aileron.GetAsst(this).SetPoint = Utils.headingClamp(AsstList.Aileron.GetAsst(this).SetPoint + hdg / 4, 180);
                        targetHeading = AsstList.Aileron.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case HrztMode.Heading:
                        StartCoroutine(shiftHeadingTarget(Utils.calculateTargetHeading(newTarget, vesModule) + hdg));
                        break;
                    case HrztMode.HeadingNum:
                        AsstList.HdgBank.GetAsst(this).SetPoint = (AsstList.HdgBank.GetAsst(this).SetPoint + hdg).headingClamp(360);
                        targetHeading = AsstList.HdgBank.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                }
            }
            // ============================================================ Vertical Controls ============================================================
            if (VertActive && Utils.hasPitchInput())
            {
                double vert = GameSettings.PITCH_DOWN.GetKey() ? -vertScale * scale : 0;
                vert += GameSettings.PITCH_UP.GetKey() ? vertScale * scale : 0;
                vert += !Utils.IsNeutral(GameSettings.AXIS_PITCH) ? vertScale * scale * GameSettings.AXIS_PITCH.GetAxis() : 0;

                switch (CurrentVertMode)
                {
                    case VertMode.Altitude:
                    case VertMode.RadarAltitude:
                        AsstList.Altitude.GetAsst(this).SetPoint = Math.Max(AsstList.Altitude.GetAsst(this).SetPoint + vert * 10, 0);
                        targetVert = AsstList.Altitude.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case VertMode.VSpeed:
                        AsstList.VertSpeed.GetAsst(this).SetPoint += vert;
                        targetVert = AsstList.VertSpeed.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case VertMode.Pitch:
                        AsstList.Elevator.GetAsst(this).SetPoint = Utils.Clamp(AsstList.Elevator.GetAsst(this).SetPoint + vert, -90, 90);
                        targetVert = AsstList.Elevator.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                }
            }
            // ============================================================ Throttle Controls ============================================================
            if (ThrtActive && Utils.hasThrottleInput())
            {
                double speedScale = scale / (units != SpeedUnits.mach ? Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound) : 1);
                double speed = GameSettings.THROTTLE_UP.GetKey() ? throttleScale * speedScale : 0;
                speed -= GameSettings.THROTTLE_DOWN.GetKey() ? throttleScale * speedScale : 0;
                speed += GameSettings.THROTTLE_FULL.GetKeyDown() ? 100 * speedScale : 0;
                speed -= (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) ? 100 * speedScale : 0;

                switch (CurrentThrottleMode)
                {
                    case ThrottleMode.Direct:
                        currentThrottlePct = Utils.Clamp(currentThrottlePct + speed / 100, 0, 1);
                        vesModule.vesselRef.ctrlState.mainThrottle = (float)currentThrottlePct;
                        if (ReferenceEquals(vesModule.vesselRef, FlightGlobals.ActiveVessel))
                            FlightInputHandler.state.mainThrottle = (float)currentThrottlePct;
                        targetSpeed = (currentThrottlePct * 100).ToString("0.00");
                        break;
                    case ThrottleMode.Acceleration:
                        AsstList.Acceleration.GetAsst(this).SetPoint += speed / 10;
                        targetSpeed = (AsstList.Acceleration.GetAsst(this).SetPoint * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound)).ToString("0.00");
                        break;
                    case ThrottleMode.Speed:
                        AsstList.Speed.GetAsst(this).SetPoint = Math.Max(AsstList.Speed.GetAsst(this).SetPoint + speed, 0);
                        targetSpeed = (AsstList.Speed.GetAsst(this).SetPoint * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound)).ToString("0.00");
                        break;
                }
            }
        }

        private void hdgModeChanged(HrztMode newMode, bool active, bool setTarget = true)
        {
            AsstList.HdgBank.GetAsst(this).skipDerivative = true;
            AsstList.BankToYaw.GetAsst(this).skipDerivative = true;
            AsstList.Aileron.GetAsst(this).skipDerivative = true;
            AsstList.Rudder.GetAsst(this).skipDerivative = true;

            if (!active)
            {
                InputLockManager.RemoveControlLock(yawLockID);
                yawLockEngaged = false;
                stopHdgShift = true;
                headingEdit = true;
                AsstList.HdgBank.GetAsst(this).Clear();
                AsstList.BankToYaw.GetAsst(this).Clear();
                AsstList.Aileron.GetAsst(this).Clear();
                AsstList.Rudder.GetAsst(this).Clear();
            }
            else
            {
                if (!yawLockEngaged)
                {
                    InputLockManager.SetControlLock(ControlTypes.YAW, yawLockID);
                    yawLockEngaged = true;
                }
                bPause = false;
                switch (newMode)
                {
                    case HrztMode.HeadingNum:
                        if (setTarget)
                            AsstList.HdgBank.GetAsst(this).SetPoint = vesModule.vesselData.heading;
                        targetHeading = AsstList.HdgBank.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case HrztMode.Heading:
                        if (setTarget)
                            StartCoroutine(shiftHeadingTarget(vesModule.vesselData.heading));
                        break;
                    case HrztMode.Bank:
                        if (setTarget)
                            AsstList.Aileron.GetAsst(this).SetPoint = vesModule.vesselData.bank;
                        targetHeading = AsstList.Aileron.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                }
            }
            HrztActive = active;
            CurrentHrztMode = newMode;
        }

        public double GetCurrentHrzt()
        {
            switch (CurrentHrztMode)
            {
                case HrztMode.HeadingNum:
                case HrztMode.Heading:
                    return AsstList.HdgBank.GetAsst(this).SetPoint;
                case HrztMode.Bank:
                default:
                    return AsstList.Aileron.GetAsst(this).SetPoint;
            }
        }

        /// <summary>
        /// API handle for RPM
        /// </summary>
        /// <param name="active">Sets the state of the heading control system. True = enabled</param>
        /// <param name="setTarget">Whether to update the target value</param>
        /// <param name="mode">Mode to use. Heading, bank, etc.</param>
        /// <param name="target">The new target value</param>
        public void SetHrzt(bool active, bool setTarget, HrztMode mode, double target)
        {
            if (setTarget)
            {
                switch (mode)
                {
                    case HrztMode.Bank:
                        AsstList.Aileron.GetAsst(this).BumplessSetPoint = target;
                        break;
                    case HrztMode.Heading:
                        StartCoroutine(shiftHeadingTarget(target.headingClamp(360)));
                        break;
                    case HrztMode.HeadingNum:
                        AsstList.HdgBank.GetAsst(this).SetPoint = target.headingClamp(360);
                        break;
                }
            }
            hdgModeChanged(mode, active, !setTarget);
        }

        private void vertModeChanged(VertMode newMode, bool active, bool setTarget = true)
        {
            if (!active)
            {
                InputLockManager.RemoveControlLock(pitchLockID);
                pitchLockEngaged = false;
                AsstList.Altitude.GetAsst(this).Clear();
                AsstList.VertSpeed.GetAsst(this).Clear();
                AsstList.Elevator.GetAsst(this).Clear();
                StartCoroutine(fadeOutPitch());
            }
            else
            {
                if (!pitchLockEngaged)
                {
                    InputLockManager.SetControlLock(ControlTypes.PITCH, pitchLockID);
                    pitchLockEngaged = true;
                }
                bPause = false;

                ////////////////////////////////////////////////////////////////////////////////////////
                // Set the integral sums for the vertical control systems to improve transfer smoothness
                bool invert = Math.Abs(vesModule.vesselData.bank) > 90;
                if (VertActive)
                {
                    AsstList.Elevator.GetAsst(this).Preset(invert);
                    if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude || CurrentVertMode == VertMode.VSpeed)
                    {
                        AsstList.VertSpeed.GetAsst(this).Preset(invert);
                        if (CurrentVertMode == VertMode.Altitude || CurrentVertMode == VertMode.RadarAltitude)
                            AsstList.Altitude.GetAsst(this).Preset(invert);
                        else
                            AsstList.Altitude.GetAsst(this).Preset(vesModule.vesselData.vertSpeed, invert);
                    }
                    else
                    {
                        AsstList.Altitude.GetAsst(this).Preset(vesModule.vesselData.vertSpeed, invert);
                        AsstList.VertSpeed.GetAsst(this).Preset(vesModule.vesselData.AoA, invert);
                    }
                }
                else
                {
                    AsstList.Altitude.GetAsst(this).Preset(vesModule.vesselData.vertSpeed, invert);
                    AsstList.VertSpeed.GetAsst(this).Preset(vesModule.vesselData.AoA, invert);
                    AsstList.Elevator.GetAsst(this).Preset(pitchSet, invert);
                }
                
                switch (newMode)
                {
                    case VertMode.Pitch:
                        if (setTarget)
                        {
                            AsstList.Elevator.GetAsst(this).SetPoint = vesModule.vesselData.pitch;
                        }
                        targetVert = AsstList.Elevator.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case VertMode.VSpeed:
                        if (setTarget)
                        {
                            AsstList.VertSpeed.GetAsst(this).SetPoint = vesModule.vesselData.vertSpeed + vesModule.vesselData.AoA / AsstList.VertSpeed.GetAsst(this).PGain;
                            AsstList.VertSpeed.GetAsst(this).BumplessSetPoint = vesModule.vesselData.vertSpeed;
                        }
                        targetVert = AsstList.VertSpeed.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case VertMode.Altitude:
                        if (setTarget)
                        {
                            AsstList.Altitude.GetAsst(this).SetPoint = vesModule.vesselRef.altitude + vesModule.vesselData.vertSpeed / AsstList.Altitude.GetAsst(this).PGain;
                            AsstList.Altitude.GetAsst(this).BumplessSetPoint = vesModule.vesselRef.altitude;
                        }
                        targetVert = AsstList.Altitude.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                    case VertMode.RadarAltitude:
                        if (setTarget)
                        {
                            AsstList.Altitude.GetAsst(this).SetPoint = vesModule.vesselData.radarAlt + vesModule.vesselData.vertSpeed / AsstList.Altitude.GetAsst(this).PGain;
                            AsstList.Altitude.GetAsst(this).BumplessSetPoint = vesModule.vesselData.radarAlt;
                        }
                        targetVert = AsstList.Altitude.GetAsst(this).SetPoint.ToString("0.00");
                        break;
                }
            }
            VertActive = active;
            CurrentVertMode = newMode;
        }

        public double GetCurrentVert()
        {
            switch (CurrentVertMode)
            {
                case VertMode.Pitch:
                    return AsstList.Elevator.GetAsst(this).SetPoint;
                case VertMode.VSpeed:
                    return AsstList.VertSpeed.GetAsst(this).SetPoint;
                case VertMode.Altitude:
                case VertMode.RadarAltitude:
                default:
                    return AsstList.Altitude.GetAsst(this).SetPoint;
            }
        }

        public void SetVert(bool active, bool setTarget, VertMode mode, double target)
        {
            if (setTarget)
            {
                switch (mode)
                {
                    case VertMode.Altitude:
                        if (!VertActive)
                            AsstList.Altitude.GetAsst(this).SetPoint = vesModule.vesselRef.altitude + vesModule.vesselData.vertSpeed / AsstList.Altitude.GetAsst(this).PGain;
                        AsstList.Altitude.GetAsst(this).BumplessSetPoint = target;
                        break;
                    case VertMode.RadarAltitude:
                        if (!VertActive)
                            AsstList.Altitude.GetAsst(this).SetPoint = vesModule.vesselData.radarAlt + vesModule.vesselData.vertSpeed / AsstList.Altitude.GetAsst(this).PGain;
                        AsstList.Altitude.GetAsst(this).BumplessSetPoint = target;
                        break;
                    case VertMode.VSpeed:
                        AsstList.VertSpeed.GetAsst(this).SetPoint = vesModule.vesselData.vertSpeed + vesModule.vesselData.AoA / AsstList.VertSpeed.GetAsst(this).PGain;
                        AsstList.VertSpeed.GetAsst(this).BumplessSetPoint = target;
                        break;
                    case VertMode.Pitch:
                        AsstList.Elevator.GetAsst(this).SetPoint = target;
                        break;
                }
            }
            vertModeChanged(mode, active, !setTarget);
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
                bPause = false;
                switch (newMode)
                {
                    case ThrottleMode.Speed:
                        if (setTarget)
                            AsstList.Speed.GetAsst(this).SetPoint = vesModule.vesselRef.srfSpeed;
                        targetSpeed = (AsstList.Speed.GetAsst(this).SetPoint * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound)).ToString("0.00");
                        break;
                    case ThrottleMode.Acceleration:
                        if (setTarget)
                            AsstList.Acceleration.GetAsst(this).SetPoint = vesModule.vesselData.acceleration;
                        targetSpeed = (AsstList.Acceleration.GetAsst(this).SetPoint * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound)).ToString("0.00");
                        break;
                    case ThrottleMode.Direct:
                        if (setTarget)
                            currentThrottlePct = vesModule.vesselRef.ctrlState.mainThrottle;
                        targetSpeed = (currentThrottlePct * 100).ToString("0.00");
                        break;
                }
            }
            ThrtActive = active;
            CurrentThrottleMode = newMode;
        }

        public double GetCurrentThrottle()
        {
            switch(CurrentThrottleMode)
            {
                case ThrottleMode.Direct:
                    return currentThrottlePct;
                case ThrottleMode.Acceleration:
                    return AsstList.Acceleration.GetAsst(this).SetPoint;
                case ThrottleMode.Speed:
                default:
                    return AsstList.Speed.GetAsst(this).SetPoint;
            }
        }

        public void SetThrottle(bool active, bool setTarget, ThrottleMode mode, double target)
        {
            if (setTarget)
            {
                switch (mode)
                {
                    case ThrottleMode.Direct:
                        currentThrottlePct = Utils.Clamp(target / 100, 0, 1);
                        vesModule.vesselRef.ctrlState.mainThrottle = (float)currentThrottlePct;
                        if (ReferenceEquals(vesModule.vesselRef, FlightGlobals.ActiveVessel))
                            FlightInputHandler.state.mainThrottle = (float)currentThrottlePct;
                        break;
                    case ThrottleMode.Acceleration:
                        target /= Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound);
                        AsstList.Acceleration.GetAsst(this).SetPoint = vesModule.vesselData.acceleration;
                        AsstList.Acceleration.GetAsst(this).BumplessSetPoint = target;
                        break;
                    case ThrottleMode.Speed:
                        target /= Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound);
                        AsstList.Speed.GetAsst(this).SetPoint = vesModule.vesselRef.srfSpeed;
                        AsstList.Speed.GetAsst(this).BumplessSetPoint = target;
                        break;
                }
            }
            throttleModeChanged(mode, active, !setTarget);
        }

        public void ChangeSpeedRef(SpeedRef newRef)
        {
            if (ThrtActive)
            {
                switch (CurrentThrottleMode)
                {
                    case ThrottleMode.Speed:
                        double currentSpeed = AsstList.Speed.GetAsst(this).SetPoint / Utils.SpeedTransform(speedRef, vesModule);
                        AsstList.Speed.GetAsst(this).SetPoint = currentSpeed * Utils.SpeedTransform(newRef, vesModule);
                        break;
                    case ThrottleMode.Acceleration:
                        double currentAccel = AsstList.Acceleration.GetAsst(this).SetPoint / Utils.SpeedTransform(speedRef, vesModule);
                        AsstList.Acceleration.GetAsst(this).SetPoint = currentAccel * Utils.SpeedTransform(newRef, vesModule);
                        break;
                }
            }
            speedRef = newRef;
            adjustedSpeed = vesModule.vesselRef.srfSpeed * Utils.SpeedTransform(speedRef, vesModule);
            adjustedAcceleration = 0;

            throttleModeChanged(CurrentThrottleMode, ThrtActive, false);
        }

        public void ChangeSpeedUnit(SpeedUnits unit)
        {
            units = unit;
            throttleModeChanged(CurrentThrottleMode, ThrtActive, false);
        }

        public void TogglePauseCtrlState()
        {
            bPause = !bPause;

            if (bPause)
            {
                GeneralUI.postMessage("Pilot Assistant: Control Paused");
                InputLockManager.RemoveControlLock(yawLockID);
                InputLockManager.RemoveControlLock(pitchLockID);
                pitchLockEngaged = false;
                yawLockEngaged = false;
            }
            else
            {
                GeneralUI.postMessage("Pilot Assistant: Control Unpaused");
                hdgModeChanged(CurrentHrztMode, HrztActive);
                vertModeChanged(CurrentVertMode, VertActive);
                throttleModeChanged(CurrentThrottleMode, ThrtActive);
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
        #endregion

        #region Control / Fixed Update

        public void vesselController(FlightCtrlState state)
        {
            pitchSet = state.pitch; // last pitch ouput, used for presetting the elevator
            UpdateAdjustedAcceleration(); // must run to update the UI readouts

            if (bPause)
                return;

            bool useIntegral = !vesModule.vesselRef.LandedOrSplashed;
            // Heading Control
            if (HrztActive && useIntegral)
            {
                switch (CurrentHrztMode)
                {
                    case HrztMode.Heading:
                    case HrztMode.HeadingNum:
                        AsstList.BankToYaw.GetAsst(this).SetPoint = AsstList.Aileron.GetAsst(this).SetPoint = AsstList.HdgBank.GetAsst(this).ResponseD(vesModule.vesselData.progradeHeading, useIntegral);
                        AsstList.Rudder.GetAsst(this).SetPoint = AsstList.BankToYaw.GetAsst(this).ResponseD(vesModule.vesselData.yaw, useIntegral);
                        break;
                    case HrztMode.Bank:
                    default:
                        AsstList.Rudder.GetAsst(this).SetPoint = 0;
                        break;
                }

                state.roll = AsstList.Aileron.GetAsst(this).ResponseF(vesModule.vesselData.bank, useIntegral).Clamp(-1, 1);
                state.yaw = AsstList.Rudder.GetAsst(this).ResponseF(vesModule.vesselData.yaw, useIntegral).Clamp(-1, 1);
            }

            if (VertActive)
            {
                if (CurrentVertMode != VertMode.Pitch)
                {
                    switch (CurrentVertMode)
                    {
                        case VertMode.RadarAltitude:
                            AsstList.VertSpeed.GetAsst(this).SetPoint = Utils.Clamp(getClimbRateForConstAltitude() + AsstList.Altitude.GetAsst(this).ResponseD(vesModule.vesselData.radarAlt * Vector3.Dot(vesModule.vesselData.surfVelForward, vesModule.vesselRef.srf_velocity.normalized), useIntegral), -vesModule.vesselRef.srfSpeed * 0.9, vesModule.vesselRef.srfSpeed * 0.9);
                            break;
                        case VertMode.Altitude:
                            AsstList.VertSpeed.GetAsst(this).SetPoint = Utils.Clamp(AsstList.Altitude.GetAsst(this).ResponseD(vesModule.vesselRef.altitude, useIntegral), vesModule.vesselRef.srfSpeed * -0.9, vesModule.vesselRef.srfSpeed * 0.9);
                            break;
                    }
                    AsstList.Elevator.GetAsst(this).SetPoint = AsstList.VertSpeed.GetAsst(this).ResponseD(vesModule.vesselData.vertSpeed, useIntegral);
                    AsstList.Elevator.GetAsst(this).SetPoint *= (float)Utils.Clamp(Math.Cos(vesModule.vesselData.bank * Math.PI / 180) * 2.0, -1, 1); // only reduce control when bank angle exceeds ~60 degrees
                    state.pitch = AsstList.Elevator.GetAsst(this).ResponseF(vesModule.vesselData.AoA, useIntegral).Clamp(-1, 1);
                }
                else
                {
                    state.pitch = AsstList.Elevator.GetAsst(this).ResponseF(vesModule.vesselData.pitch, useIntegral).Clamp(-1, 1);
                    state.pitch *= (float)Utils.Clamp(Math.Cos(vesModule.vesselData.bank * Math.PI / 180) * 2.0, -1, 1); // only reduce control when bank angle exceeds ~60 degrees
                }
            }
            else if (pitchHold != 0)
                state.pitch = Mathf.Clamp(state.pitch - pitchHold, -1, 1);

            if (ThrtActive)
            {
                if (vesModule.vesselRef.ActionGroups[KSPActionGroup.Brakes] || (AsstList.Speed.GetAsst(this).SetPoint == 0 && vesModule.vesselRef.srfSpeed < -AsstList.Acceleration.GetAsst(this).OutMin))
                    state.mainThrottle = 0;
                else if (CurrentThrottleMode != ThrottleMode.Direct)
                {
                    if (CurrentThrottleMode == ThrottleMode.Speed)
                        AsstList.Acceleration.GetAsst(this).SetPoint = AsstList.Speed.GetAsst(this).ResponseD(adjustedSpeed, useIntegral);
                    state.mainThrottle = AsstList.Acceleration.GetAsst(this).ResponseF(adjustedAcceleration, useIntegral).Clamp(0, 1);
                }
                else
                    state.mainThrottle = (float)currentThrottlePct;
                if (vesModule.vesselRef == FlightGlobals.ActiveVessel)
                    FlightInputHandler.state.mainThrottle = state.mainThrottle; // set throttle state permanently, but only if active vessel...
            }
        }

        public void UpdateAdjustedAcceleration()
        {
            double newAdjustedSpeed = vesModule.vesselRef.srfSpeed * Utils.SpeedTransform(speedRef, vesModule);
            adjustedAcceleration = adjustedAcceleration * 0.8 + 0.2 * (newAdjustedSpeed - adjustedSpeed) / TimeWarp.fixedDeltaTime;
            adjustedSpeed = newAdjustedSpeed;
        }

        float pitchHold = 0;
        IEnumerator fadeOutPitch()
        {
            double val = AsstList.Elevator.GetAsst(this).lastOutput;
            double step = val * TimeWarp.fixedDeltaTime / 10;
            int sign = Math.Sign(val);
            yield return new WaitForFixedUpdate();
            while (!VertActive && Math.Sign(val) == sign && vesModule.vesselRef.atmDensity != 0)
            {
                yield return new WaitForFixedUpdate();
                val -= step;
                pitchHold = (float)val;
            }
            pitchHold = 0;
        }

        IEnumerator shiftHeadingTarget(double newHdg)
        {
            headingEdit = false;
            stopHdgShift = false;
            if (hdgShiftIsRunning)
            {
                double remainder = Quaternion.Angle(newTarget, currentTarget);
                // set new direction
                newTarget = Utils.getPlaneRotation(newHdg, vesModule);
                // get new remainder, reset increment only if the sign changed
                double tempRemainder = Quaternion.Angle(newTarget, currentTarget);
                if (tempRemainder < 0.5 * AsstList.HdgBank.GetAsst(this).OutMax && tempRemainder < 0.5 * remainder)
                {
                    currentTarget = Utils.getPlaneRotation((vesModule.vesselData.heading + vesModule.vesselData.bank / AsstList.HdgBank.GetAsst(this).PGain).headingClamp(360), vesModule);
                    increment = 0;
                }
                yield break;
            }
            else
            {
                currentTarget = Utils.getPlaneRotation((vesModule.vesselData.heading + vesModule.vesselData.bank / AsstList.HdgBank.GetAsst(this).PGain).headingClamp(360), vesModule);
                newTarget = Utils.getPlaneRotation(newHdg, vesModule);
                increment = 0;
                hdgShiftIsRunning = true;
            }

            while (!stopHdgShift && Math.Abs(Quaternion.Angle(currentTarget, newTarget)) > 0.01)
            {
                currentTarget = Quaternion.RotateTowards(currentTarget, newTarget, (float)increment);
                increment += AsstList.HdgBank.GetAsst(this).Easing * TimeWarp.fixedDeltaTime * 0.2;
                yield return new WaitForFixedUpdate();
            }
            if (!stopHdgShift)
                currentTarget = newTarget;
            hdgShiftIsRunning = false;
        }

        double getClimbRateForConstAltitude()
        {
            // work out angle for ~1s to approach the point
            double angle = Math.Min(Math.Atan(4 * vesModule.vesselRef.horizontalSrfSpeed / vesModule.vesselData.radarAlt), 1.55); // 1.55 is ~89 degrees
            if (double.IsNaN(angle) || angle < 0.25) // 0.25 is 14.3 degrees
                return 0; // fly without predictive if high/slow
            else
            {
                double slope = 0;
                terrainSlope(angle, out slope);
                return slope * vesModule.vesselRef.horizontalSrfSpeed;
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
            angle += vesModule.vesselData.pitch * Math.PI / 180;
            double RayDist = findTerrainDistAtAngle((float)(angle * 180 / Math.PI), 10000);
            double AltAhead = 0;
            if (RayDist == -1)
                return false;
            else
            {
                AltAhead = RayDist * Math.Cos(angle);
                if (vesModule.vesselRef.mainBody.ocean)
                    AltAhead = Math.Min(AltAhead, vesModule.vesselRef.altitude);
            }
            slope = (vesModule.vesselData.radarAlt - AltAhead) / (AltAhead * Math.Tan(angle));
            return true;
        }

        /// <summary>
        /// raycast from vessel CoM along the given angle, returns the distance at which terrain is detected (-1 if never detected). Angle is degrees to rotate forwards from vertical
        /// </summary>
        float findTerrainDistAtAngle(float angle, float maxDist)
        {
            Vector3 direction = Quaternion.AngleAxis(angle, -vesModule.vesselData.surfVelRight) * -vesModule.vesselData.planetUp;
            Vector3 origin = vesModule.vesselRef.rootPart.transform.position;
            RaycastHit hitInfo;
            if (!vesModule.vesselRef.HoldPhysics && Physics.Raycast(origin, direction, out hitInfo, maxDist, ~1)) // ~1 masks off layer 0 which is apparently the parts on the current vessel. Seems to work
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
                if (CurrentHrztMode != HrztMode.Bank)
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
                if (CurrentVertMode != VertMode.Pitch)
                    vertScrollHeight += AsstList.VertSpeed.GetAsst(this).bShow ? 168 : 29;
                if (showControlSurfaces)
                    vertScrollHeight += AsstList.Elevator.GetAsst(this).bShow ? 168 : 29;
            }
            if (bShowThrottle && dragID != 3)
            {
                thrtScrollHeight = 0;
                if (CurrentThrottleMode != ThrottleMode.Direct)
                {
                    if (CurrentThrottleMode == ThrottleMode.Speed)
                        thrtScrollHeight += AsstList.Speed.GetAsst(this).bShow ? 168 : 27;
                    thrtScrollHeight += AsstList.Acceleration.GetAsst(this).bShow ? 168 : 29;
                }
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

            if (speedSelectWindowVisible)
                speedSelectWindow = GUILayout.Window(34257, speedSelectWindow, drawSpeedSelectWindow, "", GeneralUI.UISkin.box);
        }

        private bool controllerVisible(AsstController controller)
        {
            if (!controller.bShow)
                return false;
            switch (controller.ctrlID)
            {
                case AsstList.HdgBank:
                case AsstList.BankToYaw:
                    return bShowHdg && CurrentHrztMode != HrztMode.Bank;
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
                    return bShowThrottle && CurrentThrottleMode != ThrottleMode.Direct;
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
                            SetHrzt(true, true, CurrentHrztMode, newHdg);

                            GUI.FocusControl("Target Hdg: ");
                            GUI.UnfocusWindow();
                        }
                    }

                    double displayTargetDelta = 0; // active setpoint or absolute value to change (yaw L/R input)
                    string displayTarget = "0.00"; // target setpoint or setpoint to commit as target setpoint
                    if (CurrentHrztMode == HrztMode.Heading)
                    {
                        if (!hdgShiftIsRunning)
                            displayTargetDelta = AsstList.HdgBank.GetAsst(this).SetPoint - vesModule.vesselData.heading;
                        else
                            displayTargetDelta = Utils.calculateTargetHeading(newTarget, vesModule) - vesModule.vesselData.heading;

                        displayTargetDelta = displayTargetDelta.headingClamp(180);

                        if (headingEdit)
                            displayTarget = targetHeading;
                        else
                            displayTarget = Utils.calculateTargetHeading(newTarget, vesModule).ToString("0.00");
                    }
                    else
                    {
                        displayTargetDelta = AsstList.HdgBank.GetAsst(this).SetPoint - vesModule.vesselData.heading;
                        displayTargetDelta = displayTargetDelta.headingClamp(180);

                        if (headingEdit)
                            displayTarget = targetHeading;
                        else
                            displayTarget = AsstList.HdgBank.GetAsst(this).SetPoint.ToString("0.00");
                    }

                    

                    targetHeading = GUILayout.TextField(displayTarget, GUILayout.Width(51));
                    if (targetHeading != displayTarget)
                        headingEdit = true;
                    GUILayout.Label(displayTargetDelta.ToString("0.00"), GeneralUI.UISkin.customStyles[(int)myStyles.greenTextBox], GUILayout.Width(51));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Target Bank: ", GUILayout.Width(90)))
                    {
                        double newBank;
                        if (double.TryParse(targetHeading, out newBank))
                        {
                            SetHrzt(true, true, CurrentHrztMode, newBank);
                            GUI.FocusControl("Target Bank: ");
                            GUI.UnfocusWindow();
                        }
                    }
                    string displayTarget = headingEdit ? targetHeading : (AsstList.Aileron.GetAsst(this).SetPoint).ToString("0.00");
                    targetHeading = GUILayout.TextField(displayTarget, GUILayout.Width(51));
                    if (targetHeading != displayTarget)
                        headingEdit = true;

                    if (GUILayout.Button("Level", GUILayout.Width(51)))
                    {
                        AsstList.Aileron.GetAsst(this).BumplessSetPoint = 0;
                        hdgModeChanged(CurrentHrztMode, true, false);
                    }
                    GUILayout.EndHorizontal();
                }

                if (!bMinimiseHdg)
                {
                    HdgScrollbar = GUILayout.BeginScrollView(HdgScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(hdgScrollHeight, maxHdgScrollbarHeight)));
                    if (CurrentHrztMode != HrztMode.Bank)
                    {
                        drawPIDvalues(AsstList.HdgBank, "Heading", "\u00B0", vesModule.vesselData.heading, 2, "Bank", "\u00B0");
                        drawPIDvalues(AsstList.BankToYaw, "Yaw", "\u00B0", vesModule.vesselData.yaw, 2, "Yaw", "\u00B0", true, false);
                    }
                    if (showControlSurfaces)
                    {
                        drawPIDvalues(AsstList.Aileron, "Bank", "\u00B0", vesModule.vesselData.bank, 3, "Deflection", "\u00B0");
                        drawPIDvalues(AsstList.Rudder, "Yaw", "\u00B0", vesModule.vesselData.yaw, 3, "Deflection", "\u00B0");
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
                    VertMode tempMode = (VertMode)GUILayout.SelectionGrid((int)CurrentVertMode, vertLabels, 4, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                    if (tempMode != CurrentVertMode)
                        vertModeChanged(tempMode, VertActive);
                }
                GUILayout.BeginHorizontal();
                string buttonString;
                switch (CurrentVertMode)
                {
                    case VertMode.RadarAltitude:
                        buttonString = "Target RadarAlt";
                        break;
                    case VertMode.Altitude:
                        buttonString = "Target Altitude";
                        break;
                    case VertMode.VSpeed:
                        buttonString = "Target Speed";
                        break;
                    case VertMode.Pitch:
                    default:
                        buttonString = "Target Pitch";
                        break;
                }

                if (GUILayout.Button(buttonString, GUILayout.Width(118)))
                {
                    ScreenMessages.PostScreenMessage(buttonString + " updated");

                    double newVal;
                    if (double.TryParse(targetVert, out newVal))
                        SetVert(true, true, CurrentVertMode, newVal);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetVert = GUILayout.TextField(targetVert, GUILayout.Width(78));
                GUILayout.EndHorizontal();

                if (!bMinimiseVert)
                {
                    VertScrollbar = GUILayout.BeginScrollView(VertScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(vertScrollHeight, maxVertScrollbarHeight)));
                    if (CurrentVertMode == VertMode.RadarAltitude)
                        drawPIDvalues(AsstList.Altitude, "RAltitude", "m", vesModule.vesselData.radarAlt, 2, "Speed ", "m/s", true);
                    if (CurrentVertMode == VertMode.Altitude)
                        drawPIDvalues(AsstList.Altitude, "Altitude", "m", vesModule.vesselRef.altitude, 2, "Speed ", "m/s", true);
                    if (CurrentVertMode != VertMode.Pitch)
                        drawPIDvalues(AsstList.VertSpeed, "Vertical Speed", "m/s", vesModule.vesselData.vertSpeed, 2, "AoA", "\u00B0", true);

                    if (showControlSurfaces)
                        drawPIDvalues(AsstList.Elevator, CurrentVertMode != VertMode.Pitch ? "Angle of Attack" : "Pitch", "\u00B0", CurrentVertMode == VertMode.Pitch ? vesModule.vesselData.pitch : vesModule.vesselData.AoA, 3, "Deflection", "\u00B0", true);

                    AsstList.Elevator.GetAsst(this).OutMin = Utils.Clamp(AsstList.Elevator.GetAsst(this).OutMin, -1, 1);
                    AsstList.Elevator.GetAsst(this).OutMax = Utils.Clamp(AsstList.Elevator.GetAsst(this).OutMax, -1, 1);

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
                    ThrottleMode tempMode = (ThrottleMode)GUILayout.SelectionGrid((int)CurrentThrottleMode, throttleLabels, 3, GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(200));
                    if (tempMode != CurrentThrottleMode)
                        throttleModeChanged(tempMode, ThrtActive);
                }
                GUILayout.BeginHorizontal();

                string tempSpeed = "";
                switch (CurrentThrottleMode)
                {
                    case ThrottleMode.Direct:
                        tempSpeed = "Throttle %";
                        break;
                    case ThrottleMode.Acceleration:
                        tempSpeed = "Target Accel";
                        break;
                    case ThrottleMode.Speed:
                        tempSpeed = "Target Speed";
                        break;
                }
                if (GUILayout.Button(tempSpeed, GUILayout.Width(108)))
                {
                    GeneralUI.postMessage("Target updated");

                    double newVal;
                    if (double.TryParse(targetSpeed, out newVal))
                        SetThrottle(true, true, CurrentThrottleMode, newVal);

                    GUI.FocusControl("Target Hdg: ");
                    GUI.UnfocusWindow();
                }
                targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(68));
                bool tempToggle = GUILayout.Toggle(speedSelectWindowVisible, ">", GeneralUI.UISkin.customStyles[(int)myStyles.btnToggle], GUILayout.Width(18));
                if (tempToggle != speedSelectWindowVisible)
                {
                    speedSelectWindowVisible = tempToggle;
                    speedSelectWindow.x = Input.mousePosition.x + 30;
                    speedSelectWindow.y = Screen.height - (Input.mousePosition.y + 20);
                }

                GUILayout.EndHorizontal();

                if (!bMinimiseThrt)
                {
                    ThrtScrollbar = GUILayout.BeginScrollView(ThrtScrollbar, GUIStyle.none, GeneralUI.UISkin.verticalScrollbar, GUILayout.Height(Math.Min(thrtScrollHeight, maxThrtScrollbarHeight)));
                    if (CurrentThrottleMode == ThrottleMode.Speed)
                        drawPIDvalues(AsstList.Speed, "Speed", Utils.unitString(units), adjustedSpeed * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound), 2, "Accel ", Utils.unitString(units) + " / s", true);
                    if (CurrentThrottleMode != ThrottleMode.Direct)
                        drawPIDvalues(AsstList.Acceleration, "Acceleration", Utils.unitString(units) + " / s", adjustedAcceleration * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound), 1, "Throttle ", " %", true);
                    // can't have people bugging things out now can we...
                    AsstList.Acceleration.GetAsst(this).OutMax = AsstList.Speed.GetAsst(this).OutMax.Clamp(-1, 0);
                    AsstList.Acceleration.GetAsst(this).OutMin = AsstList.Speed.GetAsst(this).OutMin.Clamp(-1, 0);

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
                {
                    switch (controllerid)
                    {
                        case AsstList.Speed:
                        case AsstList.Acceleration:
                            GUILayout.Label("Target: " + (controller.SetPoint * Utils.speedUnitTransform(units, vesModule.vesselRef.speedOfSound)).ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));
                            break;
                        default:
                            GUILayout.Label("Target: " + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));
                            break;
                    }
                }

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

            if (!ReferenceEquals(PresetManager.Instance.activeAsstPreset, null)) // preset will be null after deleting an active preset
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
                PresetManager.newAsstPreset(ref newPresetName, controllers, vesModule.vesselRef);
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
            if (!ReferenceEquals(presetToDelete, null))
            {
                PresetManager.deleteAsstPreset(presetToDelete);
                presetWindow.height = 0;
            }
        }

        private void drawSpeedSelectWindow(int id)
        {
            SpeedRef tempRef = (SpeedRef)GUILayout.SelectionGrid((int)speedRef, speedRefLabels, 3);
            if (tempRef != speedRef)
                ChangeSpeedRef(tempRef);
            SpeedUnits tempUnits = (SpeedUnits)GUILayout.SelectionGrid((int)units, speedUnitLabels, 5);
            if (tempUnits != units)
                ChangeSpeedUnit(tempUnits);
        }

        #endregion
    }
}