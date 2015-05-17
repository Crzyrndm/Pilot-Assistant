using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    using FlightModules;
    public class RSASPreset
    {
        public string name;
        public double[,] PIDGains = new double[3, 3];

        public RSASPreset(VesselAutopilot.VesselRSAS rsas, string Name) // used for adding a new stock preset
        {
            name = Name;

            PIDRclamp[] rsasPID = new PIDRclamp[3];
            rsasPID[(int)SASList.Pitch] = rsas.pidPitch;
            rsasPID[(int)SASList.Bank] = rsas.pidRoll;
            rsasPID[(int)SASList.Hdg] = rsas.pidYaw;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = rsasPID[(int)s].KP;
                PIDGains[(int)s, 1] = rsasPID[(int)s].KI;
                PIDGains[(int)s, 2] = rsasPID[(int)s].KD;
            }
        }

        public RSASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = gains[(int)s][0];
                PIDGains[(int)s, 1] = gains[(int)s][1];
                PIDGains[(int)s, 2] = gains[(int)s][2];
            }
        }

        public void Update(VesselAutopilot.VesselRSAS rsas)
        {
            PIDRclamp[] sasPID = new PIDRclamp[3];
            sasPID[(int)SASList.Pitch] = rsas.pidPitch;
            sasPID[(int)SASList.Bank] = rsas.pidRoll;
            sasPID[(int)SASList.Hdg] = rsas.pidYaw;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = sasPID[(int)s].KP;
                PIDGains[(int)s, 1] = sasPID[(int)s].KI;
                PIDGains[(int)s, 2] = sasPID[(int)s].KD;
            }
        }
    }
}
