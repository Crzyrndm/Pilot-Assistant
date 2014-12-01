using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    class PresetSAS
    {
        public string name;
        public List<double[]> PIDGains = new List<double[]>();
        public bool bStockSAS = true;
        private int numControllers = 3;

        public PresetSAS(List<PID.PID_Controller> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            bStockSAS = false;
            for (int i = 0; i < numControllers; i++) // 3 PID controlers to save
            {
                double[] gains = new double[4];
                gains[0] = controllers[i].PGain;
                gains[1] = controllers[i].IGain;
                gains[2] = controllers[i].DGain;
                gains[3] = controllers[i].Scalar;

                PIDGains.Add(gains);
            }
        }

        public PresetSAS(VesselSAS sas, string Name) // used for adding a new stock preset
        {
            name = Name;
            bStockSAS = true;
            double[] pitchGains = {sas.pidLockedPitch.kp, sas.pidLockedPitch.ki, sas.pidLockedPitch.kd, sas.pidLockedPitch.clamp};
            PIDGains.Add(pitchGains);
            double[] rollGains = { sas.pidLockedRoll.kp, sas.pidLockedRoll.ki, sas.pidLockedRoll.kd, sas.pidLockedRoll.clamp };
            PIDGains.Add(rollGains);
            double[] yawGains = { sas.pidLockedYaw.kp, sas.pidLockedYaw.ki, sas.pidLockedYaw.kd, sas.pidLockedYaw.clamp };
            PIDGains.Add(yawGains);
        }

        public PresetSAS(List<double[]> gains, string Name, bool stockSAS) // used for loading presets from file
        {
            name = Name;
            bStockSAS = stockSAS;
            PIDGains = gains;
        }

        public void Update(List<PID.PID_Controller> controllers)
        {
            List<double[]> newPIDGains = new List<double[]>();
            for (int i = 0; i < numControllers; i++) // 3 PID controlers to save
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

        public void Update(VesselSAS sas)
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
