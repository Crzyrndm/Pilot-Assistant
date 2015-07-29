using System;
using System.Collections.Generic;

namespace PilotAssistant.PID.Presets
{
    public class RSASPreset
    {
        public string name;
        public PIDConstants[] PIDGains = new PIDConstants[3];

        public RSASPreset(VesselAutopilot.VesselRSAS rsas, string Name) // used for adding a new stock preset
        {
            name = Name;
            for (int i = 0; i < 3; i++)
                PIDGains[i] = new PIDConstants();

            getValuesFromCtrl(Attitude_Controller.Axis.Pitch, rsas.pidPitch);
            getValuesFromCtrl(Attitude_Controller.Axis.Roll, rsas.pidRoll);
            getValuesFromCtrl(Attitude_Controller.Axis.Yaw, rsas.pidYaw);
        }

        public RSASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            for (int i = 0; i < 3; i++)
                PIDGains[i] = new PIDConstants();

            foreach (Attitude_Controller.Axis s in Enum.GetValues(typeof(Attitude_Controller.Axis)))
            {
                PIDGains[(int)s].KP = gains[(int)s][0];
                PIDGains[(int)s].KI = gains[(int)s][1];
                PIDGains[(int)s].KD = gains[(int)s][2];
            }
        }

        public void Update(VesselAutopilot.VesselRSAS rsas)
        {
            getValuesFromCtrl(Attitude_Controller.Axis.Pitch, rsas.pidPitch);
            getValuesFromCtrl(Attitude_Controller.Axis.Roll, rsas.pidRoll);
            getValuesFromCtrl(Attitude_Controller.Axis.Yaw, rsas.pidYaw);
        }

        private void getValuesFromCtrl(Attitude_Controller.Axis id, PIDRclamp axis)
        {
            PIDGains[(int)id].KP = axis.KP;
            PIDGains[(int)id].KI = axis.KI;
            PIDGains[(int)id].KD = axis.KD;
        }
    }
}
