using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;
    using FlightModules;
    class PIDErrorController : SASController
    {
        public PIDErrorController(SASList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
                            : base (ID, Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing)
        { }

        public PIDErrorController(SASList ID, double[] gains)
            : base(ID, gains)
        { }

        public virtual double ResponseD(double error, double rate)
        {
            double res_d, res_i, res_p;
            res_d = derivativeError(rate);
            res_i = integralError(error);
            res_p = proportionalError(error);

            return res_p + res_i + res_d;
        }

        public virtual float ResponseF(double error, double rate)
        {
            return (float)ResponseD(error, rate);
        }

        protected override double derivativeError(double rate)
        {
            return rate * k_derivative / scale;
        }
    }
}
