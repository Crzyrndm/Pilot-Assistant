using System;
using System.Collections.Generic;

namespace PilotAssistant.PID.Presets
{
    public class SASPreset
    {
        public string name;
        public PIDConstants[] PIDGains = new PIDConstants[3];

        public SASPreset(VesselAutopilot.VesselSAS sas, string Name) // used for adding a new stock preset
        {
            name = Name;
            for (int i = 0; i < 3; i++)
                PIDGains[i] = new PIDConstants();

            getValuesFromCtrl(Attitude_Controller.Axis.Pitch, sas.pidLockedPitch);
            getValuesFromCtrl(Attitude_Controller.Axis.Roll, sas.pidLockedRoll);
            getValuesFromCtrl(Attitude_Controller.Axis.Yaw, sas.pidLockedYaw);
        }

        public SASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            for (int i = 0; i < 3; i++)
                PIDGains[i] = new PIDConstants();

            foreach (Attitude_Controller.Axis s in Enum.GetValues(typeof(Attitude_Controller.Axis)))
            {
                PIDGains[(int)s].KP = gains[(int)s][0];
                PIDGains[(int)s].KI = gains[(int)s][1];
                PIDGains[(int)s].KD = gains[(int)s][2];
                PIDGains[(int)s].Scalar = gains[(int)s][3];
            }
        }

        public void Update(VesselAutopilot.VesselSAS sas)
        {
            getValuesFromCtrl(Attitude_Controller.Axis.Pitch, sas.pidLockedPitch);
            getValuesFromCtrl(Attitude_Controller.Axis.Roll, sas.pidLockedRoll);
            getValuesFromCtrl(Attitude_Controller.Axis.Yaw, sas.pidLockedYaw);
        }

        private void getValuesFromCtrl(Attitude_Controller.Axis id, PIDclamp axis)
        {
            PIDGains[(int)id].KP = axis.kp;
            PIDGains[(int)id].KI = axis.ki;
            PIDGains[(int)id].KD = axis.kd;
            PIDGains[(int)id].Scalar = axis.clamp;
        }
    }
}
