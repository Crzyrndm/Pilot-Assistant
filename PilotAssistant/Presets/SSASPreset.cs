using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    public class SSASPreset
    {
        public string name;
        public double[,] PIDGains = new double[3, 5];

        public SSASPreset(List<PID.SASController> controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
                PIDGains[(int)s, 4] = SurfSAS.Instance.fadeCurrent[(int)s];
            }
        }

        public SSASPreset(PID.SASController[] controllers, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
                PIDGains[(int)s, 4] = SurfSAS.Instance.fadeCurrent[(int)s];
            }
        }

        public SSASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = gains[(int)s][0];
                PIDGains[(int)s, 1] = gains[(int)s][1];
                PIDGains[(int)s, 2] = gains[(int)s][2];
                PIDGains[(int)s, 3] = gains[(int)s][3];
                PIDGains[(int)s, 4] = gains[(int)s][4];
            }
        }

        public void Update(List<PID.SASController> controllers)
        {
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
                PIDGains[(int)s, 4] = SurfSAS.Instance.fadeCurrent[(int)s];
            }
        }

        public void Update(PID.SASController[] controllers)
        {
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = controllers[(int)s].PGain;
                PIDGains[(int)s, 1] = controllers[(int)s].IGain;
                PIDGains[(int)s, 2] = controllers[(int)s].DGain;
                PIDGains[(int)s, 3] = controllers[(int)s].Scalar;
                PIDGains[(int)s, 4] = SurfSAS.Instance.fadeCurrent[(int)s];
            }
        }
    }
}
