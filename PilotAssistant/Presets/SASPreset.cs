using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    class SASPreset
    {
        private string name;
        private List<double[]> PIDGains = new List<double[]>();
        private bool useStockSAS = true;

        private const int NUM_CONTROLLERS = 3;

        public SASPreset(List<PID.PID_Controller> controllers, string name) // used for adding a new preset, can clone the current values
        {
            this.name = name;
            useStockSAS = false;
            for (int i = 0; i < NUM_CONTROLLERS; i++) // 3 PID controlers to save
            {
                double[] gains = new double[4];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].Scalar;

                PIDGains.Add(gains);
            }
        }

        public SASPreset(VesselSAS sas, string name) // used for adding a new stock preset
        {
            this.name = name;
            useStockSAS = true;
            double[] pitchGains = {sas.pidLockedPitch.kp, sas.pidLockedPitch.ki, sas.pidLockedPitch.kd, sas.pidLockedPitch.clamp};
            PIDGains.Add(pitchGains);
            double[] rollGains = { sas.pidLockedRoll.kp, sas.pidLockedRoll.ki, sas.pidLockedRoll.kd, sas.pidLockedRoll.clamp };
            PIDGains.Add(rollGains);
            double[] yawGains = { sas.pidLockedYaw.kp, sas.pidLockedYaw.ki, sas.pidLockedYaw.kd, sas.pidLockedYaw.clamp };
            PIDGains.Add(yawGains);
        }

        public SASPreset(ConfigNode node) // used for loading presets from file
        {
            name = node.GetValue("name");
            useStockSAS = bool.Parse(node.GetValue("stock"));
            PIDGains.Add(LoadControllerGains(node.GetNode("AileronController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("RudderController")));
            PIDGains.Add(LoadControllerGains(node.GetNode("ElevatorController")));
        }

        public string GetName() { return name; }
        public bool IsStockSAS() { return useStockSAS; }

        private double[] LoadControllerGains(ConfigNode node)
        {
            // TODO: out val ---> out gains[i]
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

        private ConfigNode GainsToConfigNode(string name, double[] gains)
        {
            ConfigNode node = new ConfigNode(name);
            node.AddValue("PGain", gains[0]);
            node.AddValue("IGain", gains[1]);
            node.AddValue("DGain", gains[2]);
            node.AddValue("Scalar", gains[3]);
            return node;
        }

        public ConfigNode ToConfigNode()
        {
            ConfigNode node = new ConfigNode("SASPreset");
            node.AddValue("name", name);
            node.AddValue("stock", useStockSAS);
            node.AddNode(GainsToConfigNode("AileronController", PIDGains[0]));
            node.AddNode(GainsToConfigNode("RudderController", PIDGains[1]));
            node.AddNode(GainsToConfigNode("ElevatorController", PIDGains[2]));

            return node;
        }

        public void LoadPreset(List<PID.PID_Controller> controllers)
        {
            for (int i = 0; i < NUM_CONTROLLERS; i++)
            {
                controllers[i].PGain = PIDGains[i][0];
                controllers[i].IGain = PIDGains[i][1];
                controllers[i].DGain = PIDGains[i][2];
                controllers[i].Scalar = PIDGains[i][3];
            }
        }

        public void LoadStockPreset()
        {
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kp = PIDGains[0][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.ki = PIDGains[0][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kd = PIDGains[0][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.clamp = PIDGains[0][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kp = PIDGains[2][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.ki = PIDGains[2][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kd = PIDGains[2][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.clamp = PIDGains[2][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kp = PIDGains[1][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.ki = PIDGains[1][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kd = PIDGains[1][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.clamp = PIDGains[1][3];
        }

        public void Update(List<PID.PID_Controller> controllers)
        {
            List<double[]> newPIDGains = new List<double[]>();
            for (int i = 0; i < NUM_CONTROLLERS; i++) // 3 PID controlers to save
            {
                double[] gains = new double[4];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].Scalar;

                newPIDGains.Add(gains);
            }
            PIDGains = newPIDGains;
        }

        public void UpdateStock(VesselSAS sas)
        {
            List<double[]> newPIDGains = new List<double[]>();
            double[] pitchGains = { sas.pidLockedPitch.kp, sas.pidLockedPitch.ki, sas.pidLockedPitch.kd, sas.pidLockedPitch.clamp };
            newPIDGains.Add(pitchGains);
            double[] rollGains = { sas.pidLockedRoll.kp, sas.pidLockedRoll.ki, sas.pidLockedRoll.kd, sas.pidLockedRoll.clamp };
            newPIDGains.Add(rollGains);
            double[] yawGains = { sas.pidLockedYaw.kp, sas.pidLockedYaw.ki, sas.pidLockedYaw.kd, sas.pidLockedYaw.clamp };
            newPIDGains.Add(yawGains);

            PIDGains = newPIDGains;
        }
    }
}
