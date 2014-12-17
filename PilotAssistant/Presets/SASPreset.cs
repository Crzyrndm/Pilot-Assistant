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

        public SASPreset(PID.PID_Controller[] controllers, string name) // used for adding a new preset, can clone the current values
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
            double[] gains = new double[4];
            double.TryParse(node.GetValue("PGain"), out gains[0]);
            double.TryParse(node.GetValue("IGain"), out gains[1]);
            double.TryParse(node.GetValue("DGain"), out gains[2]);
            double.TryParse(node.GetValue("Scalar"), out gains[3]);

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
            node.AddNode(GainsToConfigNode("ElevatorController", PIDGains[(int)SASList.Pitch]));
            node.AddNode(GainsToConfigNode("AileronController", PIDGains[(int)SASList.Roll]));
            node.AddNode(GainsToConfigNode("RudderController", PIDGains[(int)SASList.Yaw]));

            return node;
        }

        public void LoadPreset(PID.PID_Controller[] controllers)
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
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kp = PIDGains[(int)SASList.Pitch][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.ki = PIDGains[(int)SASList.Pitch][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.kd = PIDGains[(int)SASList.Pitch][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedPitch.clamp = PIDGains[(int)SASList.Pitch][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kp = PIDGains[(int)SASList.Roll][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.ki = PIDGains[(int)SASList.Roll][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.kd = PIDGains[(int)SASList.Roll][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedRoll.clamp = PIDGains[(int)SASList.Roll][3];

            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kp = PIDGains[(int)SASList.Yaw][0];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.ki = PIDGains[(int)SASList.Yaw][1];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.kd = PIDGains[(int)SASList.Yaw][2];
            Utility.FlightData.thisVessel.VesselSAS.pidLockedYaw.clamp = PIDGains[(int)SASList.Yaw][3];
        }

        public void Update(PID.PID_Controller[] controllers)
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
