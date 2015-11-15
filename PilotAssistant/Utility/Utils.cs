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
        public static T Clamp<T>(this T val, T min, T max) where T : System.IComparable<T>
        {
            if (val.CompareTo(min) < 0)
                return min;
            else if (val.CompareTo(max) > 0)
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
        public static double headingClamp(this double valToClamp, double maxHeading, double range = 360)
        {
            return (maxHeading - range) + (valToClamp < 0 ? ((valToClamp - (maxHeading - range)) % 360) + 360 : (valToClamp - (maxHeading - range)) % 360);
        }

        /// <summary>
        /// Plane normal vector from a given heading (surface right vector)
        /// </summary>
        public static Vector3 vecHeading(double target, AsstVesselModule avm)
        {
            double angleDiff = target - avm.vesselData.heading;
            return Quaternion.AngleAxis((float)(angleDiff + 90), (Vector3)avm.vesselData.planetUp) * avm.vesselData.surfVesForward;
        }

        /// <summary>
        /// calculate current heading from plane normal vector
        /// </summary>
        public static double calculateTargetHeading(Vector3 direction, AsstVesselModule avm)
        {
            Vector3 fwd = Vector3.Cross(direction, avm.vesselData.planetUp);
            double heading = Vector3.Angle(fwd, avm.vesselData.planetNorth) * Math.Sign(Vector3.Dot(fwd, avm.vesselData.planetEast));
            return heading.headingClamp(360);
        }

        /// <summary>
        /// calculate current heading from plane rotation
        /// </summary>
        public static double calculateTargetHeading(Quaternion rotation, AsstVesselModule avm)
        {
            Vector3 fwd = Vector3.Cross(getPlaneNormal(rotation, avm), avm.vesselData.planetUp);
            double heading = Vector3.Angle(fwd, avm.vesselData.planetNorth) * Math.Sign(Vector3.Dot(fwd, avm.vesselData.planetEast));
            return heading.headingClamp(360);
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
                return current - 360;
            else if (diff > maxAngle)
                return current + 360;
            else
                return current;
        }

        /// <summary>
        /// calculate the planet relative rotation from the plane normal vector
        /// </summary>
        public static Quaternion getPlaneRotation(Vector3 planeNormal, AsstVesselModule avm)
        {
            return Quaternion.FromToRotation(avm.vesselRef.mainBody.transform.right, planeNormal);
        }

        public static Quaternion getPlaneRotation(double heading, AsstVesselModule avm)
        {
            Vector3 planeNormal = vecHeading(heading, avm);
            return getPlaneRotation(planeNormal, avm);
        }

        public static Vector3 getPlaneNormal(Quaternion rotation, AsstVesselModule avm)
        {
            return rotation * avm.vesselRef.mainBody.transform.right;
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

        public static double speedUnitTransform(SpeedUnits units, double soundSpeed)
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

        public static double mSecToSpeedUnit(this double mSec, SpeedRef mode, SpeedUnits units, AsstVesselModule avm)
        {
            if (mode == SpeedRef.Mach)
                return mSec / avm.vesselRef.speedOfSound;
            else
            {
                double speed = mSec * speedUnitTransform(units, avm.vesselRef.speedOfSound);
                switch (mode)
                {
                    case SpeedRef.True:
                        return speed;
                    case SpeedRef.Indicated:
                        double stagnationPres = Math.Pow(((avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1) * avm.vesselRef.mach * avm.vesselRef.mach * 0.5) + 1, avm.vesselRef.mainBody.atmosphereAdiabaticIndex / (avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1));
                        return speed * Math.Sqrt(avm.vesselRef.atmDensity / 1.225) * stagnationPres;
                    case SpeedRef.Equivalent:
                        return speed * Math.Sqrt(avm.vesselRef.atmDensity / 1.225);
                }
                return 0;
            }
        }

        public static double SpeedUnitToMSec(this double speedUnit, SpeedRef mode, SpeedUnits units, AsstVesselModule avm)
        {
            if (mode == SpeedRef.Mach)
                return speedUnit * avm.vesselRef.speedOfSound;
            else
            {
                double speed = speedUnit / speedUnitTransform(units, avm.vesselRef.speedOfSound);
                switch (mode)
                {
                    case SpeedRef.True:
                        return speed;
                    case SpeedRef.Indicated:
                        double stagnationPres = Math.Pow(((avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1) * avm.vesselRef.mach * avm.vesselRef.mach * 0.5) + 1, avm.vesselRef.mainBody.atmosphereAdiabaticIndex / (avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1));
                        return speed / (Math.Sqrt(avm.vesselRef.atmDensity / 1.225) * stagnationPres);
                    case SpeedRef.Equivalent:
                        return speed / Math.Sqrt(avm.vesselRef.atmDensity / 1.225);
                }
                return 0;
            }
        }

        public static double SpeedTransform(SpeedRef refMode, AsstVesselModule avm)
        {
            switch (refMode)
            {
                case SpeedRef.Indicated:
                    double stagnationPres = Math.Pow(((avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1) * avm.vesselRef.mach * avm.vesselRef.mach * 0.5) + 1, avm.vesselRef.mainBody.atmosphereAdiabaticIndex / (avm.vesselRef.mainBody.atmosphereAdiabaticIndex - 1));
                    return (Math.Sqrt(avm.vesselRef.atmDensity / 1.225) * stagnationPres);
                case SpeedRef.Equivalent:
                    return Math.Sqrt(avm.vesselRef.atmDensity / 1.225);
                case SpeedRef.True:
                default:
                    return 1;
            }
        }

        public static string unitString(SpeedUnits unit)
        {
            switch(unit)
            {
                case SpeedUnits.mSec:
                    return "m/s";
                case SpeedUnits.mach:
                    return "mach";
                case SpeedUnits.knots:
                    return "knots";
                case SpeedUnits.kmph:
                    return "km/h";
                case SpeedUnits.mph:
                    return "mph";
            }
            return "";
        }


        public static string TryGetValue(this ConfigNode node, string key, string defaultValue)
        {
            if (node.HasValue(key))
                return node.GetValue(key);
            return defaultValue;
        }

        public static bool TryGetValue(this ConfigNode node, string key, bool defaultValue)
        {
            bool val;
            if (node.HasValue(key) && bool.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static int TryGetValue(this ConfigNode node, string key, int defaultValue)
        {
            int val;
            if (node.HasValue(key) && int.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static float TryGetValue(this ConfigNode node, string key, float defaultValue)
        {
            float val;
            if (node.HasValue(key) && float.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static double TryGetValue(this ConfigNode node, string key, double defaultValue)
        {
            double val;
            if (node.HasValue(key) && double.TryParse(node.GetValue(key), out val))
                return val;
            return defaultValue;
        }

        public static KeyCode TryGetValue(this ConfigNode node, string key, KeyCode defaultValue)
        {
            if (node.HasValue(key))
            {
                try
                {
                    KeyCode val = (KeyCode)System.Enum.Parse(typeof(KeyCode), node.GetValue(key));
                    return val;
                }
                catch { }
            }
            return defaultValue;
        }

        public static Rect TryGetValue(this ConfigNode node, string key, Rect defaultValue)
        {
            if (node.HasValue(key))
            {
                string[] stringVals = node.GetValue(key).Split(',').Select(s => s.Trim( new char[] {' ', '(', ')' })).ToArray();
                if (stringVals.Length != 4)
                    return defaultValue;
                float x = 0, y = 0, w = 0, h = 0;
                if (!float.TryParse(stringVals[0].Substring(2), out x) || !float.TryParse(stringVals[1].Substring(2), out y) || !float.TryParse(stringVals[2].Substring(6), out w) || !float.TryParse(stringVals[3].Substring(7), out h))
                {
                    Debug.LogError(x + "," + y + "," + w + "," + h);
                    return defaultValue;
                }
                return new Rect(x, y, w, h);
            }
            return defaultValue;
        }
    }
}