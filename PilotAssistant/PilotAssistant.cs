using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;

namespace PilotAssistant
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class PilotAssistant : MonoBehaviour
    {        
        private Vessel thisVessel = null;
        private PID.PID_Controller HeadingBankController = new PID.PID_Controller(3, 0.1, 0, -30, 30, -0.1, 0.1);
        private PID.PID_Controller HeadingYawController = new PID.PID_Controller(0, 0, 0, -2, 2, -2, 2);
        private PID.PID_Controller AileronController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1);
        private PID.PID_Controller RudderController = new PID.PID_Controller(0.05, 0.01, 0.1, -1, 1, -0.1, 0.1);

        private PID.PID_Controller AltitudeToClimbRate = new PID.PID_Controller(0.1, 0, 0, -30, 30, -1, 1); // P control for converting altitude hold to climb rate
        private PID.PID_Controller AoAController = new PID.PID_Controller(3, 0.4, 1.5, -10, 10, -10, 10); // Input craft altitude, output target craft AoA
        private PID.PID_Controller ElevatorController = new PID.PID_Controller(0.01, 0.01, 0.01, -1, 1, -0.1, 0.1); // Convert pitch input to control surface deflection

        // Presets window
        private bool showPresets = false;
        private Rect presetWindow = new Rect(0, 0, 200, 350);

        private Rect window = new Rect(10, 50, 10, 10);
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

        GUIStyle labelStyle;
        GUIStyle textStyle;
        GUIStyle btnStyle1;
        GUIStyle btnStyle2;

        // Presets
        private Preset defaultTuning;
        private List<Preset> PresetList = new List<Preset>();

        public void Start()
        {
            thisVessel = FlightGlobals.ActiveVessel;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
            GameEvents.onVesselChange.Add(vesselSwitch);

            AileronController.InMax = 180;
            AileronController.InMin = -180;
            AltitudeToClimbRate.InMin = 0;

            // Set up a default preset that can be easily returned to
            List<PID.PID_Controller> controllers = new List<PID.PID_Controller>();
            controllers.Add(HeadingBankController);
            controllers.Add(HeadingYawController);
            controllers.Add(AileronController);
            controllers.Add(RudderController);
            controllers.Add(AltitudeToClimbRate);
            controllers.Add(AoAController);
            controllers.Add(ElevatorController);

            defaultTuning = new Preset(controllers, "default");
            
            // Load all other presets available
            loadPresetsFromFile();
            print("loaded");
        }

        private void loadPresetsFromFile()
        {
            PresetList.Clear();
            print(0);
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PIDPreset"))
            {
                List<double[]> gains = new List<double[]>();
                gains.Add(controllerGains(node.GetNode("HdgBankController")));
                gains.Add(controllerGains(node.GetNode("HdgYawController")));
                gains.Add(controllerGains(node.GetNode("AileronController")));
                gains.Add(controllerGains(node.GetNode("RudderController")));
                gains.Add(controllerGains(node.GetNode("AltitudeController")));
                gains.Add(controllerGains(node.GetNode("AoAController")));
                gains.Add(controllerGains(node.GetNode("ElevatorController")));
                PresetList.Add(new Preset(gains, node.GetValue("name")));
            }
        }

        private double[] controllerGains(ConfigNode node)
        {
            double[] gains = new double[7];
            double val;
            double.TryParse(node.GetValue("PGain"), out val);
            gains[0] = val;
            double.TryParse(node.GetValue("IGain"), out val);
            gains[1] = val;
            double.TryParse(node.GetValue("DGain"), out val);
            gains[2] = val;
            double.TryParse(node.GetValue("MinOut"), out val);
            gains[3] = val;
            double.TryParse(node.GetValue("MaxOut"), out val);
            gains[4] = val;
            double.TryParse(node.GetValue("ClampLower"), out val);
            gains[5] = val;
            double.TryParse(node.GetValue("ClampUpper"), out val);
            gains[6] = val;

            return gains;
        }

        private void saveCFG()
        {
            ConfigNode node = new ConfigNode();
            foreach (Preset p in PresetList)
            {
                node.AddNode(PresetNode(p));
            }
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/PilotAssistant/Presets.cfg");
        }

        private ConfigNode PresetNode(Preset preset)
        {
            ConfigNode node = new ConfigNode("PIDPreset");
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode("HdgBankController", 0, preset));
            node.AddNode(PIDnode("HdgYawController", 1, preset));
            node.AddNode(PIDnode("AileronController", 2, preset));
            node.AddNode(PIDnode("RudderController", 3, preset));
            node.AddNode(PIDnode("AltitudeController", 4, preset));
            node.AddNode(PIDnode("AoAController", 5, preset));
            node.AddNode(PIDnode("ElevatorController", 6, preset));

            return node;
        }

        private ConfigNode PIDnode(string name, int index, Preset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue("PGain", preset.PIDGains[index][0]);
            node.AddValue("IGain", preset.PIDGains[index][1]);
            node.AddValue("DGain", preset.PIDGains[index][2]);
            node.AddValue("MinOut", preset.PIDGains[index][3]);
            node.AddValue("MaxOut", preset.PIDGains[index][4]);
            node.AddValue("ClampLower", preset.PIDGains[index][5]);
            node.AddValue("ClampUpper", preset.PIDGains[index][6]);
            return node;
        }

        private void loadPreset(Preset p)
        {
            HeadingBankController = new PID.PID_Controller(p.PIDGains[0][0], p.PIDGains[0][1], p.PIDGains[0][2], p.PIDGains[0][3], p.PIDGains[0][4], p.PIDGains[0][5], p.PIDGains[0][6]);
            HeadingYawController = new PID.PID_Controller(p.PIDGains[1][0], p.PIDGains[1][1], p.PIDGains[1][2], p.PIDGains[1][3], p.PIDGains[1][4], p.PIDGains[1][5], p.PIDGains[1][6]);
            AileronController = new PID.PID_Controller(p.PIDGains[2][0], p.PIDGains[2][1], p.PIDGains[2][2], p.PIDGains[2][3], p.PIDGains[2][4], p.PIDGains[2][5], p.PIDGains[2][6]);
            RudderController = new PID.PID_Controller(p.PIDGains[3][0], p.PIDGains[3][1], p.PIDGains[3][2], p.PIDGains[3][3], p.PIDGains[3][4], p.PIDGains[3][5], p.PIDGains[3][6]);
            AltitudeToClimbRate = new PID.PID_Controller(p.PIDGains[4][0], p.PIDGains[4][1], p.PIDGains[4][2], p.PIDGains[4][3], p.PIDGains[4][4], p.PIDGains[4][5], p.PIDGains[4][6]);
            AoAController = new PID.PID_Controller(p.PIDGains[5][0], p.PIDGains[5][1], p.PIDGains[5][2], p.PIDGains[5][3], p.PIDGains[5][4], p.PIDGains[5][5], p.PIDGains[5][6]);
            ElevatorController = new PID.PID_Controller(p.PIDGains[6][0], p.PIDGains[6][1], p.PIDGains[6][2], p.PIDGains[6][3], p.PIDGains[6][4], p.PIDGains[6][5], p.PIDGains[6][6]);
        }

        private void vesselSwitch(Vessel v)
        {
            ball = null;
            thisVessel.OnFlyByWire -= new FlightInputCallback(vesselController);
            thisVessel = v;
            thisVessel.OnFlyByWire += new FlightInputCallback(vesselController);
        }

        public void OnDestroy()
        {
            appDestroy();
            GameEvents.onVesselChange.Remove(vesselSwitch);
            saveCFG();
        }

        public void Update()
        {
            // hdg control toggled
            if (rollActive != rollWasActive)
            {
                rollWasActive = rollActive;
                if (rollActive)
                {
                    HeadingBankController.SetPoint = heading;
                    targetHeading = heading.ToString("N2");
                }
                else
                {
                    HeadingBankController.Clear();
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
                window.width = 420;
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
            
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.alignment = TextAnchor.MiddleLeft;
            labelStyle.margin = new RectOffset(4, 4, 5, 3);

            textStyle = new GUIStyle(GUI.skin.textField);
            textStyle.alignment = TextAnchor.MiddleLeft;
            textStyle.margin = new RectOffset(4, 0, 5, 3);

            btnStyle1 = new GUIStyle(GUI.skin.button);
            btnStyle1.margin = new RectOffset(0, 4, 2, 0);

            btnStyle2 = new GUIStyle(GUI.skin.button);
            btnStyle2.margin = new RectOffset(0, 4, 0, 2);

            window = GUI.Window(34244, window, displayWindow, "");

            presetWindow.x = window.x + window.width;
            presetWindow.y = window.y;
            if (showPresets)
                presetWindow = GUI.Window(34245, presetWindow, displayPresetWindow, "");
        }

        private void displayWindow(int id)
        {
            if (GUILayout.Button("Show Presets"))
            {
                showPresets = !showPresets;
            }

            GUILayout.Box("", GUILayout.Height(10), GUILayout.Width(window.width - 50));

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
                    HeadingBankController.SetPoint = newHdg;
                    rollActive = rollWasActive = true; // skip toggle check to avoid being overwritten
                }
            }

            scrollbarHdg = GUILayout.BeginScrollView(scrollbarHdg, showPIDGains ? GUILayout.Height(160) : GUILayout.Height(0));

            drawPIDvalues(HeadingBankController, "Hdg Roll", "\u00B0", heading, 2, "Bank", "\u00B0");
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
                pitchActive = pitchWasActive = true; // skip the toggle check so value isn't overwritten

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

                controller.PGain = labPlusNumBox(string.Format("{0} Kp: ", inputName), controller.PGain.ToString("G3"), 80);
                controller.IGain = labPlusNumBox(string.Format("{0} Ki: ", inputName), controller.IGain.ToString("G3"), 80);
                controller.DGain = labPlusNumBox(string.Format("{0} Kd: ", inputName), controller.DGain.ToString("G3"), 80);

                if (showPIDLimits)
                {
                    GUILayout.EndVertical();
                    GUILayout.BeginVertical();

                    if (!invertOutput)
                    {
                        controller.OutMin = labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), controller.OutMin.ToString("G3"));
                        controller.OutMax = labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), controller.OutMax.ToString("G3"));
                        controller.ClampLower = labPlusNumBox("I Clamp Lower", controller.ClampLower.ToString("G3"));
                        controller.ClampUpper = labPlusNumBox("I Clamp Upper", controller.ClampUpper.ToString("G3"));
                    }
                    else
                    { // used when response * -1 is used to get the correct output
                        controller.OutMax = -1 * labPlusNumBox(string.Format("Min {0}{1}: ", outputName, outputUnits), (-controller.OutMax).ToString("G3"));
                        controller.OutMin = -1 * labPlusNumBox(string.Format("Max {0}{1}: ", outputName, outputUnits), (-controller.OutMin).ToString("G3"));
                        controller.ClampUpper = -1 * labPlusNumBox("I Clamp Lower", (-controller.ClampUpper).ToString("G3"));
                        controller.ClampLower = -1 * labPlusNumBox("I Clamp Upper", (-controller.ClampLower).ToString("G3"));
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
        }

        private double labPlusNumBox(string labelText, string boxText, float labelWidth = 100, float boxWidth = 60)
        {
            double val;
            GUILayout.BeginHorizontal();

            GUILayout.Label(labelText, labelStyle, GUILayout.Width(labelWidth));
            string text = GUILayout.TextField(boxText, textStyle, GUILayout.Width(boxWidth));
            //
            try
            {
                val = double.Parse(text);
            }
            catch
            {
                val = double.Parse(boxText);
            }
            //
            GUILayout.BeginVertical();
            if (GUILayout.Button("+", btnStyle1, GUILayout.Width(20),GUILayout.Height(13)))
            {
                if (val != 0)
                    val *= 1.1;
                else
                    val = 0.01;
            }
            if (GUILayout.Button("-", btnStyle2, GUILayout.Width(20), GUILayout.Height(13)))
            {
                val /= 1.1;
            }
            GUILayout.EndVertical();
            //
            GUILayout.EndHorizontal();
            return val;
        }

        private string newPresetName = "";
        private void displayPresetWindow(int id)
        {
            GUILayout.BeginHorizontal();
            newPresetName = GUILayout.TextField(newPresetName);
            if (GUILayout.Button("+", GUILayout.Width(15)))
            {
                if (newPresetName != "")
                {
                    List<PID.PID_Controller> controllers = new List<PID.PID_Controller>();
                    controllers.Add(HeadingBankController);
                    controllers.Add(HeadingYawController);
                    controllers.Add(AileronController);
                    controllers.Add(RudderController);
                    controllers.Add(AltitudeToClimbRate);
                    controllers.Add(AoAController);
                    controllers.Add(ElevatorController);

                    PresetList.Add(new Preset(controllers, newPresetName));
                    newPresetName = "";
                }
            }
            GUILayout.EndHorizontal();

            if(GUILayout.Button("Default Tuning"))
            {
                print("Loading default tunig parameters");
                loadPreset(defaultTuning);
            }
            foreach(Preset p in PresetList)
            {
                if(GUILayout.Button(p.name))
                {
                    print("Loading alternate preset");
                    loadPreset(p);
                }
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
                if (HeadingBankController.SetPoint - heading >= -180 && HeadingBankController.SetPoint - heading <= 180)
                {
                    AileronController.SetPoint = HeadingBankController.Response(heading);
                    RudderController.SetPoint = HeadingYawController.Response(heading);
                }
                else if (HeadingBankController.SetPoint - heading < -180)
                {
                    AileronController.SetPoint = HeadingBankController.Response(heading - 360);
                    RudderController.SetPoint = HeadingYawController.Response(heading - 360);
                }
                else if (HeadingBankController.SetPoint - heading > 180)
                {
                    AileronController.SetPoint = HeadingBankController.Response(heading + 360);
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
