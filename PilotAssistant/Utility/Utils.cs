using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.Utility
{
    using FlightModules;

    public static class Utils
    {
        public static T Clamp<T>(this T val, T min, T max) where T : System.IComparable<T>
        {
            if (val.CompareTo(min) < 0)
            {
                return min;
            }
            else if (val.CompareTo(max) > 0)
            {
                return max;
            }
            else
            {
                return val;
            }
        }

        public static Asst_PID_Controller GetAsst(this AsstList id, PilotAssistant instance)
        {
            return instance.controllers[(int)id];
        }

        public static bool IsFlightControlLocked()
        {
            return (InputLockManager.IsLocked(ControlTypes.PITCH) && !PilotAssistant.pitchLockEngaged) || InputLockManager.IsLocked(ControlTypes.ROLL)
                    || (InputLockManager.IsLocked(ControlTypes.YAW) && !PilotAssistant.yawLockEngaged) || InputLockManager.IsLocked(ControlTypes.THROTTLE);
        }

        /// <summary>
        /// Circular rounding to keep compass measurements within a 360 degree range
        /// maxHeading is the top limit, bottom limit is maxHeading - 360
        /// </summary>
        public static double HeadingClamp(this double valToClamp, double maxHeading, double range = 360)
        {
            double temp = (valToClamp - (maxHeading - range)) % range;
            return (maxHeading - range) + (temp < 0 ? temp + range : temp);
        }

        /// <summary>
        /// Plane normal vector from a given heading (surface right vector)
        /// </summary>
        public static Vector3 VecHeading(double target, AsstVesselModule avm)
        {
            double angleDiff = target - avm.vesselData.heading;
            return Quaternion.AngleAxis((float)(angleDiff + 90), (Vector3)avm.vesselData.planetUp) * avm.vesselData.surfVesForward;
        }

        /// <summary>
        /// calculate current heading from plane normal vector
        /// </summary>
        public static double CalculateTargetHeading(Vector3 direction, AsstVesselModule avm)
        {
            var fwd = Vector3.Cross(direction, avm.vesselData.planetUp);
            double heading = Vector3.Angle(fwd, avm.Vessel.north) * Math.Sign(Vector3.Dot(fwd, avm.Vessel.east));
            return heading.HeadingClamp(360);
        }

        /// <summary>
        /// calculate current heading from plane rotation
        /// </summary>
        public static double CalculateTargetHeading(Quaternion rotation, AsstVesselModule avm)
        {
            var fwd = Vector3.Cross(GetPlaneNormal(rotation, avm), avm.vesselData.planetUp);
            double heading = Vector3.Angle(fwd, avm.Vessel.north) * Math.Sign(Vector3.Dot(fwd, avm.Vessel.east));
            return heading.HeadingClamp(360);
        }

        /// <summary>
        /// calculates the angle to feed corrected for 0/360 crossings
        /// eg. if the target is 350 and the current is 10, it will return 370 giving a diff of -20 degrees
        /// else you get +ve 340 and the turn is in the wrong direction
        /// </summary>
        public static double CurrentAngleTargetRel(double current, double target, double maxAngle)
        {
            double diff = target - current;
            if (diff < maxAngle - 360)
            {
                return current - 360;
            }
            else if (diff > maxAngle)
            {
                return current + 360;
            }
            else
            {
                return current;
            }
        }

        /// <summary>
        /// calculate the planet relative rotation from the plane normal vector
        /// </summary>
        public static Quaternion GetPlaneRotation(Vector3 planeNormal, AsstVesselModule avm)
        {
            return Quaternion.FromToRotation(avm.Vessel.mainBody.transform.right, planeNormal);
        }

        public static Quaternion GetPlaneRotation(double heading, AsstVesselModule avm)
        {
            Vector3 planeNormal = VecHeading(heading, avm);
            return GetPlaneRotation(planeNormal, avm);
        }

        public static Vector3 GetPlaneNormal(Quaternion rotation, AsstVesselModule avm)
        {
            return rotation * avm.Vessel.mainBody.transform.right;
        }

        public static bool IsNeutral(AxisBinding axis)
        {
            return axis.IsNeutral() && Math.Abs(axis.GetAxis()) < 0.00001;
        }

        public static bool HasYawInput()
        {
            return GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_YAW);
        }

        public static bool HasPitchInput()
        {
            return GameSettings.PITCH_DOWN.GetKey() || GameSettings.PITCH_UP.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_PITCH);
        }

        public static bool HasRollInput()
        {
            return GameSettings.ROLL_LEFT.GetKey() || GameSettings.ROLL_RIGHT.GetKey() || !Utils.IsNeutral(GameSettings.AXIS_ROLL);
        }

        public static bool HasThrottleInput()
        {
            return GameSettings.THROTTLE_UP.GetKey() || GameSettings.THROTTLE_DOWN.GetKey() || (GameSettings.THROTTLE_CUTOFF.GetKeyDown() && !GameSettings.MODIFIER_KEY.GetKey()) || GameSettings.THROTTLE_FULL.GetKeyDown();
        }

        public static Vector3d ProjectOnPlane(this Vector3d vector, Vector3d planeNormal)
        {
            return vector - Vector3d.Project(vector, planeNormal);
        }

        public static double SpeedUnitTransform(SpeedUnits units, double soundSpeed)
        {
            switch (units)
            {
                case SpeedUnits.mSec:
                    return 1;
                case SpeedUnits.knots:
                    return 1.943844492440604768413343347219;
                case SpeedUnits.kmph:
                    return 3.6;
                case SpeedUnits.mph:
                    return 2.236936;
                case SpeedUnits.mach:
                    return 1 / soundSpeed;
            }
            return 1;
        }

        public static double SpeedTransform(SpeedRef refMode, AsstVesselModule avm)
        {
            switch (refMode)
            {
                case SpeedRef.Indicated:
                    double stagnationPres = Math.Pow(((avm.Vessel.mainBody.atmosphereAdiabaticIndex - 1) * avm.Vessel.mach * avm.Vessel.mach * 0.5) + 1, avm.Vessel.mainBody.atmosphereAdiabaticIndex / (avm.Vessel.mainBody.atmosphereAdiabaticIndex - 1));
                    return Math.Sqrt(avm.Vessel.atmDensity / 1.225) * stagnationPres;
                case SpeedRef.Equivalent:
                    return Math.Sqrt(avm.Vessel.atmDensity / 1.225);
                case SpeedRef.True:
                default:
                    return 1;
            }
        }

        public static string UnitString(SpeedUnits unit)
        {
            switch(unit)
            {
                case SpeedUnits.mSec:
                    return " m/s";
                case SpeedUnits.mach:
                    return " mach";
                case SpeedUnits.knots:
                    return " kn";
                case SpeedUnits.kmph:
                    return " km/h";
                case SpeedUnits.mph:
                    return " mph";
            }
            return string.Empty;
        }


        public static string TryGetValue(this ConfigNode node, string key, string defaultValue)
        {
            if (node.HasValue(key))
            {
                return node.GetValue(key);
            }

            return defaultValue;
        }

        public static bool TryGetValue(this ConfigNode node, string key, bool defaultValue)
        {
            if (node.HasValue(key) && bool.TryParse(node.GetValue(key), out bool val))
            {
                return val;
            }

            return defaultValue;
        }

        public static int TryGetValue(this ConfigNode node, string key, int defaultValue)
        {
            if (node.HasValue(key) && int.TryParse(node.GetValue(key), out int val))
            {
                return val;
            }

            return defaultValue;
        }

        public static float TryGetValue(this ConfigNode node, string key, float defaultValue)
        {
            if (node.HasValue(key) && float.TryParse(node.GetValue(key), out float val))
            {
                return val;
            }

            return defaultValue;
        }

        public static double TryGetValue(this ConfigNode node, string key, double defaultValue)
        {
            if (node.HasValue(key) && double.TryParse(node.GetValue(key), out double val))
            {
                return val;
            }

            return defaultValue;
        }

        public static KeyCode TryGetValue(this ConfigNode node, string key, KeyCode defaultValue)
        {
            if (node.HasValue(key))
            {
                try
                {
                    var val = (KeyCode)System.Enum.Parse(typeof(KeyCode), node.GetValue(key));
                    return val;
                }
                catch { }
            }
            return defaultValue;
        }

        public static Rect TryGetValue(this ConfigNode node, string key, Rect defaultValue)
        {
            var val = new Rect();
            if (node.TryGetValue(key, ref val))
            {
                return val;
            }
            return defaultValue;
        }
    }
}