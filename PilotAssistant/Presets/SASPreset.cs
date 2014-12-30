using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    using Utility;

    public class SASPreset
    {
        private string name;
        private double[][] pidGains = new double[3][];
        private bool useStockSAS = true;

        public SASPreset(PID.PID_Controller[] controllers, string name) // used for adding a new preset, can clone the current values
        {
            this.name = name;
            useStockSAS = false;
            for (int i = 0; i < pidGains.Length; i++)
            {
                double[] gains = new double[4];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].Scalar;

                pidGains[i] = gains;
            }
        }

        public SASPreset(VesselAutopilot.VesselSAS sas, string name) // used for adding a new stock preset
        {
            this.name = name;
            useStockSAS = true;
            double[] pitchGains = { sas.pidLockedPitch.kp, sas.pidLockedPitch.ki, sas.pidLockedPitch.kd, sas.pidLockedPitch.clamp };
            double[] rollGains = { sas.pidLockedRoll.kp, sas.pidLockedRoll.ki, sas.pidLockedRoll.kd, sas.pidLockedRoll.clamp };
            double[] yawGains = { sas.pidLockedYaw.kp, sas.pidLockedYaw.ki, sas.pidLockedYaw.kd, sas.pidLockedYaw.clamp };
            pidGains[(int)SASList.Pitch] = pitchGains;
            pidGains[(int)SASList.Roll] = rollGains;
            pidGains[(int)SASList.Yaw] = yawGains;
        }

        public SASPreset(ConfigNode node) // used for loading presets from file
        {
            name = node.GetValue("name");
            useStockSAS = bool.Parse(node.GetValue("stock"));
            double[] pitchGains = LoadControllerGains(node.GetNode("ElevatorController"));
            double[] rollGains = LoadControllerGains(node.GetNode("AileronController"));
            double[] yawGains = LoadControllerGains(node.GetNode("RudderController"));
            pidGains[(int)SASList.Pitch] = pitchGains;
            pidGains[(int)SASList.Roll] = rollGains;
            pidGains[(int)SASList.Yaw] = yawGains;
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
            node.AddNode(GainsToConfigNode("ElevatorController", pidGains[(int)SASList.Pitch]));
            node.AddNode(GainsToConfigNode("AileronController", pidGains[(int)SASList.Roll]));
            node.AddNode(GainsToConfigNode("RudderController", pidGains[(int)SASList.Yaw]));
            return node;
        }

        public void LoadPreset(PID.PID_Controller[] controllers)
        {
            for (int i = 0; i < pidGains.Length; i++)
            {
                controllers[i].PGain = pidGains[i][0];
                controllers[i].IGain = pidGains[i][1];
                controllers[i].DGain = pidGains[i][2];
                controllers[i].Scalar = pidGains[i][3];
            }
        }

        public void LoadStockPreset(VesselAutopilot.VesselSAS sas)
        {
            sas.pidLockedPitch.kp = pidGains[(int)SASList.Pitch][0];
            sas.pidLockedPitch.ki = pidGains[(int)SASList.Pitch][1];
            sas.pidLockedPitch.kd = pidGains[(int)SASList.Pitch][2];
            sas.pidLockedPitch.clamp = pidGains[(int)SASList.Pitch][3];

            sas.pidLockedRoll.kp = pidGains[(int)SASList.Roll][0];
            sas.pidLockedRoll.ki = pidGains[(int)SASList.Roll][1];
            sas.pidLockedRoll.kd = pidGains[(int)SASList.Roll][2];
            sas.pidLockedRoll.clamp = pidGains[(int)SASList.Roll][3];

            sas.pidLockedYaw.kp = pidGains[(int)SASList.Yaw][0];
            sas.pidLockedYaw.ki = pidGains[(int)SASList.Yaw][1];
            sas.pidLockedYaw.kd = pidGains[(int)SASList.Yaw][2];
            sas.pidLockedYaw.clamp = pidGains[(int)SASList.Yaw][3];
        }

        public void Update(PID.PID_Controller[] controllers)
        {
            for (int i = 0; i < pidGains.Length; i++)
            {
                double[] gains = new double[4];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].Scalar;

                pidGains[i] = gains;
            }
        }

        public void UpdateStock(VesselAutopilot.VesselSAS sas)
        {
            double[] pitchGains = { sas.pidLockedPitch.kp, sas.pidLockedPitch.ki, sas.pidLockedPitch.kd, sas.pidLockedPitch.clamp };
            double[] rollGains = { sas.pidLockedRoll.kp, sas.pidLockedRoll.ki, sas.pidLockedRoll.kd, sas.pidLockedRoll.clamp };
            double[] yawGains = { sas.pidLockedYaw.kp, sas.pidLockedYaw.ki, sas.pidLockedYaw.kd, sas.pidLockedYaw.clamp };
            pidGains[(int)SASList.Pitch] = pitchGains;
            pidGains[(int)SASList.Roll] = rollGains;
            pidGains[(int)SASList.Yaw] = yawGains;
        }
    }
}
