using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    /// <summary>
    /// Holds all the PID tuning values for the 7 (or more if required) controllers involved.
    /// </summary>
    class PAPreset
    {
        private string name;
        private List<double[]> PIDGains = new List<double[]>();

        private const int NUM_CONTROLLERS = 7;

        public PAPreset(List<PID.PID_Controller> controllers, string name) // used for adding a new preset, can clone the current values
        {
            this.name = name;
            for (int i = 0; i < NUM_CONTROLLERS; i++) // currently 7 PID controlers to save
            {
                double[] gains = new double[8];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].OutMin;
                gains[4] = controllers[i].OutMax;
                gains[5] = controllers[i].ClampLower;
                gains[6] = controllers[i].ClampUpper;
                gains[7] = controllers[i].Scalar;
                PIDGains.Add(gains);
            }
        }

        public PAPreset(ConfigNode node)
        {
            name = node.GetValue("name");
            PIDGains.Add(LoadControllerGains(node.GetNode("HdgBankController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("HdgYawController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("AileronController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("RudderController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("AltitudeController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("AoAController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("ElevatorController")));
        }

        public string GetName() { return name; }

        private double[] LoadControllerGains(ConfigNode node)
        {
            // TODO: out val ---> out gains[i]
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

        private ConfigNode GainsToConfigNode(string name, double[] gains)
        {
            ConfigNode node = new ConfigNode(name);

            node.AddValue("PGain", gains[0]);
            node.AddValue("IGain", gains[1]);
            node.AddValue("DGain", gains[2]);
            node.AddValue("MinOut", gains[3]);
            node.AddValue("MaxOut", gains[4]);
            node.AddValue("ClampLower", gains[5]);
            node.AddValue("ClampUpper", gains[6]);
            node.AddValue("Scalar", gains[7]);
                          
            return node;
        }

        public ConfigNode ToConfigNode()
        {
            ConfigNode node = new ConfigNode("PAPreset");
            node.AddValue("name", name);
            node.AddNode(GainsToConfigNode("HdgBankController", PIDGains[0]));
            node.AddNode(GainsToConfigNode("HdgYawController", PIDGains[1]));
            node.AddNode(GainsToConfigNode("AileronController", PIDGains[2]));
            node.AddNode(GainsToConfigNode("RudderController", PIDGains[3]));
            node.AddNode(GainsToConfigNode("AltitudeController", PIDGains[4]));
            node.AddNode(GainsToConfigNode("AoAController", PIDGains[5]));
            node.AddNode(GainsToConfigNode("ElevatorController", PIDGains[6]));

            return node;
        }

        public void LoadPreset(List<PID.PID_Controller> controllers)
        {
            for (int i = 0; i < NUM_CONTROLLERS; i++)
            {
                controllers[i].PGain = PIDGains[i][0];
                controllers[i].IGain = PIDGains[i][1];
                controllers[i].DGain = PIDGains[i][2];
                controllers[i].OutMin = PIDGains[i][3];
                controllers[i].OutMax = PIDGains[i][4];
                controllers[i].ClampLower = PIDGains[i][5];
                controllers[i].ClampUpper = PIDGains[i][6];
                controllers[i].Scalar = PIDGains[i][7];
            }
        }

        public void Update(List<PID.PID_Controller> controllers)
        {
            List<double[]> newPIDGains = new List<double[]>();
            for (int i = 0; i < NUM_CONTROLLERS; i++) // currently 7 PID controlers to save
            {
                double[] gains = new double[8];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].OutMin;
                gains[4] = controllers[i].OutMax;
                gains[5] = controllers[i].ClampLower;
                gains[6] = controllers[i].ClampUpper;
                gains[7] = controllers[i].Scalar;

                newPIDGains.Add(gains);
            }
            PIDGains = newPIDGains;
        }
    }
}
