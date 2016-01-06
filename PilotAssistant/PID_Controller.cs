using System;
using UnityEngine;

namespace PilotAssistant
{
    using Utility;
    using FlightModules;

    public class Asst_PID_Controller
    {
        public AsstList ctrlID { get; set; }
        public double target_setpoint { get; set; } // target setpoint
        public double active_setpoint { get; set; } // setpoint being shifted to the target
        public double k_proportional { get; set; }
        public double k_integral { get; set; }
        public double k_derivative { get; set; }
        protected double scale;
        public double inMin { private get; set; }
        public double inMax { private get; set; }
        public double outMin { get; set; }
        public double outMax { get; set; }
        public double integralClampUpper { get; set; }
        public double integralClampLower { get; set; }
        protected double increment = 0; // increment stored because it grows with each frame
        protected double easing = 1; // speed of increment growth

        protected double sum = 0; // integral sum
        protected double previous = 0; // previous value stored for derivative action
        protected double rolling_diff = 0; // used for rolling average difference
        protected double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes

        public double lastOutput { get; protected set; }
        public bool invertInput { get; set; }
        public bool invertOutput { get; set; }
        public bool bShow { get; set; }
        public bool skipDerivative { get; set; }
        public bool isHeadingControl { get; set; }

        public Asst_PID_Controller(AsstList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double shiftRate = 1)
        {
            ctrlID = ID;
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
            outMin = OutputMin;
            outMax = OutputMax;
            integralClampLower = intClampLower;
            integralClampUpper = intClampUpper;
            scale = scalar;
            easing = shiftRate;
            inMin = -double.MaxValue;
            inMax = double.MaxValue;
        }

        public Asst_PID_Controller(AsstList ID, double[] gains)
        {
            ctrlID = ID;
            k_proportional = gains[0];
            k_integral = gains[1];
            k_derivative = gains[2];
            outMin = gains[3];
            outMax = gains[4];
            integralClampLower = gains[5];
            integralClampUpper = gains[6];
            scale = gains[7];
            easing = gains[8];
            inMin = -double.MaxValue;
            inMax = double.MaxValue;
        }
        
        public virtual double ResponseD(double input, bool useIntegral)
        {
            input = Utils.Clamp((invertInput ? -1 : 1) * input, inMin, inMax);
            if (active_setpoint != target_setpoint)
            {
                increment += easing * TimeWarp.fixedDeltaTime * 0.01;
                active_setpoint += Utils.Clamp(target_setpoint - active_setpoint, -increment, increment);
            }
            double error;
            if (!isHeadingControl)
                error = input - active_setpoint;
            else
                error = Utils.CurrentAngleTargetRel(input, active_setpoint, 180) - active_setpoint;

            if (skipDerivative)
            {
                skipDerivative = false;
                previous = input;
            }
            lastOutput = proportionalError(error) + integralError(error, useIntegral) + derivativeError(input);
            lastOutput *= (invertOutput ? -1 : 1);
            lastOutput = Utils.Clamp(lastOutput, outMin, outMax);
            return lastOutput;
        }

        public virtual float ResponseF(double input, bool useIntegral)
        {
            return (float)ResponseD(input, useIntegral);
        }

        protected virtual double proportionalError(double error)
        {
            return error * k_proportional / scale;
        }

        protected virtual double integralError(double error, bool useIntegral)
        {
            if (k_integral == 0 || !useIntegral)
            {
                sum = 0;
                return sum;
            }
            sum += error * TimeWarp.fixedDeltaTime * k_integral / scale;
            sum = Utils.Clamp(sum, integralClampLower, integralClampUpper); // AIW
            return sum;
        }

        protected virtual double derivativeError(double input)
        {
            double difference = 0;
            if (isHeadingControl)
                difference = (Utils.CurrentAngleTargetRel(input, previous, 180) - previous) / TimeWarp.fixedDeltaTime;
            else
                difference = (input - previous) / TimeWarp.fixedDeltaTime;
            previous = input;

            rolling_diff = rolling_diff * rollingFactor + difference * (1 - rollingFactor); // rolling average sometimes helps smooth out a jumpy derivative response
            return rolling_diff * k_derivative / scale;
        }

        protected virtual double derivativeErrorRate(double rate)
        {
            return rate * k_derivative / scale;
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
            sum = lastOutput * (invert ? 1 : -1);
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
            target_setpoint = Utils.Clamp(newSetpoint, inMin, inMax);
            active_setpoint = smooth ? smoothStart : newSetpoint;
            increment = 0;
        }

        public virtual void IncreaseSetpoint(double increaseBy)
        {
            target_setpoint += increaseBy;
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
