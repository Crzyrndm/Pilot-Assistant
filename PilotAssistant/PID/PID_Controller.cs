﻿using System;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;

    public class PID_Controller : MonoBehaviour
    {
        private double setpoint = 0; // process setpoint

        private double k_proportional; // Kp
        private double k_integral; // Ki
        private double k_derivative; // Kd

        private double sum = 0; // integral sum
        private double previous = 0; // previous value stored for derivative action
        private double rolling_diff = 0; // used for rolling average difference
        private double rollingFactor = 0.5; // rolling average proportion. 0 = all new, 1 = never changes
        private double error = 0; // error of current iteration

        private double inMin = -1000000000; // Minimum input value
        private double inMax = 1000000000; // Maximum input value

        private double outMin; // Minimum output value
        private double outMax; // Maximum output value

        private double integralClampUpper; // AIW clamp
        private double integralClampLower; // AIW clamp

        private double dt = 1; // standardised response for any physics dt

        private double scale = 1;

        internal bool bShow = false;
        internal bool skipDerivative = false;

        public PID_Controller(double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1)
        {
            k_proportional = Kp;
            k_integral = Ki;
            k_derivative = Kd;
            outMin = OutputMin;
            outMax = OutputMax;
            integralClampLower = intClampLower;
            integralClampUpper = intClampUpper;
            scale = scalar;
        }

        public double Response(double input)
        {
            input = Functions.Clamp(input, inMin, inMax);
            dt = TimeWarp.fixedDeltaTime;
            error = input - setpoint;
            double response = proportionalError(error) + integralError(error);
            if (!skipDerivative)
                response += derivativeError(input);
            else
            {
                skipDerivative = false;
                previous = input;
            }
            return Functions.Clamp(response, outMin, outMax);
        }

        private double proportionalError(double input)
        {
            if (k_proportional == 0)
                return 0;
            return input * k_proportional / scale;
        }

        private double integralError(double input)
        {
            if (k_integral == 0 || FlightData.thisVessel.checkLanded())
            {
                sum = 0;
                return sum;
            }

            sum += input * dt * k_integral / scale;
            sum = Functions.Clamp(sum, integralClampLower, integralClampUpper); // AIW

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

        #region utility functions

        #endregion

        #region properties
        public double SetPoint
        {
            get
            {
                return setpoint;
            }
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

        /// <summary>
        /// Set output minimum and anti-integral windup to value
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
        /// Set output maximum and anti-integral windup to value
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

        public double RollingFactor
        {
            set
            {
                rollingFactor = Functions.Clamp(value, 0, 1);
            }
        }

        #endregion
    }
}
