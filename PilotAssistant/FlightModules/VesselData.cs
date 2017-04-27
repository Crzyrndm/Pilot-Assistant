using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    using Utility;

    public class VesselData
    {
        public VesselData(AsstVesselModule avm)
        {
            vesModule = avm;
        }

        public AsstVesselModule vesModule;

        public double radarAlt;
        public double pitch;
        public double bank;
        public double yaw;
        public double AoA;
        public double heading;
        public double progradeHeading;
        public double vertSpeed;
        public double acceleration;
        public Vector3d planetUp;
        public Vector3d surfVelForward;
        public Vector3d surfVelRight;
        public Vector3d surfVesForward;
        public Vector3d surfVesRight;
        public Vector3d lastVelocity;
        public Vector3d velocity;
        public Vector3 obtRadial;
        public Vector3 obtNormal;
        public Vector3 srfRadial;
        public Vector3 srfNormal;

        /// <summary>
        /// Called in OnPreAutoPilotUpdate. Do not call multiple times per physics frame or the "lastPlanetUp" vector will not be correct and VSpeed will not be calculated correctly
        /// Can't just leave it to a Coroutine becuase it has to be called before anything else
        /// </summary>
        public void UpdateAttitude()
        {
            //if (PilotAssistantFlightCore.calculateDirection)
            //findVesselFwdAxis(vRef.Vessel);
            //else
            vesselFacingAxis = vesModule.Vessel.transform.up;
            planetUp = (vesModule.Vessel.rootPart.transform.position - vesModule.Vessel.mainBody.position).normalized;

            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface
            radarAlt = vesModule.Vessel.altitude - (vesModule.Vessel.mainBody.ocean ? Math.Max(vesModule.Vessel.pqsAltitude, 0) : vesModule.Vessel.pqsAltitude);
            velocity = vesModule.Vessel.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            acceleration = (velocity - lastVelocity).magnitude / TimeWarp.fixedDeltaTime;
            acceleration *= Math.Sign(Vector3.Dot(velocity - lastVelocity, velocity));
            vertSpeed = Vector3d.Dot(planetUp, (velocity + lastVelocity) / 2);
            lastVelocity = velocity;

            // Velocity forward and right vectors parallel to the surface
            surfVelRight = Vector3d.Cross(planetUp, vesModule.Vessel.srf_velocity).normalized;
            surfVelForward = Vector3d.Cross(surfVelRight, planetUp).normalized;

            // Vessel forward and right vectors parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, vesselFacingAxis).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(vesModule.Vessel.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(vesModule.Vessel.obt_velocity, obtNormal).normalized;
            srfNormal = Vector3.Cross(vesModule.Vessel.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(vesModule.Vessel.srf_velocity, srfNormal).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, vesselFacingAxis);
            heading = (Vector3d.Angle(surfVesForward, vesModule.Vessel.north) * Math.Sign(Vector3d.Dot(surfVesForward, vesModule.Vessel.east))).HeadingClamp(360);
            progradeHeading = (Vector3d.Angle(surfVelForward, vesModule.Vessel.north) * Math.Sign(Vector3d.Dot(surfVelForward, vesModule.Vessel.east))).HeadingClamp(360);
            bank = Vector3d.Angle(surfVesRight, vesModule.Vessel.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -vesModule.Vessel.ReferenceTransform.forward));

            if (vesModule.Vessel.srfSpeed > 1)
            {
                Vector3d AoAVec = vesModule.Vessel.srf_velocity.ProjectOnPlane(vesModule.Vessel.ReferenceTransform.right);
                AoA = Vector3d.Angle(AoAVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(AoAVec, vesModule.Vessel.ReferenceTransform.forward));

                Vector3d yawVec = vesModule.Vessel.srf_velocity.ProjectOnPlane(vesModule.Vessel.ReferenceTransform.forward);
                yaw = Vector3d.Angle(yawVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(yawVec, vesModule.Vessel.ReferenceTransform.right));
            }
            else
            {
                AoA = yaw = 0;
            }
        }

        private Vector3 vesselFacingAxis = new Vector3();
        /// <summary>
        /// Find the vessel orientation at the CoM by interpolating from surrounding part transforms
        /// This orientation should be significantly more resistant to vessel flex/wobble than the vessel transform (root part) as a free body rotates about it's CoM
        ///
        /// Has an issue with the origin shifter causing random bounces
        /// </summary>
        public void FindVesselFwdAxis(Vessel v)
        {
            Part closestPart = v.rootPart;
            float offset = (closestPart.transform.position - v.CurrentCoM).sqrMagnitude; // only comparing magnitude, sign and actual value don't matter

            foreach (Part p in v.Parts)
            {
                float partOffset = (p.partTransform.position - v.CurrentCoM).sqrMagnitude;
                if (partOffset < offset)
                {
                    closestPart = p;
                    offset = partOffset;
                }
            }
            ///
            /// now require two things, accounting for any rotation in part placement, and interpolating with surrounding parts (parent/children/symmetry counterparts) to "shift" the location to the CoM
            /// accounting for rotation is the most important, the nearby position will work for now.
            /// Vector3 location = closestPart.partTransform.position - v.CurrentCoM;
            ///
            vesselFacingAxis = closestPart.transform.localRotation * Quaternion.Inverse(closestPart.orgRot) * Vector3.up;
            if (!ReferenceEquals(closestPart.symmetryCounterparts, null))
            {
                for (int i = 0; i < closestPart.symmetryCounterparts.Count; i++)
                {
                    vesselFacingAxis += closestPart.symmetryCounterparts[i].transform.localRotation * Quaternion.Inverse(closestPart.symmetryCounterparts[i].orgRot) * Vector3.up;
                }
                vesselFacingAxis /= (closestPart.symmetryCounterparts.Count + 1);
            }
        }

        private ArrowPointer pointer;
        public void DrawArrow(Vector3 dir, Transform t)
        {
            if (ReferenceEquals(pointer, null))
            {
                pointer = ArrowPointer.Create(t, Vector3.zero, dir, 100, Color.red, true);
            }
            else
            {
                pointer.Direction = dir;
            }
        }

        public void DestroyArrow()
        {
            UnityEngine.Object.Destroy(pointer);
            pointer = null;
        }
    }
}