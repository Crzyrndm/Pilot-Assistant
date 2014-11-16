using System;
using UnityEngine;

namespace PilotAssistant
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistant : MonoBehaviour
    {        
        private Vessel thisVessel = null;
        private PID.PID_Controller HeadingController = new PID.PID_Controller(3, 0.1, 0, -30, 30, -0.1, 0.1);
        private PID.PID_Controller HeadingYawController = new PID.PID_Controller(0, 0, 0, -2, 2, -2, 2);
        private PID.PID_Controller AileronController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1);
        private PID.PID_Controller RudderController = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.1, 0.1);

        private PID.PID_Controller AltitudeToClimbRate = new PID.PID_Controller(0.1, 0, 0, -30, 30, -1, 1); // P control for converting altitude hold to climb rate
        private PID.PID_Controller AoAController = new PID.PID_Controller(3, 0.4, 1.5, -10, 10, -10, 10); // Input craft altitude, output target craft AoA
        private PID.PID_Controller ElevatorController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1); // Convert pitch input to control surface deflection

        private Rect window = new Rect(10, 50, 500, 380);
        // RollController
        private bool rollActive = false;
        private bool rollWasActive = false;
        // PitchController
        private bool pitchActive = false;
        private bool pitchWasActive = false;
        // Altitude / vertical speed
        private bool bAltitudeHold = false;
        private bool bWasAltitudeHold = false;

        private double pitch = 0, roll = 0, yaw = 0, AoA = 0, heading = 0; // currenct craft attitude variables

        private string targetVert = "0";
        private string targetHeading = "0";

        private bool showPIDGains = false;
        private bool showPIDLimits = false;
        private bool showControlSurfaces = false;

        private Vector2 scrollbarHdg = Vector2.zero;
        private Vector2 scrollbarVert = Vector2.zero;

        public void Start()
        {
            thisVessel = FlightGlobals.ActiveVessel;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            AileronController.InMax = 180;
            AileronController.InMin = -180;

            AltitudeToClimbRate.InMin = 0;
        }

        public void vesselSwitch(Vessel v)
        {
            thisVessel.OnFlyByWire -= new FlightInputCallback(vesselController);
            thisVessel = v;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void OnDestroy()
        {
            appDestroy();
            GameEvents.onVesselChange.Remove(vesselSwitch);
        }

        public void Update()
        {
            // hdg control toggled
            if (rollActive != rollWasActive)
            {
                rollWasActive = rollActive;
                if (rollActive)
                {
                    HeadingController.SetPoint = heading;
                    targetHeading = heading.ToString("N2");
                }
                else
                {
                    HeadingController.Clear();
                    AileronController.Clear();
                    RudderController.Clear();
                    HeadingYawController.Clear();
                }
            }

            // vertical control toggled
            if (pitchActive != pitchWasActive)
            {
                pitchWasActive = pitchActive;
                if (pitchActive)
                {
                    if (bAltitudeHold)
                    {
                        AltitudeToClimbRate.SetPoint = thisVessel.altitude;
                        targetVert = AltitudeToClimbRate.SetPoint.ToString("N1");
                    }
                    else
                    {
                        AoAController.SetPoint = thisVessel.verticalSpeed;
                        targetVert = AoAController.SetPoint.ToString("N3");
                    }
                }
                else
                {
                    AltitudeToClimbRate.Clear();
                    AoAController.Clear();
                    ElevatorController.Clear();
                }
            }

            // Altitude/speed control toggled
            if (bAltitudeHold != bWasAltitudeHold)
            {
                bWasAltitudeHold = bAltitudeHold;
                if (bAltitudeHold)
                {
                    AltitudeToClimbRate.SetPoint = thisVessel.altitude;
                    targetVert = AltitudeToClimbRate.SetPoint.ToString("N1");
                }
                else
                {
                    AoAController.SetPoint = thisVessel.verticalSpeed;
                    targetVert = AoAController.SetPoint.ToString("N3");
                }
            }

            // Window resizing
            if (showPIDGains)
                window.height = 700;
            else
                window.height = 390;

            if (showPIDLimits && showPIDGains)
                window.width = 390;
            else
                window.width = 245;
        }

        public void FixedUpdate()
        {
        }

        public void OnGUI()
        {
            if (!bDisplay)
                return;

            window = GUI.Window(34244, window, displayWindow, "");
        }

        private void displayWindow(int id)
        {
            showPIDGains = GUILayout.Toggle(showPIDGains, "Show PID Gains", GUILayout.Width(200));
            if (showPIDGains)
            {
                showPIDLimits = GUILayout.Toggle(showPIDLimits, "Show PID Limits", GUILayout.Width(200));
                showControlSurfaces = GUILayout.Toggle(showControlSurfaces, "Show Control Surfaces", GUILayout.Width(200));
            }
            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

            GUILayout.BeginVertical();

            #region Hdg GUI
            GUILayout.Label("Heading Control", GUILayout.Width(100));

            if(GUILayout.Button(rollActive ? "Deactivate" : "Activate", GUILayout.Width(200)))
            {
                rollActive = !rollActive;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Heading: ", GUILayout.Width(98));
            targetHeading = GUILayout.TextField(targetHeading, GUILayout.Width(98));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Update Target Heading", GUILayout.Width(200)))
            {
                double newHdg;
                double.TryParse(targetHeading, out newHdg);
                if (newHdg >= 0 && newHdg <= 360)
                {
                    HeadingController.SetPoint = newHdg;
                    rollActive = rollWasActive = true;
                }
            }

            scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, showPIDGains ? GUILayout.Height(160) : GUILayout.Height(0));

            drawPIDvalues(HeadingController, "Hdg Roll", "\u00B0", heading, 2, "Bank", "\u00B0");
            if (showControlSurfaces)
                drawPIDvalues(AileronController, "Bank", "\u00B0", roll, 3, "Deflection", "\u00B0");
            drawPIDvalues(HeadingYawController, "Hdg Yaw", "\u00B0", heading, 2, "Yaw", "\u00B0", false, false);
            if (showControlSurfaces)
                drawPIDvalues(RudderController, "Yaw", "\u00B0", yaw, 3, "Deflection", "\u00B0");

            GUILayout.EndScrollView();
            #endregion

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

            #region Pitch GUI

            GUILayout.Label("Vertical Control");
            
            if(GUILayout.Button(pitchActive ? "Deactivate" : "Activate", GUILayout.Width(200)))
            {
                pitchActive = !pitchActive;
            }
    
            bAltitudeHold = GUILayout.Toggle(bAltitudeHold, bAltitudeHold ? "Mode: Altitude" : "Mode: Vertical Speed", GUILayout.Width(200));

            GUILayout.BeginHorizontal();
            GUILayout.Label(bAltitudeHold ? "Target Altitude: " : "Target Speed: ", GUILayout.Width(98));
            targetVert = GUILayout.TextField(targetVert, GUILayout.Width(98));
            GUILayout.EndHorizontal();

            if (GUILayout.Button(bAltitudeHold ? "Update Target Altitude" : "Update Target Speed", GUILayout.Width(200)))
            {
                pitchActive = pitchWasActive = true;

                double newVal;
                double.TryParse(targetVert, out newVal);
                if (bAltitudeHold)
                    AltitudeToClimbRate.SetPoint = newVal;
                else
                    AoAController.SetPoint = newVal;
            }

            scrollbarVert = GUILayout.BeginScrollView(scrollbarVert);
            if (bAltitudeHold)
            {
                drawPIDvalues(AltitudeToClimbRate, "Alt", "m", thisVessel.altitude, 1, "Speed ", "m/s", true);
            }
            drawPIDvalues(AoAController, "Speed ", "m/s", thisVessel.verticalSpeed, 3, "AoA", "\u00B0", true);
            if (showControlSurfaces)
            {
                drawPIDvalues(ElevatorController, "AoA", "\u00B0", AoA, 3, "Deflection", "\u00B0");
            }
            #endregion

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        private void drawPIDvalues(PID.PID_Controller controller, string inputName, string inputUnits, double inputValue, int displayPrecision, string outputName, string outputUnits, bool invertOutput = false, bool showCurrent = true)
        {
            if (showPIDGains)
                GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(window.width - 50));

            if (showCurrent)
                GUILayout.Label(string.Format("Current {0}: ", inputName) + inputValue.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));
            
            if (showPIDGains)
            {
                if (showCurrent)
                    GUILayout.Label(string.Format("Target {0}: ", inputName) + controller.SetPoint.ToString("N" + displayPrecision.ToString()) + inputUnits, GUILayout.Width(200));

                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                
                controller.PGain = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString(), 80);
                controller.IGain = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString(), 80);
                controller.DGain = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString(), 80);

                if (showPIDLimits)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (!invertOutput)
                    {
                        controller.OutMin = labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), controller.OutMin.ToString());
                        controller.OutMax = labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), controller.OutMax.ToString());
                        controller.ClampLower = labPlusNumBox("I Clamp Lower", controller.ClampLower.ToString());
                        controller.ClampUpper = labPlusNumBox("I Clamp Upper", controller.ClampUpper.ToString());
                    }
                    else
                    { // used when response * -1 is used to get the correct output
                        controller.OutMax = -1 * labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), (-controller.OutMax).ToString());
                        controller.OutMin = -1 * labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), (-controller.OutMin).ToString());
                        controller.ClampUpper = -1 * labPlusNumBox("I Clamp Lower", (-controller.ClampUpper).ToString());
                        controller.ClampLower = -1 * labPlusNumBox("I Clamp Upper", (-controller.ClampLower).ToString());
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private double labPlusNumBox(string labelText, string boxText, float labelWidth = 120, float boxWidth = 60)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(labelText, GUILayout.Width(labelWidth));
            string text = GUILayout.TextField(boxText, GUILayout.Width(boxWidth));
            GUILayout.EndHorizontal();
            try
            {
                return double.Parse(text);
            }
            catch
            {
                return double.Parse(boxText);
            }
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (thisVessel == null)
                return;

            updateAttitude();
            
            // Heading Control
            if (rollActive)
            {
                // Fix heading so it behaves properly traversing 0/360 degrees
                if (HeadingController.SetPoint - heading >= -180 && HeadingController.SetPoint - heading <= 180)
                {
                    AileronController.SetPoint = HeadingController.Response(heading);
                    RudderController.SetPoint = HeadingYawController.Response(heading);
                }
                else if (HeadingController.SetPoint - heading < -180)
                {
                    AileronController.SetPoint = HeadingController.Response(heading - 360);
                    RudderController.SetPoint = HeadingYawController.Response(heading - 360);
                }
                else if (HeadingController.SetPoint - heading > 180)
                {
                    AileronController.SetPoint = HeadingController.Response(heading + 360);
                    RudderController.SetPoint = HeadingYawController.Response(heading + 360);
                }

                state.roll = (float)Clamp(AileronController.Response(roll) + state.roll, -1, 1);
                state.yaw = (float)Clamp(RudderController.Response(yaw) + state.yaw, -1, 1);
            }

            // Pitch Controller
            // Work on vertical speed, altitude hold can use a proportional error as input
            if (pitchActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    AoAController.SetPoint = -AltitudeToClimbRate.Response(thisVessel.altitude);

                ElevatorController.SetPoint = -AoAController.Response(thisVessel.verticalSpeed);
                state.pitch = (float)Clamp(-ElevatorController.Response(AoA) + state.pitch, -1, 1);
            }
        }

        private NavBall ball;
        private void updateAttitude()
        {
            // blatant copying of FAR get attitude logic because its just so straightfoward...
            if (ball == null)
                ball = FlightUIController.fetch.GetComponentInChildren<NavBall>();

            // pitch/roll/heading
            Quaternion vesselRot = Quaternion.Inverse(ball.relativeGymbal);
            pitch = (vesselRot.eulerAngles.x > 180) ? (360 - vesselRot.eulerAngles.x) : -vesselRot.eulerAngles.x; // pitch up is +ve
            roll = (vesselRot.eulerAngles.z > 180) ? (vesselRot.eulerAngles.z - 360) : vesselRot.eulerAngles.z;
            heading = vesselRot.eulerAngles.y;

            // pitch AoA
            Vector3 tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + thisVessel.ReferenceTransform.forward * Vector3.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            AoA = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.forward);
            AoA = 180 / Math.PI * Math.Asin(AoA);
            if (double.IsNaN(AoA))
                AoA = 0;

            // yaw AoA
            tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + thisVessel.ReferenceTransform.right * Vector3.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
            yaw = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.right);
            yaw = 180 / Math.PI * Math.Asin(yaw);
            if (double.IsNaN(yaw))
                yaw = 0;
        }

        #region Applauncher Functions and Variables

        private ApplicationLauncherButton btnLauncher;
        private bool bDisplay = false;

        private void Awake()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(this.OnAppLauncherReady);
        }

        private void appDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(this.OnAppLauncherReady);
            if (btnLauncher != null)
                ApplicationLauncher.Instance.RemoveModApplication(this.btnLauncher);
        }

        private void OnAppLauncherReady()
        {
            btnLauncher = ApplicationLauncher.Instance.AddModApplication(OnToggleTrue, OnToggleFalse,
                                                                        null, null, null, null,
                                                                        ApplicationLauncher.AppScenes.ALWAYS,
                                                                        GameDatabase.Instance.GetTexture("PilotAssistant/Icons/AppLauncherIcon", false));
        }

        private void OnGameSceneChange(GameScenes scene)
        {
            bDisplay = false;
            ApplicationLauncher.Instance.RemoveModApplication(btnLauncher);
        }

        private void OnToggleTrue()
        {
            bDisplay = true;
        }

        private void OnToggleFalse()
        {
            bDisplay = false;
        }
        #endregion

        #region Utility Functions

        /// <summary>
        /// Clamp double input between maximum and minimum value
        /// </summary>
        /// <param name="val">variable to be clamped</param>
        /// <param name="min">minimum output value of the variable</param>
        /// <param name="max">maximum output value of the variable</param>
        /// <returns>val clamped between max and min</returns>
        internal static double Clamp(double val, double min, double max)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            else
                return val;
        }

        /// <summary>
        /// Linear interpolation between two points
        /// </summary>
        /// <param name="pct">fraction of travel from the minimum to maximum. Can be less than 0 or greater than 1</param>
        /// <param name="lower">reference point treated as the base (pct = 0)</param>
        /// <param name="upper">reference point treated as the target (pct = 1)</param>
        /// <param name="clamp">clamp pct input between 0 and 1?</param>
        /// <returns></returns>
        internal static double Lerp(double pct, double lower, double upper, bool clamp = true)
        {
            if (clamp)
            {
                pct = Clamp(pct, 0, 1);
            }
            return (1 - pct) * lower + pct * upper;
        }

        #endregion
    }
}
