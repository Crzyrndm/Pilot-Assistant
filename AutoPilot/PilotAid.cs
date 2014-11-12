using System;
using UnityEngine;

namespace PilotAid
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAid : MonoBehaviour
    {        
        Vessel thisVessel = null;
        PID.PID_Controller wingsLevel = new PID.PID_Controller(0.01, 0.01, 0.01);

        Rect window = new Rect(Screen.width - 400, 40, 200, 400);
        bool button = false;

        public void Start()
        {
            thisVessel = FlightGlobals.ActiveVessel;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void Update()
        {
            /*
            print("x: " + thisVessel.ReferenceTransform.eulerAngles.x.ToString()); // roll
            print("y: " + thisVessel.ReferenceTransform.eulerAngles.y.ToString()); // pitch
            print("z: " + thisVessel.ReferenceTransform.eulerAngles.z.ToString()); // heading / yaw
            */
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

            GUILayout.Label("Wing Leveller");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Kp: ", GUILayout.Width(50));
            string text = GUILayout.TextField(wingsLevel.PGain.ToString());
            wingsLevel.PGain = double.Parse(text);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Ki: ", GUILayout.Width(50));
            text = GUILayout.TextField(wingsLevel.IGain.ToString());
            wingsLevel.IGain = double.Parse(text);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Kd: ", GUILayout.Width(50));
            text = GUILayout.TextField(wingsLevel.DGain.ToString());
            wingsLevel.DGain = double.Parse(text);
            GUILayout.EndHorizontal();

            button = GUILayout.Toggle(button, "Active");

            GUILayout.EndVertical();
        }

        private NavBall ball;

        private void vesselController(FlightCtrlState state)
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            if (thisVessel == null)
                return;
            if (!button)
                return;

            // blatant copying of FAR get attitude logic because its just so straightfoward...
            if (ball == null)
                ball = FlightUIController.fetch.GetComponentInChildren<NavBall>();

            Quaternion vesselRot = Quaternion.Inverse(ball.relativeGymbal);
            double pitch = (vesselRot.eulerAngles.x > 180) ? (vesselRot.eulerAngles.x - 360) : vesselRot.eulerAngles.x;
            double roll = (vesselRot.eulerAngles.z > 180) ? (vesselRot.eulerAngles.z - 360) : vesselRot.eulerAngles.z;
            double heading = vesselRot.eulerAngles.y;

            Vector3 tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + thisVessel.ReferenceTransform.forward * Vector3.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            double AoA = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.forward);
            AoA = 180 / Math.PI * Math.Asin(AoA);
            if (double.IsNaN(AoA))
                AoA = 0;

            tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + thisVessel.ReferenceTransform.right * Vector3.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
            double yaw = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.right);
            yaw = 180 / Math.PI * Math.Asin(yaw);
            if (double.IsNaN(yaw))
                yaw = 0;


            // Functionality here
            print("roll: " + roll.ToString());
            state.roll = (float)PID.PID_Controller.Clamp(wingsLevel.Response(roll) + state.roll, -1, 1);
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
