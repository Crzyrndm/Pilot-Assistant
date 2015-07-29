using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using PilotAssistant.Utility;

namespace PilotAssistant.PID
{
    public class Axis_Controller
    {
        protected PIDConstants constants;
        protected double sum = 0;
        public PIDConstants Constants
        {
            get { return constants; }
            set
            {
                constants = value;
                constants.OutMin = Math.Min(constants.OutMin, constants.OutMax);
                constants.IMin = Math.Min(constants.IMin, constants.IMax);
                constants.Scalar = Math.Max(constants.Scalar, 0.01);
            }
        }
        protected Attitude_Controller.Axis axis { get; private set; }
        public double LastOutput { get; private set; }
        public bool Active { get; set; }
        public bool InvertInput { protected get; set; }
        public bool InvertOutput { protected get; set; }
        public bool BShow { get; set; }

        /// <summary>
        /// remember to initialise through some other method
        /// </summary>
        /// <param name="axis"></param>
        public Axis_Controller(Attitude_Controller.Axis Axis)
        {
            Active = true;
            axis = Axis;
            constants = new PIDConstants();
        }

        public Axis_Controller(Attitude_Controller.Axis Axis, double Kp, double Ki, double Kd, double OutputMin = -1, double OutputMax = 1, double intClampLower = -1, double intClampUpper = 1, double scalar = 1, double easing = 1)
        {
            Active = true;
            axis = Axis;

            constants = new PIDConstants(new double[9] { Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing });
        }

        public Axis_Controller(Attitude_Controller.Axis Axis, double[] gains)
        {
            Active = true;
            axis = Axis;
            constants = new PIDConstants(gains);
        }

        public void Initialise(PIDConstants Vals)
        {
            constants = Vals;
        }

        public virtual double ResponseD(double error, double rate, PIDmode mode)
        {
            if (!Active)
                return 0;

            if (InvertInput)
            {
                error *= -1;
                rate *= -1;
            }

            double res_d = 0, res_i = 0, res_p = 0;
            if ((mode & PIDmode.P) != 0)
                res_p = proportionalError(error);
            res_i = integralError(error, (mode & PIDmode.I) != 0);
            if ((mode & PIDmode.D) != 0)
                res_d = derivativeError(rate);

            LastOutput = (InvertOutput ? -1 : 1) * Utils.Clamp(res_p + res_i + res_d, constants.OutMin, constants.OutMax);
            return LastOutput;
        }

        public virtual float ResponseF(double error, double rate, PIDmode mode)
        {
            return (float)ResponseD(error, rate, mode);
        }

        protected virtual double proportionalError(double error)
        {
            return error * constants.KP / constants.Scalar;
        }

        protected virtual double integralError(double error, bool useIntegral)
        {
            if (constants.KI == 0 || !useIntegral)
            {
                sum = 0;
                return sum;
            }

            sum += error * TimeWarp.fixedDeltaTime * constants.KI / constants.Scalar;
            sum = Utils.Clamp(sum, constants.IMin, constants.IMax); // AIW
            return sum;
        }

        protected virtual double derivativeError(double rate)
        {
            return rate * constants.KD / constants.Scalar;
        }

        public virtual void Clear()
        {
            sum = 0;
        }

        public virtual void Preset()
        {
            sum = LastOutput;
        }

        public virtual void Preset(double toSet)
        {
            sum = toSet;
        }
    }
}
