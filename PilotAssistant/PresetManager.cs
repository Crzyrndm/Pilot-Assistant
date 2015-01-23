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


        public List<PresetPA> PAPresetList = new List<PresetPA>();
        public List<PresetSAS> SASPresetList = new List<PresetSAS>();

        public PresetPA activePAPreset = null;
        public PresetSAS activeSASPreset = null;
        public PresetSAS activeStockSASPreset = null;

        public Dictionary<string, CraftPreset> craftPresetList = new Dictionary<string, CraftPreset>();

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

        public static void loadPresetsFromFile()
        {
            PresetPA asst = null;
            PresetSAS SSAS = null, stock = null;

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("PIDPreset"))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerGains(node.GetNode("HdgBankController")));
                gains.Add(controllerGains(node.GetNode("HdgYawController")));
                gains.Add(controllerGains(node.GetNode("AileronController")));
                gains.Add(controllerGains(node.GetNode("RudderController")));
                gains.Add(controllerGains(node.GetNode("AltitudeController")));
                gains.Add(controllerGains(node.GetNode("AoAController")));
                gains.Add(controllerGains(node.GetNode("ElevatorController")));

                if (node.GetValue("name") != "default" && !instance.PAPresetList.Any(p => p.name == node.GetValue("name")))
                    instance.PAPresetList.Add(new PresetPA(gains, node.GetValue("name")));
                else
                    asst = new PresetPA(gains, node.GetValue("name"));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("SASPreset"))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode("AileronController")));
                gains.Add(controllerSASGains(node.GetNode("RudderController")));
                gains.Add(controllerSASGains(node.GetNode("ElevatorController")));

                if ((node.GetValue("name") != "SSAS" && node.GetValue("name") != "stock") && !instance.SASPresetList.Any(p=> p.name == node.GetValue("name")))
                    instance.SASPresetList.Add(new PresetSAS(gains, node.GetValue("name"), bool.Parse(node.GetValue("stock"))));
                else
                {
                    if (node.GetValue("name") == "SSAS")
                        SSAS = new PresetSAS(gains, node.GetValue("name"), false);
                    else
                        stock = new PresetSAS(gains, node.GetValue("name"), true);
                }
            }
            
            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("CraftPreset"))
            {
                if (node == null || instance.craftPresetList.ContainsKey(node.GetValue("name")))
                    continue;

                if (node.GetValue("name") == "default")
                    instance.craftPresetList.Add("default", new CraftPreset("default", asst, SSAS, stock));
                else
                {
                    CraftPreset cP = new CraftPreset(node.GetValue("name"),
                                            instance.PAPresetList.FirstOrDefault(p => p.name == node.GetValue("pilot")),
                                            instance.SASPresetList.FirstOrDefault(p => p.name == node.GetValue("ssas")),
                                            instance.SASPresetList.FirstOrDefault(p => p.name == node.GetValue("stock")));

                    instance.craftPresetList.Add(cP.Name, cP);
                }
            }
        }

        public static void saveToFile()
        {
            ConfigNode node = new ConfigNode();
            node.AddValue("dummy", "do not delete me");
            foreach (PresetPA p in instance.PAPresetList)
            {
                node.AddNode(PAPresetNode(p));
            }
            foreach (PresetSAS p in instance.SASPresetList)
            {
                node.AddNode(SASPresetNode(p));
            }
            foreach (KeyValuePair<string, CraftPreset> cP in instance.craftPresetList)
            {
                if (cP.Value == null || cP.Key == "default" || cP.Value.dead)
                    continue;
                node.AddNode(CraftNode(cP.Value));
            }
            
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Pilot Assistant/Presets.cfg");
        }

        public static void saveDefaults()
        {
            ConfigNode node = new ConfigNode();
            CraftPreset cP = instance.craftPresetList["default"];

            if (cP.PresetPA != null)
                node.AddNode(PAPresetNode(cP.PresetPA));
            if (cP.SSAS != null)
                node.AddNode(SASPresetNode(cP.SSAS));
            if (cP.Stock != null)
                node.AddNode(SASPresetNode(cP.Stock));

            node.AddNode(CraftNode(cP));
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Pilot Assistant/Defaults.cfg");
        }

        public static double[] controllerGains(ConfigNode node)
        {
            double[] gains = new double[8];
            double.TryParse(node.GetValue("PGain"), out gains[0]);
            double.TryParse(node.GetValue("IGain"), out gains[1]);
            double.TryParse(node.GetValue("DGain"), out gains[2]);
            double.TryParse(node.GetValue("MinOut"), out gains[3]);
            double.TryParse(node.GetValue("MaxOut"), out gains[4]);
            double.TryParse(node.GetValue("ClampLower"), out gains[5]);
            double.TryParse(node.GetValue("ClampUpper"), out gains[6]);
            double.TryParse(node.GetValue("Scalar"), out gains[7]);

            return gains;
        }

        public static double[] controllerSASGains(ConfigNode node)
        {
            double[] gains = new double[4];
            double.TryParse(node.GetValue("PGain"), out gains[0]);
            double.TryParse(node.GetValue("IGain"), out gains[1]);
            double.TryParse(node.GetValue("DGain"), out gains[2]);
            double.TryParse(node.GetValue("Scalar"), out gains[3]);

            return gains;
        }

        public static ConfigNode PAPresetNode(PresetPA preset)
        {
            ConfigNode node = new ConfigNode("PIDPreset");
            node.AddValue("name", preset.name);
            node.AddNode(PIDnode("HdgBankController", (int)PIDList.HdgBank, preset));
            node.AddNode(PIDnode("HdgYawController", (int)PIDList.HdgYaw, preset));
            node.AddNode(PIDnode("AileronController", (int)PIDList.Aileron, preset));
            node.AddNode(PIDnode("RudderController", (int)PIDList.Rudder, preset));
            node.AddNode(PIDnode("AltitudeController", (int)PIDList.Altitude, preset));
            node.AddNode(PIDnode("AoAController", (int)PIDList.VertSpeed, preset));
            node.AddNode(PIDnode("ElevatorController", (int)PIDList.Elevator, preset));

            return node;
        }

        public static ConfigNode SASPresetNode(PresetSAS preset)
        {
            ConfigNode node = new ConfigNode("SASPreset");
            node.AddValue("name", preset.name);
            node.AddValue("stock", preset.bStockSAS);
            node.AddNode(PIDnode("AileronController", (int)SASList.Roll, preset));
            node.AddNode(PIDnode("RudderController", (int)SASList.Yaw, preset));
            node.AddNode(PIDnode("ElevatorController", (int)SASList.Pitch, preset));

            return node;
        }

        public static ConfigNode PIDnode(string name, int index, PresetPA preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue("PGain", preset.PIDGains[index][0]);
            node.AddValue("IGain", preset.PIDGains[index][1]);
            node.AddValue("DGain", preset.PIDGains[index][2]);
            node.AddValue("MinOut", preset.PIDGains[index][3]);
            node.AddValue("MaxOut", preset.PIDGains[index][4]);
            node.AddValue("ClampLower", preset.PIDGains[index][5]);
            node.AddValue("ClampUpper", preset.PIDGains[index][6]);
            node.AddValue("Scalar", preset.PIDGains[index][7]);
            return node;
        }

        public static ConfigNode PIDnode(string name, int index, PresetSAS preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue("PGain", preset.PIDGains[index, 0]);
            node.AddValue("IGain", preset.PIDGains[index, 1]);
            node.AddValue("DGain", preset.PIDGains[index, 2]);
            node.AddValue("Scalar", preset.PIDGains[index, 3]);
            return node;
        }

        public static ConfigNode CraftNode(CraftPreset preset)
        {
            ConfigNode node = new ConfigNode("CraftPreset");
            if (!string.IsNullOrEmpty(preset.Name))
                node.AddValue("name", preset.Name);
            if (preset.PresetPA != null && !string.IsNullOrEmpty(preset.PresetPA.name))
                node.AddValue("pilot", preset.PresetPA.name);
            if (preset.SSAS != null && !string.IsNullOrEmpty(preset.SSAS.name))
                node.AddValue("ssas", preset.SSAS.name);
            if (preset.Stock != null && !string.IsNullOrEmpty(preset.Stock.name))
                node.AddValue("stock", preset.Stock.name);
            return node;
        }

        public static void newPAPreset(ref string name, PID_Controller[] controllers)
        {
            if (name == "")
                return;
            
            foreach (PresetPA p in Instance.PAPresetList)
            {
                if (name == p.name)
                {
                    Messaging.postMessage("Failed to add preset with duplicate name");
                    return;
                }
            }

            if (Instance.craftPresetList.ContainsKey(FlightData.thisVessel.vesselName))
                Instance.craftPresetList[FlightData.thisVessel.vesselName].PresetPA = new PresetPA(controllers, name);
            else
            {
                Instance.craftPresetList.Add(FlightData.thisVessel.vesselName,
                    new CraftPreset(FlightData.thisVessel.vesselName, new PresetPA(PilotAssistant.Instance.controllers, name), PresetManager.Instance.activeSASPreset, PresetManager.Instance.activeStockSASPreset));
            }

            Instance.PAPresetList.Add(new PresetPA(PilotAssistant.Instance.controllers, name));
            name = "";
            Instance.activePAPreset = PresetManager.Instance.PAPresetList[PresetManager.Instance.PAPresetList.Count - 1];
            saveToFile();
        }

        public static void loadPAPreset(PresetPA p)
        {
            PID_Controller[] c = PilotAssistant.Instance.controllers;

            for (int i = 0; i < 7; i++)
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
        }

        public static void updatePAPreset(PID_Controller[] controllers)
        {
            instance.activePAPreset.Update(controllers);
            saveToFile();
        }

        public static void deletePAPreset(PresetPA p)
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

            foreach (PresetSAS p in Instance.SASPresetList)
            {
                if (name == p.name)
                    return;
            }
            
            Instance.SASPresetList.Add(new PresetSAS(Utility.FlightData.thisVessel.Autopilot.SAS, name));
            Instance.activeSASPreset = Instance.SASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void newSASPreset(ref string name, PID_Controller[] controllers)
        {
            if (string.IsNullOrEmpty(name))
                return;

            foreach (PresetSAS p in Instance.SASPresetList)
            {
                if (name == p.name)
                    return;
            }

            Instance.SASPresetList.Add(new PresetSAS(controllers, name));
            Instance.activeSASPreset = Instance.SASPresetList.Last();
            saveToFile();
            name = "";
        }

        public static void loadSASPreset(PresetSAS p)
        {
            PID_Controller[] c = SurfSAS.Instance.SASControllers;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                c[(int)s].PGain = p.PIDGains[(int)s, 0];
                c[(int)s].IGain = p.PIDGains[(int)s, 1];
                c[(int)s].DGain = p.PIDGains[(int)s, 2];
                c[(int)s].Scalar = p.PIDGains[(int)s, 3];
            }

            Instance.activeSASPreset = p;
        }

        public static void deleteSASPreset(PresetSAS p)
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
                    if (cp.Value != null && cp.Value.SSAS == p)
                        cp.Value.SSAS = null;
                }
                else
                {
                    if (cp.Value != null && cp.Value.Stock == p)
                        cp.Value.Stock = null;
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

        public static void loadStockSASPreset(PresetSAS p)
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
        }

        public static void loadCraftAsstPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].PresetPA != null)
                loadPAPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].PresetPA);
            else
                loadPAPreset(instance.craftPresetList["default"].PresetPA);
        }

        public static void loadCraftSSASPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SSAS != null)
                loadSASPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].SSAS);
            else
                loadSASPreset(instance.craftPresetList["default"].SSAS);
        }

        public static void loadCraftStockPreset()
        {
            if (instance.craftPresetList.ContainsKey(FlightGlobals.ActiveVessel.vesselName) && instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].Stock != null)
                loadStockSASPreset(instance.craftPresetList[FlightGlobals.ActiveVessel.vesselName].Stock);
            else
                loadStockSASPreset(instance.craftPresetList["default"].Stock);
        }
    }
}
