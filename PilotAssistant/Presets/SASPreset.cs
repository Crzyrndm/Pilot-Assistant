using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.Presets
{
    using FlightModules;
    public class SASPreset
    {
        public string name;
        public double[,] PIDGains = new double[3, 4];

        public SASPreset(VesselAutopilot.VesselSAS sas, string Name) // used for adding a new stock preset
        {
            name = Name;

            PIDclamp[] sasPID = new PIDclamp[3];
            sasPID[(int)SASList.Pitch] = sas.pidLockedPitch;
            sasPID[(int)SASList.Bank] = sas.pidLockedRoll;
            sasPID[(int)SASList.Hdg] = sas.pidLockedYaw;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = sasPID[(int)s].kp;
                PIDGains[(int)s, 1] = sasPID[(int)s].ki;
                PIDGains[(int)s, 2] = sasPID[(int)s].kd;
                PIDGains[(int)s, 3] = sasPID[(int)s].clamp;
            }
        }

        public SASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = gains[(int)s][0];
                PIDGains[(int)s, 1] = gains[(int)s][1];
                PIDGains[(int)s, 2] = gains[(int)s][2];
                PIDGains[(int)s, 3] = gains[(int)s][3];
            }
        }

        public void Update(VesselAutopilot.VesselSAS sas)
        {
            PIDclamp[] sasPID = new PIDclamp[3];
            sasPID[(int)SASList.Pitch] = sas.pidLockedPitch;
            sasPID[(int)SASList.Bank] = sas.pidLockedRoll;
            sasPID[(int)SASList.Hdg] = sas.pidLockedYaw;

            foreach (SASList s in Enum.GetValues(typeof(SASList)))
            {
                PIDGains[(int)s, 0] = sasPID[(int)s].kp;
                PIDGains[(int)s, 1] = sasPID[(int)s].ki;
                PIDGains[(int)s, 2] = sasPID[(int)s].kd;
                PIDGains[(int)s, 3] = sasPID[(int)s].clamp;
            }
        }
    }
}
