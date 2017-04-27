using System;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;
    using FlightModules;

    public class Asst_PID_Controller
    {
        public AsstList CtrlID { get; set; }
        public double Target_setpoint { get; set; } // target setpoint
        public double Active_setpoint { get; set; } // setpoint being shifted to the target
        public double K_proportional { get; set; }
        public double K_integral { get; set; }
        public double K_derivative { get; set; }
        protected double scale;
        public double InMin { private get; set; }
        public double InMax { private get; set; }
        public double OutMin { get; set; }
        public double OutMax { get; set; }
        public double IntegralClampUpper { get; set; }
        public double IntegralClampLower { get; set; }
        protected double increment = 0; // increment stored because it grows with each frame
        protected double easing = 1; // speed of increment growth

        protected double sum = 0; // integral sum
        protected double previous = 0; // previous value stored for derivative action
        protected double rolling_diff = 0; // used for rolling average difference
        protected double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes

        public double LastOutput { get; protected set; }
        public bool InvertInput { get; set; }
        public bool InvertOutput { get; set; }
        public bool BShow { get; set; }
        public bool SkipDerivative { get; set; }
        public bool IsHeadingControl { get; set; }

        public Asst_PID_Controller(AsstList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double shiftRate = 1)
        {
            CtrlID = ID;
            K_proportional = Kp;
            K_integral = Ki;
            K_derivative = Kd;
            OutMin = OutputMin;
            OutMax = OutputMax;
            IntegralClampLower = intClampLower;
            IntegralClampUpper = intClampUpper;
            scale = scalar;
            easing = shiftRate;
            InMin = -double.MaxValue;
            InMax = double.MaxValue;
        }

        public Asst_PID_Controller(AsstList ID, double[] gains)
        {
            CtrlID = ID;
            K_proportional = gains[0];
            K_integral = gains[1];
            K_derivative = gains[2];
            OutMin = gains[3];
            OutMax = gains[4];
            IntegralClampLower = gains[5];
            IntegralClampUpper = gains[6];
            scale = gains[7];
            easing = gains[8];
            InMin = -double.MaxValue;
            InMax = double.MaxValue;
        }
        
        public virtual double ResponseD(double input, bool useIntegral)
        {
            input = Utils.Clamp((InvertInput ? -1 : 1) * input, InMin, InMax);
            if (Active_setpoint != Target_setpoint)
            {
                increment += easing * TimeWarp.fixedDeltaTime * 0.01;
                Active_setpoint += Utils.Clamp(Target_setpoint - Active_setpoint, -increment, increment);
            }
            double error;
            if (!IsHeadingControl)
            {
                error = input - Active_setpoint;
            }
            else
            {
                error = Utils.CurrentAngleTargetRel(input, Active_setpoint, 180) - Active_setpoint;
            }

            if (SkipDerivative)
            {
                SkipDerivative = false;
                previous = input;
            }
            LastOutput = ProportionalError(error) + IntegralError(error, useIntegral) + DerivativeError(input);
            LastOutput *= (InvertOutput ? -1 : 1);
            LastOutput = Utils.Clamp(LastOutput, OutMin, OutMax);
            return LastOutput;
        }

        public virtual float ResponseF(double input, bool useIntegral)
        {
            return (float)ResponseD(input, useIntegral);
        }

        protected virtual double ProportionalError(double error)
        {
            return error * K_proportional / scale;
        }

        protected virtual double IntegralError(double error, bool useIntegral)
        {
            if (K_integral == 0 || !useIntegral)
            {
                sum = 0;
                return sum;
            }
            sum += error * TimeWarp.fixedDeltaTime * K_integral / scale;
            sum = Utils.Clamp(sum, IntegralClampLower, IntegralClampUpper); // AIW
            return sum;
        }

        protected virtual double DerivativeError(double input)
        {
            double difference = 0;
            if (IsHeadingControl)
            {
                difference = (Utils.CurrentAngleTargetRel(input, previous, 180) - previous) / TimeWarp.fixedDeltaTime;
            }
            else
            {
                difference = (input - previous) / TimeWarp.fixedDeltaTime;
            }

            previous = input;

            rolling_diff = rolling_diff * rollingFactor + difference * (1 - rollingFactor); // rolling average sometimes helps smooth out a jumpy derivative response
            return rolling_diff * K_derivative / scale;
        }

        protected virtual double DerivativeErrorRate(double rate)
        {
            return rate * K_derivative / scale;
        }

        public virtual void Clear()
        {
            sum = 0;
        }

        /// <summary>
        /// Set the integral up to resume from its last loop. Used to smoothly resume control
        /// </summary>
        /// <param name="invert">set true if control is reversed for some reason</param>
        public virtual void Preset(bool invert = false)
        {
            sum = LastOutput * (invert ? 1 : -1);
        }

        /// <summary>
        /// Set the integral to resume from a new target. Used to smoothly resume control
        /// </summary>
        /// <param name="target"></param>
        /// <param name="invert"></param>
        public virtual void Preset(double target, bool invert = false)
        {
            sum = target * (invert ? 1 : -1);
        }

        public virtual void UpdateSetpoint(double newSetpoint, bool smooth = false, double smoothStart = 0)
        {
            Target_setpoint = Utils.Clamp(newSetpoint, InMin, InMax);
            Active_setpoint = smooth ? smoothStart : newSetpoint;
            increment = 0;
        }

        public virtual void IncreaseSetpoint(double increaseBy)
        {
            Target_setpoint += increaseBy;
        }

        #region properties
        public virtual double Scalar
        {
            get
            {
                return scale;
            }
            set
            {
                scale = Math.Max(value, 0.01);
            }
        }

        public virtual double Easing
        {
            get
            {
                return easing;
            }
            set
            {
                easing = Math.Max(value, 0.01);
            }
        }
        #endregion
    }
}
