using System;
using UnityEngine;

namespace PilotAid
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAid : MonoBehaviour
    {        
        private Vessel thisVessel = null;
        private PID.PID_Controller HeadingController = new PID.PID_Controller(0.01, 0, 0, -30, 30, 0, 0);
        private PID.PID_Controller RollController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1);

        private PID.PID_Controller AltitudeToClimbRate = new PID.PID_Controller(0.1, 0, 0, -20, 20, -1, 1); // P control for converting altitude hold to climb rate
        private PID.PID_Controller PitchController = new PID.PID_Controller(1, 0.4, 0.6, -10, 10, -2, 2); // Input craft altitude, output target craft pitch
        private PID.PID_Controller ElevatorController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1); // Convert pitch input to control surface deflection

        private PID.PID_Controller YawController = new PID.PID_Controller(0.01, 0.01, 0.01, 0, 0, 0, 0);

        private Rect window = new Rect(200, 40, 1500, 500);
        // RollController
        private bool rollActive = false;
        // PitchController
        private bool pitchActive = false;
        private bool pitchWasActive = false;
        private bool bAltitudeHold = true;

        private double pitch = 0, roll = 0, yaw = 0, AoA = 0, heading = 0; // currenct craft attitude variables

        private string targetAltitude = "0";
        private string targetSpeed = "0";


        public void Start()
        {
            thisVessel = FlightGlobals.ActiveVessel;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);

            RollController.InMax = 180;
            RollController.InMin = -180;

            AltitudeToClimbRate.InMin = 0;
        }

        public void Update()
        {
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
            GUILayout.BeginVertical();
            

            // Roll Control
            GUILayout.Label("Wing Leveller");
            GUILayout.Label("Target Angle: " + RollController.SetPoint.ToString("N3") + "\u00B0");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Kp: ", GUILayout.Width(80));
            string text = GUILayout.TextField(RollController.PGain.ToString());
            RollController.PGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Ki: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.IGain.ToString());
            RollController.IGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Kd: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.DGain.ToString());
            RollController.DGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Min Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.OutMin.ToString());
            RollController.OutMin = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Max Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.OutMax.ToString());
            RollController.OutMax = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Clamp Lower: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.ClampLower.ToString());
            RollController.ClampLower = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Clamp Upper: ", GUILayout.Width(80));
            text = GUILayout.TextField(RollController.ClampUpper.ToString());
            RollController.ClampUpper = double.Parse(text);
            GUILayout.EndHorizontal();

            rollActive = GUILayout.Toggle(rollActive, "Wing Leveller is Active?");


            //// Pitch Controls
            GUILayout.Label("", GUILayout.Height(30)); // vertical spacer
            GUILayout.Label("Altitude Hold");
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Altitude: ", GUILayout.Width(100));
            //
            targetAltitude = GUILayout.TextField(targetAltitude, GUILayout.Width(100));
            if (GUILayout.Button("Update Target Altitude", GUILayout.Width(150)))
            {
                double newAlt;
                double.TryParse(targetAltitude, out newAlt);
                if (newAlt > 0)
                {
                    AltitudeToClimbRate.SetPoint = newAlt;
                    bAltitudeHold = true;
                }
            }
            //
            GUILayout.Space(50);
            GUILayout.Label("Current Altitude: " + thisVessel.altitude.ToString("N1") + "m", GUILayout.Width(250));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Speed: ", GUILayout.Width(100));
            //
            targetSpeed = GUILayout.TextField(targetSpeed, GUILayout.Width(100));
            if (GUILayout.Button("Update Target Speed", GUILayout.Width(150)))
            {
                double newSpeed;
                double.TryParse(targetSpeed, out newSpeed);
                print(newSpeed);
                if (newSpeed != 0)
                {
                    PitchController.SetPoint = newSpeed;
                    bAltitudeHold = false;
                }
            }
            //
            GUILayout.Space(50);
            GUILayout.Label("Current Speed: " + thisVessel.verticalSpeed.ToString("N3") + "m/s", GUILayout.Width(250));
            GUILayout.EndHorizontal();

            bAltitudeHold = GUILayout.Toggle(bAltitudeHold, "Altitude Hold?", GUILayout.Width(200));

            if (bAltitudeHold)
            {
                GUILayout.BeginHorizontal();

                GUILayout.Label("Altitude Kp: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.PGain.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.PGain = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Altitude Ki: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.IGain.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.IGain = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Altitude Kd: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.DGain.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.DGain = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Altitude Min Out: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.OutMin.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.OutMin = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Altitude Max Out: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.OutMax.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.OutMax = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Clamp Min: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.ClampLower.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.ClampLower = double.Parse(text);

                GUILayout.Space(20);

                GUILayout.Label("Clamp Max: ", GUILayout.Width(80));
                text = GUILayout.TextField(AltitudeToClimbRate.ClampUpper.ToString(), GUILayout.Width(40));
                AltitudeToClimbRate.ClampUpper = double.Parse(text);

                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();

            GUILayout.Label("Pitch Kp: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.PGain.ToString());
            PitchController.PGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Ki: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.IGain.ToString());
            PitchController.IGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Kd: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.DGain.ToString());
            PitchController.DGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Min Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.OutMin.ToString());
            PitchController.OutMin = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Max Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.OutMax.ToString());
            PitchController.OutMax = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Clamp Min: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.ClampLower.ToString());
            PitchController.ClampLower = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Pitch Clamp Max: ", GUILayout.Width(80));
            text = GUILayout.TextField(PitchController.ClampUpper.ToString());
            PitchController.ClampUpper = double.Parse(text);

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.Label("Elevator Kp: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.PGain.ToString());
            ElevatorController.PGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Ki: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.IGain.ToString());
            ElevatorController.IGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Kd: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.DGain.ToString());
            ElevatorController.DGain = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Min Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.OutMin.ToString());
            ElevatorController.OutMin = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Max Out: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.OutMax.ToString());
            ElevatorController.OutMax = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Clamp Min: ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.ClampLower.ToString());
            ElevatorController.ClampLower = double.Parse(text);

            GUILayout.Space(20);

            GUILayout.Label("Elevator Clamp Max ", GUILayout.Width(80));
            text = GUILayout.TextField(ElevatorController.ClampUpper.ToString());
            ElevatorController.ClampUpper = double.Parse(text);

            GUILayout.EndHorizontal();

            pitchActive = GUILayout.Toggle(pitchActive, "Active");
            if (pitchActive && !pitchWasActive)
            {
                if (bAltitudeHold)
                    targetAltitude = thisVessel.altitude.ToString();
                AltitudeToClimbRate.SetPoint = thisVessel.altitude;
                pitchWasActive = true;
            }
            if (!pitchActive && pitchWasActive)
            {
                AltitudeToClimbRate.Clear();
                PitchController.Clear();
                ElevatorController.Clear();
                pitchWasActive = false;
            }

            GUILayout.EndVertical();
        }

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (thisVessel == null)
                return;

            updateAttitude();
            
            // Wing leveller
            if (rollActive)
                state.roll = (float)PID.PID_Controller.Clamp(RollController.Response(roll) + state.roll, -1, 1);

            // Pitch Controller
            // Work on vertical speed, altitude hold can use a proportional error as input
            // meh - look into switching between vert speed and altitude control. Alt hold with vert speed is pathetic
            if (pitchActive)
            {
                // Set requested vertical speed
                if (bAltitudeHold)
                    PitchController.SetPoint = -AltitudeToClimbRate.Response(thisVessel.altitude);

                ElevatorController.SetPoint = -PitchController.Response(thisVessel.verticalSpeed);
                state.pitch = (float)-ElevatorController.Response(pitch);
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

            // AoA
            Vector3 tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + thisVessel.ReferenceTransform.forward * Vector3.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            AoA = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.forward);
            AoA = 180 / Math.PI * Math.Asin(AoA);
            if (double.IsNaN(AoA))
                AoA = 0;

            // yaw? horizontal AoA?
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

        private void OnDestroy()
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
                                                                        GameDatabase.Instance.GetTexture("FlightAids/Icons/AppLauncherIcon", false));
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
    }
}
