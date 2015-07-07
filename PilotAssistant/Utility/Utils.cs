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

        public static PIDErrorController GetSAS(this SASList id, SurfSAS instance)
        {
            return instance.SASControllers[(int)id];
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
        /// Plane normal vector from a given heading (surface right vector)
        /// </summary>
        public static Vector3 vecHeading(double target, VesselData vd)
        {
            double angleDiff = target - vd.heading;
            return Quaternion.AngleAxis((float)(angleDiff + 90), (Vector3)vd.planetUp) * vd.surfVesForward;
        }

        /// <summary>
        /// calculate current heading from plane normal vector
        /// </summary>
        public static double calculateTargetHeading(Vector3 direction, VesselData vd)
        {
            Vector3 fwd = Vector3.Cross(direction, vd.planetUp);
            double heading = Vector3.Angle(fwd, vd.planetNorth) * Math.Sign(Vector3.Dot(fwd, vd.planetEast));
            return heading.headingClamp(360);
        }

        /// <summary>
        /// calculate current heading from plane rotation
        /// </summary>
        public static double calculateTargetHeading(Quaternion rotation, VesselData vd)
        {
            Vector3 fwd = Vector3.Cross(getPlaneNormal(rotation, vd), vd.planetUp);
            double heading = Vector3.Angle(fwd, vd.planetNorth) * Math.Sign(Vector3.Dot(fwd, vd.planetEast));
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

        /// <summary>
        /// calculate the planet relative rotation from the plane normal vector
        /// </summary>
        public static Quaternion getPlaneRotation(Vector3 planeNormal, VesselData vd)
        {
            return Quaternion.FromToRotation(vd.v.mainBody.transform.right, planeNormal);
        }

        public static Quaternion getPlaneRotation(double heading, VesselData vd)
        {
            Vector3 planeNormal = vecHeading(heading, vd);
            return getPlaneRotation(planeNormal, vd);
        }

        public static Vector3 getPlaneNormal(Quaternion rotation, VesselData vd)
        {
            return rotation * vd.v.mainBody.transform.right;
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

        public static double getCurrentVal(SASList ID, VesselData vd)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return vd.bank;
                case SASList.Hdg:
                    return vd.heading;
                case SASList.Pitch:
                default:
                    return vd.pitch;
            }
        }

        public static double getCurrentRate(SASList ID, Vessel v)
        {
            switch (ID)
            {
                case SASList.Bank:
                    return v.angularVelocity.y;
                case SASList.Hdg:
                    return v.angularVelocity.z;
                case SASList.Pitch:
                default:
                    return v.angularVelocity.x;
            }
        }

        public static Vector3d projectOnPlane(this Vector3d vector, Vector3d planeNormal)
        {
            return vector - Vector3d.Project(vector, planeNormal);
        }
    }
}
