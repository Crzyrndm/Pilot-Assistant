using System;
using System.Collections.Generic;
using UnityEngine;

using PilotAssistant.Utility;
using PilotAssistant.FlightModules;

namespace PilotAssistant.PID
{
    public class Attitude_Controller
    {
        protected Quaternion HeadingNormalRotation = Quaternion.identity;

        public float Heading { get; protected set; }
        public float Pitch { get; protected set; }
        public float Roll { get; protected set; }
        /// <summary>
        /// rate limit for target shifting
        /// </summary>
        public float maxDelta { get; set; }

        /// <summary>
        /// target to calculate off, moves towards the final target if not equal to it
        /// </summary>
        Quaternion curTargetFacing;

        /// <summary>
        /// the previous error for yaw, pitch, and roll
        /// </summary>
        Vector3d lastError;

        /// <summary>
        /// the individual axis controllers 
        /// </summary>
        Axis_Controller[] controllers = new Axis_Controller[3];

        /// <summary>
        /// calculated data for the vessel
        /// </summary>
        VesselData data;

        public enum Axis
        {
            Pitch = 0,
            Roll = 1,
            Yaw = 2
        }

        public Attitude_Controller(VesselData Data, PIDConstants Pitch, PIDConstants Roll, PIDConstants Yaw)
        {
            maxDelta = 1;
            data = Data;
            for (int i = 0; i < 3; i++)
                controllers[i] = new Axis_Controller((Axis)i);
            Initialise(Pitch, Roll, Yaw);
        }

        protected void Initialise(PIDConstants Pitch, PIDConstants Roll, PIDConstants Yaw)
        {
            if (Pitch != null)
                controllers[(int)Axis.Pitch].Initialise(Pitch);
            if (Roll != null)
                controllers[(int)Axis.Roll].Initialise(Roll);
            if (Yaw != null)
                controllers[(int)Axis.Yaw].Initialise(Yaw);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="facing">the vessel transform.rotation parameter.</param>
        /// <param name="turnRate">the vessel angularVelocity parameter</param>
        /// <param name="activeAxis">which axes are active and should be assigned</param>
        /// <returns>(pitch, roll, yaw) control outputs</returns>
        public Vector3d ResponseD(Quaternion facing, Vector3 turnRate, bool[] activeAxis)
        {
            turnRate = turnRate * (float)(180 / Math.PI); // turn rate is given in rad/s
            facing = facing * Quaternion.Euler(-90, 0, 0);

            Quaternion targetFacing = TargetModeSwitch();
            if (curTargetFacing != targetFacing)
                curTargetFacing = Quaternion.RotateTowards(curTargetFacing, targetFacing, 180);

            Vector3 tgtFwd = curTargetFacing * Vector3.forward;
            Vector3 curFwd = facing * Vector3.forward;

            double angleError = Vector3d.Angle(curFwd, tgtFwd);
            Vector3d errorRot = facing.Inverse() * curTargetFacing * Vector3d.forward;
            lastError = (new Vector3d(-errorRot.y, 0, errorRot.x)).normalized * angleError;

            Vector3d rollTargetRight = Quaternion.AngleAxis((float)angleError, Vector3d.Cross(tgtFwd, curFwd)) * curTargetFacing * Vector3d.right;
            lastError.y = Vector3d.Angle(facing * Vector3d.right, rollTargetRight) * Math.Sign(Vector3d.Dot(facing * -Vector3.up, rollTargetRight));

            PIDmode mode = (data.vRef.vesselRef.LandedOrSplashed || !data.vRef.vesselRef.IsControllable) ? PIDmode.PD : PIDmode.PID;
            Vector3d response = Vector3d.zero;

            if (activeAxis[(int)Axis.Pitch])
                response[(int)Axis.Pitch] = controllers[(int)Axis.Pitch].ResponseD(lastError.x, turnRate.x, mode);

            if (activeAxis[(int)Axis.Roll])
                response[(int)Axis.Roll] = controllers[(int)Axis.Roll].ResponseD(lastError.y, turnRate.y, mode);

            if (activeAxis[(int)Axis.Yaw])
                response[(int)Axis.Yaw] = controllers[(int)Axis.Yaw].ResponseD(lastError.z, turnRate.z, mode);

            return response;
        }

        public Vector3 ResponseF(Quaternion facing, Vector3 turnRate, bool[] activeAxis)
        {
            return ResponseD(facing, turnRate, activeAxis);
        }

        public void ResponseF(Quaternion facing, Vector3 turnRate, bool[] activeAxis, FlightCtrlState state)
        {
            Vector3 output = ResponseD(facing, turnRate, activeAxis);
                
            if (activeAxis[(int)Axis.Pitch])
                state.pitch = output[(int)Axis.Pitch];
            if (activeAxis[(int)Axis.Roll])
                state.roll = output[(int)Axis.Roll];
            if (activeAxis[(int)Axis.Yaw])
                state.yaw = output[(int)Axis.Yaw];
        }

        public Axis_Controller GetCtrl(Axis ctrlAxis)
        {
            return controllers[(int)ctrlAxis];
        }

        public void SetTarget(double pitch, double heading, double roll)
        {
            Pitch = (float)pitch;
            Roll = (float)roll;
            Heading = (float)heading;
            HeadingNormalRotation = Utils.getPlaneRotation(heading, data.vRef);
        }

        /// <summary>
        /// preserves surface relative rotation
        /// </summary>
        public void UpdateSrf()
        {
            Heading = (float)Utils.calculateTargetHeading(HeadingNormalRotation, data.vRef);
        }

        /// <summary>
        /// only updates the pitch/heading/roll values, facing is unchanged
        /// </summary>
        //public void UpdateObt()
        //{
        //    Vector3 fwd = targetFacing * Vector3.forward;
        //    Vector3 srfRgt = Vector3.Cross(data.planetUp, fwd);
        //    Vector3 srfFwd = Vector3.Cross(srfRgt, data.planetUp);
        //    Pitch = 90 - Vector3.Angle(data.planetUp, fwd);
        //    Heading = (Vector3.Angle(srfFwd, data.planetNorth) * Math.Sign(Vector3d.Dot(srfFwd, data.planetEast))).headingClamp(360);
        //    Roll = Vector3.Angle(srfRgt, targetFacing * Vector3.right) * Math.Sign(Vector3.Dot(srfRgt, targetFacing * Vector3.up));
        //}

        //public Quaternion GetTarget()
        //{
        //    return targetFacing;
        //}

        Quaternion orbitalTarget = Quaternion.identity;
        Quaternion TargetModeSwitch()
        {
            Quaternion target = Quaternion.identity;
            switch (data.vRef.vesselRef.Autopilot.Mode)
            {
                case VesselAutopilot.AutopilotMode.StabilityAssist:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                    {
                        float hdgAngle = (float)(GetCtrl(Attitude_Controller.Axis.Yaw).Active ? Heading : data.heading);
                        float pitchAngle = (float)(GetCtrl(Attitude_Controller.Axis.Pitch).Active ? Pitch : data.pitch);

                        target = Quaternion.LookRotation(data.planetNorth, data.planetUp);
                        target = Quaternion.AngleAxis(hdgAngle, target * Vector3.up) * target; // heading rotation
                        target = Quaternion.AngleAxis(pitchAngle, target * -Vector3.right) * target; // pitch rotation
                    }
                    else
                        return orbitalTarget * Quaternion.Euler(-90, 0, 0);
                    break;
                case VesselAutopilot.AutopilotMode.Prograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.obt_velocity, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.srf_velocity, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.obt_velocity - data.vRef.vesselRef.targetObject.GetVessel().obt_velocity, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Retrograde:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit)
                        target = Quaternion.LookRotation(-data.vRef.vesselRef.obt_velocity, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.srf_velocity, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.targetObject.GetVessel().obt_velocity - data.vRef.vesselRef.obt_velocity, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialOut:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(data.vRef.vesselData.obtRadial, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(data.vRef.vesselData.srfRadial, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.RadialIn:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-data.vRef.vesselData.obtRadial, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-data.vRef.vesselData.srfRadial, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Normal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(data.vRef.vesselData.obtNormal, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(data.vRef.vesselData.srfNormal, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Antinormal:
                    if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Orbit || FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Target)
                        target = Quaternion.LookRotation(-data.vRef.vesselData.obtNormal, data.planetUp);
                    else if (FlightUIController.speedDisplayMode == FlightUIController.SpeedDisplayModes.Surface)
                        target = Quaternion.LookRotation(-data.vRef.vesselData.srfNormal, data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Target:
                    if (data.vRef.vesselRef.targetObject != null)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.targetObject.GetVessel().GetWorldPos3D() - data.vRef.vesselRef.GetWorldPos3D(), data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.AntiTarget:
                    if (data.vRef.vesselRef.targetObject != null)
                        target = Quaternion.LookRotation(data.vRef.vesselRef.GetWorldPos3D() - data.vRef.vesselRef.targetObject.GetVessel().GetWorldPos3D(), data.planetUp);
                    break;
                case VesselAutopilot.AutopilotMode.Maneuver:
                    if (data.vRef.vesselRef.patchedConicSolver.maneuverNodes != null && data.vRef.vesselRef.patchedConicSolver.maneuverNodes.Count > 0)
                        target = data.vRef.vesselRef.patchedConicSolver.maneuverNodes[0].nodeRotation;
                    break;
            }
            float rollAngle = (float)(GetCtrl(Attitude_Controller.Axis.Roll).Active ? Roll : data.bank);
            target = Quaternion.AngleAxis(-rollAngle, target * Vector3.forward) * target; // roll rotation
            return target;
        }

        public void Setpoint(Axis id, float value)
        {
            switch (id)
            {
                case Axis.Pitch:
                    Pitch = value;
                    break;
                case Axis.Roll:
                    Roll = value;
                    break;
                case Axis.Yaw:
                    Heading = value;
                    HeadingNormalRotation = Utils.getPlaneRotation(value, data.vRef);
                    break;
            }
        }

        public float GetSetpoint(Axis id)
        {
            switch (id)
            {
                case Axis.Pitch:
                    return Pitch;
                case Axis.Roll:
                    return Roll;
                case Axis.Yaw:
                    return Heading;
                default:
                    return 0;
            }
        }
    }
}
