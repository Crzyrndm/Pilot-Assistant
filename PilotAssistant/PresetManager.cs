using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PilotAssistant
{
    using PID;
    using Utility;
    using Presets;

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class PresetManager : MonoBehaviour
    {
        private static PresetManager instance;
        public static PresetManager Instance
        {
            get
            {
                return instance;
            }
        }


        public List<AsstPreset> PAPresetList = new List<AsstPreset>();
        public List<SASPreset> SASPresetList = new List<SASPreset>();

        public AsstPreset activePAPreset = null;
        public SASPreset activeSASPreset = null;
        public SASPreset activeStockSASPreset = null;

        public Dictionary<string, CraftPreset> craftPresetList = new Dictionary<string, CraftPreset>();

        const string presetsPath = "GameData/Pilot Assistant/Presets.cfg";
        const string defaultsPath = "GameData/Pilot Assistant/Defaults.cfg";

        const string craftDefault = "default";
        const string asstDefault = "default";
        const string ssasDefault = "SSAS";
        const string stockDefault = "stock";

        const string craftPreset = "CraftPreset";
        const string asstPreset = "PIDPreset";
        const string sasPreset = "SASPreset";

        const string craftAsst = "pilot";
        const string craftSSAS = "ssas";
        const string craftStock = "stock";
        const string craftSasMode = "SASmode";

        const string hdgCtrlr = "HdgBankController";
        const string yawCtrlr = "HdgYawController";
        const string aileronCtrlr = "AileronController";
        const string rudderCtrlr = "RudderController";
        const string altCtrlr = "AltitudeController";
        const string vertCtrlr = "AoAController";
        const string elevCtrlr = "ElevatorController";
        const string throttleCtrlr = "ThrottleController";

        const string pGain = "PGain";
        const string iGain = "IGain";
        const string dGain = "DGain";
        const string min = "MinOut";
        const string max = "MaxOut";
        const string iLower = "ClampLower";
        const string iUpper = "ClampUpper";
        const string scalar = "Scalar";
        const string slide = "Slide";

        public void Start()
        {
            instance = this;

            loadPresetsFromFile();
            DontDestroyOnLoad(this);
        }

        public void OnDestroy()
        {
            saveToFile();
        }

        public void OnGUI()
        {
            if (GeneralUI.UISkin == null)
                GeneralUI.UISkin = UnityEngine.GUI.skin;
        }

        public static void loadPresetsFromFile()
        {
            AsstPreset asst = null;
            SASPreset SSAS = null, stock = null;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(asstPreset))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerGains(node.GetNode(hdgCtrlr), PIDList.HdgBank));
                gains.Add(controllerGains(node.GetNode(yawCtrlr), PIDList.HdgYaw));
                gains.Add(controllerGains(node.GetNode(aileronCtrlr), PIDList.Aileron));
                gains.Add(controllerGains(node.GetNode(rudderCtrlr), PIDList.Rudder));
                gains.Add(controllerGains(node.GetNode(altCtrlr), PIDList.Altitude));
                gains.Add(controllerGains(node.GetNode(vertCtrlr), PIDList.VertSpeed));
                gains.Add(controllerGains(node.GetNode(elevCtrlr), PIDList.Elevator));
                gains.Add(controllerGains(node.GetNode(throttleCtrlr), PIDList.Throttle));

                if (node.GetValue("name") != craftDefault && !instance.PAPresetList.Any(p => p.name == node.GetValue("name")))
                    instance.PAPresetList.Add(new AsstPreset(gains, node.GetValue("name")));
                else if (node.GetValue("name") == asstDefault)
                    asst = new AsstPreset(gains, node.GetValue("name"));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(sasPreset))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode(elevCtrlr), SASList.Pitch));
                gains.Add(controllerSASGains(node.GetNode(aileronCtrlr), SASList.Roll));
                gains.Add(controllerSASGains(node.GetNode(rudderCtrlr), SASList.Yaw));

                if ((node.GetValue("name") != ssasDefault && node.GetValue("name") != stockDefault) && !instance.SASPresetList.Any(p=> p.name == node.GetValue("name")))
                    instance.SASPresetList.Add(new SASPreset(gains, node.GetValue("name"), bool.Parse(node.GetValue("stock"))));
                else
                {
                    if (node.GetValue("name") == ssasDefault)
                        SSAS = new SASPreset(gains, node.GetValue("name"), false);
                    else if (node.GetValue("name") == stockDefault)
                        stock = new SASPreset(gains, node.GetValue("name"), true);
                }
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes(craftPreset))
            {
                if (node == null || instance.craftPresetList.ContainsKey(node.GetValue("name")))
                    continue;
                
                bool bStock;
                bool.TryParse(node.GetValue(craftSasMode), out bStock);

                if (node.GetValue("name") == craftDefault)
                    instance.craftPresetList.Add(craftDefault, new CraftPreset(craftDefault, asst, SSAS, stock, bStock));
                else
                {
                    CraftPreset cP = new CraftPreset(node.GetValue("name"),
                                            instance.PAPresetList.FirstOrDefault(p => p.name == node.GetValue(craftAsst)),
                                            instance.SASPresetList.FirstOrDefault(p => p.name == node.GetValue(craftSSAS)),
                                            instance.SASPresetList.FirstOrDefault(p => p.name == node.GetValue(craftStock)),
                                            bStock);

                    instance.craftPresetList.Add(cP.Name, cP);
                }
            }
        }

        public static void saveToFile()
        {
            ConfigNode node = new ConfigNode();
            node.AddValue("dummy", "do not delete me");
            foreach (AsstPreset p in instance.PAPresetList)
            {
                node.AddNode(PAPresetNode(p));
            }
            foreach (SASPreset p in instance.SASPresetList)
            {
                node.AddNode(SASPresetNode(p));
            }
            foreach (KeyValuePair<string, CraftPreset> cP in instance.craftPresetList)
            {
                if (cP.Value == null || cP.Key == craftDefault || cP.Value.Dead)
                    continue;
                node.AddNode(CraftNode(cP.Value));
            }
            
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + presetsPath);
        }

        public static void saveDefaults()
        {
            ConfigNode node = new ConfigNode();
            CraftPreset cP = instance.craftPresetList[craftDefault];

            if (cP.AsstPreset != null)
                node.AddNode(PAPresetNode(cP.AsstPreset));
            if (cP.SSASPreset != null)
                node.AddNode(SASPresetNode(cP.SSASPreset));
            if (cP.StockPreset != null)
                node.AddNode(SASPresetNode(cP.StockPreset));

            node.AddNode(CraftNode(cP));
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + defaultsPath);
        }

        public static void updateDefaults()
        {
            instance.craftPresetList[craftDefault].AsstPreset.PIDGains = instance.activePAPreset.PIDGains;
            instance.craftPresetList[craftDefault].SSASPreset.PIDGains = instance.activeSASPreset.PIDGains;
            instance.craftPresetList[craftDefault].StockPreset.PIDGains = instance.activeStockSASPreset.PIDGains;
            instance.craftPresetList[craftDefault].SASMode = SurfSAS.Instance.bStockSAS;

            saveDefaults();
        }

        public static double[] controllerGains(ConfigNode node, PIDList type)
        {
            double[] gains = new double[8];

            if (node == null)
                return defaultControllerGains(type);

            double.TryParse(node.GetValue(pGain), out gains[0]);
            double.TryParse(node.GetValue(iGain), out gains[1]);
            double.TryParse(node.GetValue(dGain), out gains[2]);
            double.TryParse(node.GetValue(min), out gains[3]);
            double.TryParse(node.GetValue(max), out gains[4]);
            double.TryParse(node.GetValue(iLower), out gains[5]);
            double.TryParse(node.GetValue(iUpper), out gains[6]);
            double.TryParse(node.GetValue(scalar), out gains[7]);

            return gains;
        }

        public static double[] defaultControllerGains(PIDList type)
        {
            switch(type)
            {
                case PIDList.HdgBank:
                    return PilotAssistant.defaultHdgBankGains;
                case PIDList.HdgYaw:
                    return PilotAssistant.defaultHdgYawGains;
                case PIDList.Aileron:
                    return PilotAssistant.defaultAileronGains;
                case PIDList.Rudder:
                    return PilotAssistant.defaultRudderGains;
                case PIDList.Altitude:
                    return PilotAssistant.defaultAltitudeGains;
                case PIDList.VertSpeed:
                    return PilotAssistant.defaultVSpeedGains;
                case PIDList.Elevator:
                    return PilotAssistant.defaultElevatorGains;
                case PIDList.Throttle:
                    return PilotAssistant.defaultThrottleGains;
                default:
                    return PilotAssistant.defaultAileronGains;
            }
        }

        public static double[] controllerSASGains(ConfigNode node, SASList type)
        {
            double[] gains = new double[5];

            if (node == null)
                return defaultControllerGains(type);

            double.TryParse(node.GetValue(pGain), out gains[0]);
            double.TryParse(node.GetValue(iGain), out gains[1]);
            double.TryParse(node.GetValue(dGain), out gains[2]);
            double.TryParse(node.GetValue(scalar), out gains[3]);
            double.TryParse(node.GetValue(slide), out gains[4]);

            return gains;
        }

        public static double[] defaultControllerGains(SASList type)
        {
            switch (type)
            {
                case SASList.Pitch:
                    return SurfSAS.defaultPresetPitchGains;
                case SASList.Roll:
                    return SurfSAS.defaultPresetRollGains;
                case SASList.Yaw:
                    return SurfSAS.defaultPresetYawGains;
                default:
                    return SurfSAS.defaultPresetPitchGains;
            }
        }

        public static ConfigNode PAPresetNode(AsstPreset preset)
        {
            ConfigNode node = new ConfigNode(asstPreset);
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode(hdgCtrlr, (int)PIDList.HdgBank, preset));
            node.AddNode(PIDnode(yawCtrlr, (int)PIDList.HdgYaw, preset));
            node.AddNode(PIDnode(aileronCtrlr, (int)PIDList.Aileron, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)PIDList.Rudder, preset));
            node.AddNode(PIDnode(altCtrlr, (int)PIDList.Altitude, preset));
            node.AddNode(PIDnode(vertCtrlr, (int)PIDList.VertSpeed, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)PIDList.Elevator, preset));
            node.AddNode(PIDnode(throttleCtrlr, (int)PIDList.Throttle, preset));

            return node;
        }

        public static ConfigNode SASPresetNode(SASPreset preset)
        {
            ConfigNode node = new ConfigNode(sasPreset);
            node.AddValue("name", preset.name);
            node.AddValue("stock", preset.bStockSAS);
            node.AddNode(PIDnode(aileronCtrlr, (int)SASList.Roll, preset));
            node.AddNode(PIDnode(rudderCtrlr, (int)SASList.Yaw, preset));
            node.AddNode(PIDnode(elevCtrlr, (int)SASList.Pitch, preset));

            return node;
        }

        public static ConfigNode PIDnode(string name, int index, AsstPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index][0]);
            node.AddValue(iGain, preset.PIDGains[index][1]);
            node.AddValue(dGain, preset.PIDGains[index][2]);
            node.AddValue(min, preset.PIDGains[index][3]);
            node.AddValue(max, preset.PIDGains[index][4]);
            node.AddValue(iLower, preset.PIDGains[index][5]);
            node.AddValue(iUpper, preset.PIDGains[index][6]);
            node.AddValue(scalar, preset.PIDGains[index][7]);
            return node;
        }

        public static ConfigNode PIDnode(string name, int index, SASPreset preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue(pGain, preset.PIDGains[index, 0]);
            node.AddValue(iGain, preset.PIDGains[index, 1]);
            node.AddValue(dGain, preset.PIDGains[index, 2]);
            node.AddValue(scalar, preset.PIDGains[index, 3]);
            node.AddValue(slide, preset.PIDGains[index, 4]);
            return node;
        }

        public static ConfigNode CraftNode(CraftPreset preset)
        {
            ConfigNode node = new ConfigNode(craftPreset);
            if (!string.IsNullOrEmpty(preset.Name))
                node.AddValue("name", preset.Name);
            if (preset.AsstPreset != null && !string.IsNullOrEmpty(preset.AsstPreset.name))
                node.AddValue(craftAsst, preset.AsstPreset.name);
            if (preset.SSASPreset != null && !string.IsNullOrEmpty(preset.SSASPreset.name))
                node.AddValue(craftSSAS, preset.SSASPreset.name);
            if (preset.StockPreset != null && !string.IsNullOrEmpty(preset.StockPreset.name))
                node.AddValue(craftStock, preset.StockPreset.name);
            node.AddValue(craftSasMode, preset.SASMode.ToString());
            
            return node;
        }

        public static void newPAPreset(ref string name, PID_Controller[] controllers)
        {
            if (name == "")
                return;
            
            foreach (AsstPreset p in Instance.PAPresetList)
            {
                if (name == p.name)
                {
                    Messaging.postMessage("Failed to add preset with duplicate name");
                    return;
                }
            }

            if (Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName))
                Instance.craftPresetList[FlightData.thisVessel.vesselName].AsstPreset = new AsstPreset(controllers, name);
            else
            {
                Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                                                new CraftPreset(FlightData.thisVessel.vesselName,
                                                    new AsstPreset(PilotAssistant.controllers, name),
                                                    Instance.activeSASPreset == Instance.craftPresetList[craftDefault].SSASPreset ? null : Instance.activeSASPreset,
                                                    Instance.activeStockSASPreset == Instance.craftPresetList[craftDefault].StockPreset ? null : Instance.activeStockSASPreset,
                                                    SurfSAS.Instance.bStockSAS));
            }

            Instance.PAPresetList.Add(new AsstPreset(controllers, name));
            name = "";
            Instance.activePAPreset = PresetManager.Instance.PAPresetList.Last();
            saveToFile();
        }

        public static void loadPAPreset(AsstPreset p)
        {
            PID_Controller[] c = PilotAssistant.controllers;

            for (int i = 0; i < 8; i++)
            {
                c[i].PGain = p.PIDGains[i][0];
                c[i].IGain = p.PIDGains[i][1];
                c[i].DGain = p.PIDGains[i][2];
                c[i].OutMin = p.PIDGains[i][3];
                c[i].OutMax = p.PIDGains[i][4];
                c[i].ClampLower = p.PIDGains[i][5];
                c[i].ClampUpper = p.PIDGains[i][6];
                c[i].Scalar = p.PIDGains[i][7];
            }

            Instance.activePAPreset = p;
            Messaging.postMessage("Loaded preset " + p.name);

            if (Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activePAPreset != Instance.craftPresetList[craftDefault].AsstPreset)
                Instance.craftPresetList[FlightData.thisVessel.vesselName].AsstPreset = Instance.activePAPreset;
            else if (!Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activePAPreset != Instance.craftPresetList[craftDefault].AsstPreset)
            {
                Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                                                new CraftPreset(FlightData.thisVessel.vesselName,
                                                    Instance.activePAPreset == Instance.craftPresetList[craftDefault].AsstPreset ? null : Instance.activePAPreset,
                                                    Instance.activeSASPreset == Instance.craftPresetList[craftDefault].SSASPreset ? null : Instance.activeSASPreset,
                                                    Instance.activeStockSASPreset == Instance.craftPresetList[craftDefault].StockPreset ? null : Instance.activeStockSASPreset,
                                                    SurfSAS.Instance.bStockSAS));
            }
            saveToFile();
        }

        public static void updatePAPreset(PID_Controller[] controllers)
        {
            instance.activePAPreset.Update(controllers);
            saveToFile();
        }

        public static void deletePAPreset(AsstPreset p)
        {
            Messaging.postMessage("Deleted preset " + p.name);
            if (Instance.activePAPreset == p)
                Instance.activePAPreset = null;
            Instance.PAPresetList.Remove(p);

            p = null;

            saveToFile();
        }

        public static void newSASPreset(ref string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            
            string nameTest = name;
            if (Instance.SASPresetList.Any(p => p.name == nameTest))
                return;
            
            Instance.SASPresetList.Add(new SASPreset(Utility.FlightData.thisVessel.Autopilot.SAS, name));
            Instance.activeStockSASPreset = Instance.SASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void newSASPreset(ref string name, PID_Controller[] controllers)
        {
            if (string.IsNullOrEmpty(name))
                return;

            foreach (SASPreset p in Instance.SASPresetList)
            {
                if (name == p.name)
                    return;
            }

            Instance.SASPresetList.Add(new SASPreset(controllers, name));
            Instance.activeSASPreset = Instance.SASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void loadSASPreset(SASPreset p)
        {
            PID_Controller[] c = SurfSAS.SASControllers;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                c[(int)s].PGain = p.PIDGains[(int)s, 0];
                c[(int)s].IGain = p.PIDGains[(int)s, 1];
                c[(int)s].DGain = p.PIDGains[(int)s, 2];
                c[(int)s].Scalar = p.PIDGains[(int)s, 3];
                SurfSAS.Instance.fadeReset[(int)s] = Math.Max((float)p.PIDGains[(int)s, 4],1);
            }

            Instance.activeSASPreset = p;

            if (Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activeSASPreset != Instance.craftPresetList[craftDefault].SSASPreset)
                Instance.craftPresetList[FlightData.thisVessel.vesselName].SSASPreset = Instance.activeSASPreset;
            else if (!Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activeSASPreset != Instance.craftPresetList[craftDefault].SSASPreset)
            {
                Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                                                new CraftPreset(FlightData.thisVessel.vesselName,
                                                    Instance.activePAPreset == Instance.craftPresetList[craftDefault].AsstPreset ? null : Instance.activePAPreset,
                                                    Instance.activeSASPreset == Instance.craftPresetList[craftDefault].SSASPreset ? null : Instance.activeSASPreset,
                                                    Instance.activeStockSASPreset == Instance.craftPresetList[craftDefault].StockPreset ? null : Instance.activeStockSASPreset,
                                                    SurfSAS.Instance.bStockSAS));
            }
            saveToFile();
        }

        public static void deleteSASPreset(SASPreset p)
        {
            if (Instance.activeSASPreset == p && !p.bStockSAS)
                Instance.activeSASPreset = null;
            else if (Instance.activeStockSASPreset == p && p.bStockSAS)
                Instance.activeStockSASPreset = null;
            
            Instance.SASPresetList.Remove(p);

            foreach (KeyValuePair<string, CraftPreset> cp in instance.craftPresetList)
            {
                if (!p.bStockSAS)
                {
                    if (cp.Value != null && cp.Value.SSASPreset == p)
                        cp.Value.SSASPreset = null;
                }
                else
                {
                    if (cp.Value != null && cp.Value.StockPreset == p)
                        cp.Value.StockPreset = null;
                }
            }

            saveToFile();
        }

        public static void updateSASPreset(bool stock, PID_Controller[] controllers = null)
        {
            if (stock)
                Instance.activeStockSASPreset.Update(Utility.FlightData.thisVessel.Autopilot.SAS);
            else
                Instance.activeSASPreset.Update(controllers);
            saveToFile();
        }

        public static void loadStockSASPreset(SASPreset p)
        {
            FlightData.thisVessel.Autopilot.SAS.pidLockedPitch.kp = p.PIDGains[(int)SASList.Pitch, 0];
            FlightData.thisVessel.Autopilot.SAS.pidLockedPitch.ki = p.PIDGains[(int)SASList.Pitch, 1];
            FlightData.thisVessel.Autopilot.SAS.pidLockedPitch.kd = p.PIDGains[(int)SASList.Pitch, 2];
            FlightData.thisVessel.Autopilot.SAS.pidLockedPitch.clamp = p.PIDGains[(int)SASList.Pitch, 3];

            FlightData.thisVessel.Autopilot.SAS.pidLockedRoll.kp = p.PIDGains[(int)SASList.Roll, 0];
            FlightData.thisVessel.Autopilot.SAS.pidLockedRoll.ki = p.PIDGains[(int)SASList.Roll, 1];
            FlightData.thisVessel.Autopilot.SAS.pidLockedRoll.kd = p.PIDGains[(int)SASList.Roll, 2];
            FlightData.thisVessel.Autopilot.SAS.pidLockedRoll.clamp = p.PIDGains[(int)SASList.Roll, 3];

            FlightData.thisVessel.Autopilot.SAS.pidLockedYaw.kp = p.PIDGains[(int)SASList.Yaw, 0];
            FlightData.thisVessel.Autopilot.SAS.pidLockedYaw.ki = p.PIDGains[(int)SASList.Yaw, 1];
            FlightData.thisVessel.Autopilot.SAS.pidLockedYaw.kd = p.PIDGains[(int)SASList.Yaw, 2];
            FlightData.thisVessel.Autopilot.SAS.pidLockedYaw.clamp = p.PIDGains[(int)SASList.Yaw, 3];

            Instance.activeStockSASPreset = p;

            if (Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activeStockSASPreset != Instance.craftPresetList[craftDefault].StockPreset)
                Instance.craftPresetList[FlightData.thisVessel.vesselName].StockPreset = Instance.activeStockSASPreset;
            else if (!Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName) && Instance.activeStockSASPreset != Instance.craftPresetList[craftDefault].StockPreset)
            {
                Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                                                new CraftPreset(FlightData.thisVessel.vesselName,
                                                    Instance.activePAPreset == Instance.craftPresetList[craftDefault].AsstPreset ? null : Instance.activePAPreset,
                                                    Instance.activeSASPreset == Instance.craftPresetList[craftDefault].SSASPreset ? null : Instance.activeSASPreset,
                                                    Instance.activeStockSASPreset == Instance.craftPresetList[craftDefault].StockPreset ? null : Instance.activeStockSASPreset,
                                                    SurfSAS.Instance.bStockSAS));
            }
            saveToFile();
        }

        public static void loadCraftAsstPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].AsstPreset != null)
                loadPAPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].AsstPreset);
            else
                loadPAPreset(instance.craftPresetList[craftDefault].AsstPreset);
        }

        public static void loadCraftSSASPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SSASPreset != null)
                loadSASPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SSASPreset);
            else
                loadSASPreset(instance.craftPresetList[craftDefault].SSASPreset);

            // sas mode
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName))
                SurfSAS.Instance.bStockSAS = instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SASMode;
            else
                SurfSAS.Instance.bStockSAS = instance.craftPresetList[craftDefault].SASMode;
        }

        public static void loadCraftStockPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].StockPreset != null)
                loadStockSASPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].StockPreset);
            else
                loadStockSASPreset(instance.craftPresetList[craftDefault].StockPreset);

            // sas mode
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName))
                SurfSAS.Instance.bStockSAS = instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SASMode;
            else
                SurfSAS.Instance.bStockSAS = instance.craftPresetList[craftDefault].SASMode;
        }
    }
}
