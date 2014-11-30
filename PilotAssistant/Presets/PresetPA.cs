using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    /// <summary>
    /// Holds all the PID tuning values for the 7 (or more if required) controllers involved.
    /// </summary>
    class PresetPA
    {
        public string name;
        public List<double[]> PIDGains = new List<double[]>();
        private int numControllers = 7;

        public PresetPA(List<PID.PID_Controller> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            for (int i = 0; i < numControllers; i++) // currently 7 PID controlers to save
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

        public PresetPA(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            PIDGains = gains;        
        }

        public void Update(List<PID.PID_Controller> controllers)
        {
            List<double[]> newPIDGains = new List<double[]>();
            for (int i = 0; i < numControllers; i++) // currently 7 PID controlers to save
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
