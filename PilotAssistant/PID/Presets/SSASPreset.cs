using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PilotAssistant.PID.Presets
{
    using FlightModules;
    public class SSASPreset
    {
        public string name;
        public PIDConstants[] PIDGains = new PIDConstants[3];

        public SSASPreset(PIDConstants pitch, PIDConstants yaw, PIDConstants roll, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;

            PIDGains[(int)Attitude_Controller.Axis.Pitch] = pitch;
            PIDGains[(int)Attitude_Controller.Axis.Yaw] = yaw;
            PIDGains[(int)Attitude_Controller.Axis.Roll] = roll;
        }
        
        public SSASPreset(Attitude_Controller controller, string Name) // used for adding a new preset, can clone the current values
        {
            name = Name;
            foreach (Attitude_Controller.Axis s in Enum.GetValues(typeof(Attitude_Controller.Axis)))
            {
                PIDGains[(int)s] = new PIDConstants();
                PIDGains[(int)s].KP = controller.GetCtrl(s).Constants.KP;
                PIDGains[(int)s].KI = controller.GetCtrl(s).Constants.KI;
                PIDGains[(int)s].KD = controller.GetCtrl(s).Constants.KD;
                PIDGains[(int)s].Scalar = controller.GetCtrl(s).Constants.Scalar;
            }
        }

        public SSASPreset(List<double[]> gains, string Name) // used for loading presets from file
        {
            name = Name;
            foreach (Attitude_Controller.Axis s in Enum.GetValues(typeof(Attitude_Controller.Axis)))
            {
                PIDGains[(int)s] = new PIDConstants();
                PIDGains[(int)s].KP = gains[(int)s][0];
                PIDGains[(int)s].KI = gains[(int)s][1];
                PIDGains[(int)s].KD = gains[(int)s][2];
                PIDGains[(int)s].Scalar = gains[(int)s][3];
            }
        }

        public void Update(Attitude_Controller controller)
        {
            foreach (Attitude_Controller.Axis s in Enum.GetValues(typeof(Attitude_Controller.Axis)))
            {
                PIDGains[(int)s].KP = controller.GetCtrl(s).Constants.KP;
                PIDGains[(int)s].KI = controller.GetCtrl(s).Constants.KI;
                PIDGains[(int)s].KD = controller.GetCtrl(s).Constants.KD;
                PIDGains[(int)s].Scalar = controller.GetCtrl(s).Constants.Scalar;
            }
        }

        public void Update(PIDConstants update, Attitude_Controller.Axis toUpdate)
        {
            PIDGains[(int)toUpdate] = update;
        }
    }
}
