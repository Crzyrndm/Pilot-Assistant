using System;
using UnityEngine;

namespace PilotAid.PID
{
    public class PID_Controller : MonoBehaviour
    {
        private double setpoint = 0; // process setpoint

        private double k_proportional = 0; // Kp
        private double k_integral = 0; // Ki
        private double k_derivative = 0; // Kd

        private double sum = 0; // integral sum
        private double previous = 0; // previous value stored for derivative action
        private double rolling_diff = 0; // used for rolling average difference
        private double rollingFactor = 0; // rolling average proportion. 0 = all new, 1 = never changes
        private double error = 0; // error of current iteration

        private double inMin = -180; // Minimum input value
        private double inMax = 180; // Maximum input value

        private double outMin = -1; // Minimum output value
        private double outMax = 1; // Maximum output value

        private double integralClampUpper = 1; // AIW clamp
        private double integralClampLower = -1; // AIW clamp

        private double dt = 1; // standardised response for any physics dt setting. Check if this can be changed after module initialisation (ie. in flight)

        public PID_Controller(double Kp, double Ki, double Kd)
        {
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
        }

        public double Response(double input)
        {
            input = Clamp(input, inMin, inMax);
            print("input: " + input.ToString());
            dt = TimeWarp.fixedDeltaTime;
            print("dt: " + dt.ToString());
            error = input - setpoint;
            print("error" + error.ToString());
            return Clamp(proportionalError(input) + integralError(input) + derivativeError(input), outMin, outMax);
        }

        private double proportionalError(double input)
        {
            if (k_proportional == 0)
                return 0;
            return error * k_proportional;
        }

        private double integralError(double input)
        {
            if (k_integral == 0)
                return 0;

            sum += error * dt * k_integral;
            Clamp(sum, integralClampLower, integralClampUpper); // AIW

            return sum;
        }

        private double derivativeError(double input)
        {
            if (k_derivative == 0)
                return 0;

            double difference = (input - previous) / dt;
            rolling_diff = rolling_diff * rollingFactor + difference * (1 - rollingFactor); // rolling average might help smooth out jumpy derivative response
            
            previous = input;
            return rolling_diff * k_derivative;
        }

        #region utility functions

        /// <summary>
        /// Clamp double input between maximum and minimum value
        /// </summary>
        /// <param name="val">variable to be clamped</param>
        /// <param name="min">minimum output value of the variable</param>
        /// <param name="max">maximum output value of the variable</param>
        /// <returns>val clamped between max and min</returns>
        internal static double Clamp(double val, double min, double max)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            else
                return val;
        }

        /// <summary>
        /// Linear interpolation between two points
        /// </summary>
        /// <param name="pct">fraction of travel from the minimum to maximum. Can be less than 0 or greater than 1</param>
        /// <param name="lower">reference point treated as the base (pct = 0)</param>
        /// <param name="upper">reference point treated as the target (pct = 1)</param>
        /// <param name="clamp">clamp pct input between 0 and 1?</param>
        /// <returns></returns>
        internal static double Lerp(double pct, double lower, double upper, bool clamp = true)
        {
            if (clamp)
            {
                pct = Clamp(pct, 0, 1);
            }
            return (1 - pct) * lower + pct * upper;
        }

        #endregion

        #region properties
        public double SetPoint
        {
            set
            {
                setpoint = value;
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

        public double OutMin
        {
            set
            {
                outMin = value;
            }
        }

        public double OutMax
        {
            set
            {
                outMax = value;
            }
        }

        public double ClampLower
        {
            set
            {
                integralClampLower = value;
            }
        }

        public double ClampUpper
        {
            set
            {
                integralClampUpper = value;
            }
        }

        public double RollingFactor
        {
            set
            {
                rollingFactor = Clamp(value, 0, 1);
            }
        }

        #endregion
    }
}
