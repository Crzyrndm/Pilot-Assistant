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
        internal static Preset defaultTuning;
        internal static List<Preset> PresetList = new List<Preset>();
        internal static Preset activePreset;

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
            PresetList.Clear();
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
                PresetList.Add(new Preset(gains, node.GetValue("name")));
            }
        }

        internal static void saveCFG()
        {
            ConfigNode node = new ConfigNode();
            if (PresetList.Count == 0)
                node.AddValue("dummy", "do not delete me");
            else
            {
                foreach (Preset p in PresetList)
                {
                    node.AddNode(PresetNode(p));
                }
            }
            node.Save(KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/Pilot Assistant/Presets.cfg");
        }

        private static double[] controllerGains(ConfigNode node)
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

        private static ConfigNode PresetNode(Preset preset)
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

        private static ConfigNode PIDnode(string name, int index, Preset preset)
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

        internal static void loadPreset(Preset p)
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
            }
        }
    }
}
