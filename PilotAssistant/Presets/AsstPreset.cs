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
    public class AsstPreset
    {
        public string name;
        public List<double[]> PIDGains = new List<double[]>();

        public AsstPreset(List<AsstController> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public AsstPreset(AsstController[] controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public AsstPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            PIDGains = gains;        
        }

        public void Update(List<AsstController> controllers)
        {
            PIDGains.Clear();
            foreach (AsstController controller in controllers)
            {
                double[] gains = new double[9];
                gains[0] = controller.PGain;
                gains[1] = controller.IGain;
                gains[2] = controller.DGain;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.ClampLower;
                gains[6] = controller.ClampUpper;
                gains[7] = controller.Scalar;
                gains[8] = controller.Easing;

                PIDGains.Add(gains);
            }
        }

        public void Update(AsstController[] controllers)
        {
            PIDGains.Clear();
            foreach (AsstController controller in controllers)
            {
                double[] gains = new double[9];
                gains[0] = controller.PGain;
                gains[1] = controller.IGain;
                gains[2] = controller.DGain;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.ClampLower;
                gains[6] = controller.ClampUpper;
                gains[7] = controller.Scalar;
                gains[8] = controller.Easing;

                PIDGains.Add(gains);
            }
        }
    }
}
