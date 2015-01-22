using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    using PID;
    /// <summary>
    /// Holds all the PID tuning values for the 7 (or more if required) controllers involved.
    /// </summary>
    class PresetPA
    {
        public string name;
        public List<double[]> PIDGains = new List<double[]>();

        public PresetPA(List<PID_Controller> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public PresetPA(PID_Controller[] controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public PresetPA(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            PIDGains = gains;        
        }

        public void Update(List<PID_Controller> controllers)
        {
            PIDGains.Clear();
            foreach (PID_Controller controller in controllers)
            {
                double[] gains = new double[8];
                gains[0] = controller.PGain;
                gains[1] = controller.IGain;
                gains[2] = controller.DGain;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.ClampLower;
                gains[6] = controller.ClampUpper;
                gains[7] = controller.Scalar;

                PIDGains.Add(gains);
            }
        }

        public void Update(PID_Controller[] controllers)
        {
            PIDGains.Clear();
            foreach (PID_Controller controller in controllers)
            {
                double[] gains = new double[8];
                gains[0] = controller.PGain;
                gains[1] = controller.IGain;
                gains[2] = controller.DGain;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.ClampLower;
                gains[6] = controller.ClampUpper;
                gains[7] = controller.Scalar;

                PIDGains.Add(gains);
            }
        }
    }
}
