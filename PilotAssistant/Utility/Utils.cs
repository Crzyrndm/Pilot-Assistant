using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.Utility
{
    using PID;
    using FlightModules;

    public static class Utils
    {
        public static float Clamp(this float val, float min, float max)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            else
                return val;
        }

        public static double Clamp(this double val, double min, double max)
        {
            if (val < min)
                return min;
            else if (val > max)
                return max;
            else
                return val;
        }

        public static AsstController GetAsst(this AsstList id, PilotAssistant instance)
        {
            return instance.controllers[(int)id];
        }

        public static PIDErrorController GetSAS(this SASList id)
        {
            return SurfSAS.Instance.SASControllers[(int)id];
        }

        public static bool isFlightControlLocked()
        {
            return (InputLockManager.IsLocked(ControlTypes.PITCH) && !PilotAssistant.pitchLockEngaged) || InputLockManager.IsLocked(ControlTypes.ROLL)
                    || (InputLockManager.IsLocked(ControlTypes.YAW) && !PilotAssistant.yawLockEngaged) || InputLockManager.IsLocked(ControlTypes.THROTTLE);
        }

        /// <summary>
        /// Circular rounding to keep compass measurements within a 360 degree range
        /// maxHeading is the top limit, bottom limit is maxHeading - 360
        /// </summary>
        public static double headingClamp(this double valToClamp, double maxHeading)
        {
            while (valToClamp > maxHeading)
                valToClamp -= 360;
            while (valToClamp < (maxHeading - 360))
                valToClamp += 360;
            return valToClamp;
        }

        /// <summary>
        /// Direction vector from a given heading
        /// </summary>
        public static Vector3 vecHeading(double heading)
        {
            double angleDiff = heading - FlightData.heading;
            return Quaternion.AngleAxis((float)(angleDiff + 90), (Vector3)FlightData.planetUp) * FlightData.surfVesForward;
        }

        /// <summary>
        /// calculate current heading from target vector
        /// </summary>
        public static double calculateTargetHeading(Vector3 direction)
        {
            Vector3 fwd = Vector3.Cross(FlightData.planetUp, direction);
            double heading = -1 * Vector3.Angle(fwd, -FlightData.planetNorth) * Math.Sign(Vector3.Dot(fwd, FlightData.planetEast));
            return heading.headingClamp(360);
        }

        /// <summary>
        /// calculates the angle to feed corrected for 0/360 crossings
        /// eg. if the target is 350 and the current is 10, it will return 370 giving a diff of -20 degrees
        /// else you get +ve 340 and the turn is in the wrong direction
        /// </summary>
        public static double CurrentAngleTargetRel(double current, double target, double maxAngle)
        {
            if (target - current < maxAngle - 360)
                return current - 360;
            else if (target - current > maxAngle)
                return current + 360;
            else
                return current;
        }

        public static bool IsNeutral(AxisBinding axis)
        {
            return axis.IsNeutral() && Math.Abs(axis.GetAxis()) < 0.00001;
        }

        public static bool hasInput(SASList ID)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return hasRollInput();
                case SASList.Hdg:
                    return hasYawInput();
                case SASList.Pitch:
                default:
                    return hasPitchInput();
            }
        }

        public static bool hasYawInput()
        {
            return GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_YAW);
        }

        public static bool hasPitchInput()
        {
            return GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_PITCH);
        }

        public static bool hasRollInput()
        {
            return GameSettings.ROLL_LEFT.GetKey() || GameSettings.ROLL_RIGHT.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_ROLL);
        }

        public static bool hasThrottleInput()
        {
            return GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey() || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown();
        }

        public static double getCurrentVal(SASList ID)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return FlightData.bank;
                case SASList.Hdg:
                    return FlightData.heading;
                case SASList.Pitch:
                default:
                    return FlightData.pitch;
            }
        }

        public static double getCurrentRate(SASList ID)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return FlightData.thisVessel.angularVelocity.y;
                case SASList.Hdg:
                    return FlightData.thisVessel.angularVelocity.z;
                case SASList.Pitch:
                default:
                    return FlightData.thisVessel.angularVelocity.x;
            }
        }
    }
}
