using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;
    using FlightModules;
    public class PIDErrorController : SASController
    {
        public PIDErrorController(SASList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
                            : base (ID, Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing)
        { }

        public PIDErrorController(SASList ID, double[] gains)
            : base(ID, gains)
        { }

        public virtual double ResponseD(double error, double rate, bool useIntegral)
        {
            double res_d, res_i, res_p;
            res_d = derivativeError(rate);
            res_i = integralError(error, useIntegral);
            res_p = proportionalError(error);

            return Utils.Clamp(res_p + res_i + res_d, OutMin, OutMax);
        }

        public virtual float ResponseF(double error, double rate, bool useIntegral)
        {
            return (float)ResponseD(error, rate, useIntegral);
        }

        protected override double derivativeError(double rate)
        {
            return rate * k_derivative / scale;
        }
    }
}
