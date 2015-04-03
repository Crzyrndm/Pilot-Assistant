using System;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;

    public class PID_Controller : MonoBehaviour
    {
        double target_setpoint = 0; // target setpoint
        double active_setpoint = 0;

        double k_proportional; // Kp
        double k_integral; // Ki
        double k_derivative; // Kd

        double sum = 0; // integral sum
        double previous = 0; // previous value stored for derivative action
        double rolling_diff = 0; // used for rolling average difference
        double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes
        double error = 0; // error of current iteration

        double inMin = -1000000000; // Minimum input value
        double inMax = 1000000000; // Maximum input value

        double outMin; // Minimum output value
        double outMax; // Maximum output value

        double integralClampUpper; // AIW clamp
        double integralClampLower;

        double dt = 1; // standardised response for any physics dt

        double scale = 1;
        double easing = 1;
        double increment = 0;

        public bool bShow = false;
        public bool skipDerivative = false;

        public PID_Controller(double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
        {
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
            outMin = OutputMin;
            outMax = OutputMax;
            integralClampLower = intClampLower;
            integralClampUpper = intClampUpper;
            scale = scalar;
            this.easing = easing;
        }

        public PID_Controller(double[] gains)
        {
            k_proportional = gains[0];
            k_integral = gains[1];
            k_derivative = gains[2];
            outMin = gains[3];
            outMax = gains[4];
            integralClampLower = gains[5];
            integralClampUpper = gains[6];
            scale = gains[7];
            easing = gains[8];
        }

        public double ResponseD(double input)
        {
            if (active_setpoint != target_setpoint)
            {
                increment += easing * TimeWarp.fixedDeltaTime * 0.01;
                if (active_setpoint < target_setpoint)
                {
                    if (active_setpoint + increment > target_setpoint)
                        active_setpoint = target_setpoint;
                    else
                        active_setpoint += increment;
                }
                else
                {
                    if (active_setpoint - increment < target_setpoint)
                        active_setpoint = target_setpoint;
                    else
                        active_setpoint -= increment;
                }
            }
            input = Clamp(input, inMin, inMax);

            dt = TimeWarp.fixedDeltaTime;
            error = input - active_setpoint;

            if (skipDerivative)
            {
                skipDerivative = false;
                previous = input;
            }
            return Clamp((proportionalError(error) + integralError(error) + derivativeError(input)), outMin, outMax);
        }

        public float ResponseF(double input)
        {
            return (float)ResponseD(input);
        }

        private double proportionalError(double error)
        {
            return error * k_proportional / scale;
        }

        private double integralError(double error)
        {
            if (k_integral == 0 || FlightData.thisVessel.checkLanded()|| !FlightData.thisVessel.IsControllable)
            {
                sum = 0;
                return sum;
            }

            sum += error * dt * k_integral / scale;
            sum = Clamp(sum, integralClampLower, integralClampUpper); // AIW
            return sum;
        }

        private double derivativeError(double input)
        {
            if (k_derivative == 0)
                return 0;

            double difference = (input - previous) / dt;
            rolling_diff = rolling_diff * rollingFactor + difference * (1 - rollingFactor); // rolling average sometimes helps smooth out a jumpy derivative response
            
            previous = input;
            return rolling_diff * k_derivative / scale;
        }

        public void Clear()
        {
            sum = 0;
        }

        public void Preset(double target)
        {
            sum = target;
        }

        #region utility functions

        /// <summary>
        /// Clamp double input between maximum and minimum value
        /// </summary>
        /// <param name="val">variable to be clamped</param>
        /// <param name="min">minimum output value of the variable</param>
        /// <param name="max">maximum output value of the variable</param>
        /// <returns>val clamped between max and min</returns>
        public static double Clamp(double val, double min, double max)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            else
                return val;
        }

        #endregion

        #region properties
        public double SetPoint
        {
            get
            {
                return target_setpoint;
            }
            set
            {
                target_setpoint = value;
                active_setpoint = value;
            }
        }

        // let active setpoint move to match the target to smooth the transition
        public double BumplessSetPoint
        {
            get
            {
                return active_setpoint;
            }
            set
            {
                target_setpoint = value;
                increment = 0;
            }
        }

        public double PGain
        {
            get
            {
                return k_proportional;
            }
            set
            {
                k_proportional = value;
            }
        }

        public double IGain
        {
            get
            {
                return k_integral;
            }
            set
            {
                k_integral = value;
            }
        }

        public double DGain
        {
            get
            {
                return k_derivative;
            }
            set
            {
                k_derivative = value;
            }
        }

        public double InMin
        {
            set
            {
                inMin = value;
            }
        }

        public double InMax
        {
            set
            {
                inMax = value;
            }
        }

        /// <summary>
        /// Set output minimum to value
        /// </summary>
        public double OutMin
        {
            get
            {
                return outMin;
            }
            set
            {
                outMin = value;
            }
        }

        /// <summary>
        /// Set output maximum to value
        /// </summary>
        public double OutMax
        {
            get
            {
                return outMax;
            }
            set
            {
                outMax = value;
            }
        }

        public double ClampLower
        {
            get
            {
                return integralClampLower;
            }
            set
            {
                integralClampLower = value;
            }
        }

        public double ClampUpper
        {
            get
            {
                return integralClampUpper;
            }
            set
            {
                integralClampUpper = value;
            }
        }

        public double Scalar
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

        public double Easing
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
