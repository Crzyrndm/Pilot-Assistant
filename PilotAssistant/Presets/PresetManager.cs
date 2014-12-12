using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PilotAssistant.Presets
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class PresetManager : MonoBehaviour
    {
        internal static PresetPA defaultPATuning;
        internal static List<PresetPA> PAPresetList = new List<PresetPA>();
        internal static PresetPA activePAPreset = null;

        internal static PresetSAS defaultSASTuning;
        internal static PresetSAS defaultStockSASTuning;
        internal static List<PresetSAS> SASPresetList = new List<PresetSAS>();
        internal static PresetSAS activeSASPreset = null;
        internal static PresetSAS activeStockSASPreset = null;

        public void Start()
        {
            loadPresetsFromFile();
            DontDestroyOnLoad(this);
        }

        public void OnDestroy()
        {
            saveCFG();
        }

        internal static void loadPresetsFromFile()
        {
            PAPresetList.Clear();
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
                PAPresetList.Add(new PresetPA(gains, node.GetValue("name")));
            }

            foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("SASPreset"))
            {
                if (node == null)
                    continue;

                List<double[]> gains = new List<double[]>();
                gains.Add(controllerSASGains(node.GetNode("AileronController")));
                gains.Add(controllerSASGains(node.GetNode("RudderController")));
                gains.Add(controllerSASGains(node.GetNode("ElevatorController")));
                SASPresetList.Add(new PresetSAS(gains, node.GetValue("name"), bool.Parse(node.GetValue("stock"))));
            }
        }

        internal static void saveCFG()
        {
            ConfigNode node = new ConfigNode();
            if (PAPresetList.Count == 0 && SASPresetList.Count == 0)
                node.AddValue("dummy", "do not delete me");
            else
            {
                foreach (PresetPA p in PAPresetList)
                {
                    node.AddNode(PAPresetNode(p));
                }
                foreach (PresetSAS p in SASPresetList)
                {
                    node.AddNode(SASPresetNode(p));
                }
            }
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Pilot Assistant/Presets.cfg");
        }

        private static double[] controllerGains(ConfigNode node)
        {
            double[] gains = new double[8];
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
            double.TryParse(node.GetValue("Scalar"), out val);
            gains[7] = val;

            return gains;
        }

        private static double[] controllerSASGains(ConfigNode node)
        {
            double[] gains = new double[4];
            double val;
            double.TryParse(node.GetValue("PGain"), out val);
            gains[0] = val;
            double.TryParse(node.GetValue("IGain"), out val);
            gains[1] = val;
            double.TryParse(node.GetValue("DGain"), out val);
            gains[2] = val;
            double.TryParse(node.GetValue("Scalar"), out val);
            gains[3] = val;

            return gains;
        }

        private static ConfigNode PAPresetNode(PresetPA preset)
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

        private static ConfigNode SASPresetNode(PresetSAS preset)
        {
            ConfigNode node = new ConfigNode("SASPreset");
            node.AddValue("name", preset.name);
            node.AddValue("stock", preset.bStockSAS);
            node.AddNode(PIDnode("AileronController", 0, preset));
            node.AddNode(PIDnode("RudderController", 1, preset));
            node.AddNode(PIDnode("ElevatorController", 2, preset));

            return node;
        }

        private static ConfigNode PIDnode(string name, int index, PresetPA preset)
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

        private static ConfigNode PIDnode(string name, int index, PresetSAS preset)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue("PGain", preset.PIDGains[index][0]);
            node.AddValue("IGain", preset.PIDGains[index][1]);
            node.AddValue("DGain", preset.PIDGains[index][2]);
            node.AddValue("Scalar", preset.PIDGains[index][3]);
            return node;
        }

        internal static void loadPAPreset(PresetPA p)
        {
            List<PID.PID_Controller> c = PilotAssistant.controllers;

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
        }

        internal static void loadSASPreset(PresetSAS p)
        {
            List<PID.PID_Controller> c = SurfSAS.SASControllers;

            for (int i = 0; i < 3; i++)
            {
                c[i].PGain = p.PIDGains[i][0];
                c[i].IGain = p.PIDGains[i][1];
                c[i].DGain = p.PIDGains[i][2];
                c[i].Scalar = p.PIDGains[i][3];
            }
        }

        internal static void loadStockSASPreset(PresetSAS p)
        {
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kp = p.PIDGains[0][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.ki = p.PIDGains[0][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kd = p.PIDGains[0][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.clamp = p.PIDGains[0][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kp = p.PIDGains[2][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.ki = p.PIDGains[2][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kd = p.PIDGains[2][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.clamp = p.PIDGains[2][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kp = p.PIDGains[1][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.ki = p.PIDGains[1][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kd = p.PIDGains[1][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.clamp = p.PIDGains[1][3];
        }
    }
}
