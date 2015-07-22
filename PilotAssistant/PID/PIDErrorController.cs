using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.PID
{
    using Utility;
    using FlightModules;

    public enum PIDmode
    {
        PID,
        PD,
        D
    }

    public class PIDErrorController : SASController
    {
        public PIDErrorController(SASList ID, double Kp, double Ki, double Kd, double OutputMin, double OutputMax, double intClampLower, double intClampUpper, double scalar = 1, double easing = 1)
                            : base (ID, Kp, Ki, Kd, OutputMin, OutputMax, intClampLower, intClampUpper, scalar, easing)
        { }

        public PIDErrorController(SASList ID, double[] gains)
            : base(ID, gains)
        { }

        public virtual double ResponseD(double error, double rate, PIDmode mode)
        {
            if (invertInput)
            {
                error *= -1;
                rate *= -1;
            }

            double res_d = 0, res_i = 0, res_p = 0;
            res_d = derivativeError(rate);
            if (mode == PIDmode.PID)
                res_i = integralError(error, true);
            if (mode == PIDmode.PD || mode == PIDmode.PID)
                res_p = proportionalError(error);

            lastOutput = (invertOutput ? -1 : 1) * Utils.Clamp(res_p + res_i + res_d, OutMin, OutMax);
            return lastOutput;
        }

        public virtual float ResponseF(double error, double rate, PIDmode mode)
        {
            return (float)ResponseD(error, rate, mode);
        }

        protected override double derivativeError(double rate)
        {
            return rate * k_derivative / scale;
        }
    }
}
