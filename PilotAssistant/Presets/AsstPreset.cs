using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    /// <summary>
    /// Holds all the PID tuning values for the 7 (or more if required) controllers involved.
    /// </summary>
    public class AsstPreset
    {
        public string name;
        public List<double[]> PIDGains = new List<double[]>();

        public AsstPreset(List<Asst_PID_Controller> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public AsstPreset(Asst_PID_Controller[] controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            Update(controllers);
        }

        public AsstPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            PIDGains = gains;        
        }

        public void Update(List<Asst_PID_Controller> controllers)
        {
            PIDGains.Clear();
            foreach (Asst_PID_Controller controller in controllers)
            {
                double[] gains = new double[9];
                gains[0] = controller.K_proportional;
                gains[1] = controller.K_integral;
                gains[2] = controller.K_derivative;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.IntegralClampLower;
                gains[6] = controller.IntegralClampUpper;
                gains[7] = controller.Scalar;
                gains[8] = controller.Easing;

                PIDGains.Add(gains);
            }
        }

        public void Update(Asst_PID_Controller[] controllers)
        {
            PIDGains.Clear();
            foreach (Asst_PID_Controller controller in controllers)
            {
                double[] gains = new double[9];
                gains[0] = controller.K_proportional;
                gains[1] = controller.K_integral;
                gains[2] = controller.K_derivative;
                gains[3] = controller.OutMin;
                gains[4] = controller.OutMax;
                gains[5] = controller.IntegralClampLower;
                gains[6] = controller.IntegralClampUpper;
                gains[7] = controller.Scalar;
                gains[8] = controller.Easing;

                PIDGains.Add(gains);
            }
        }
    }
}
