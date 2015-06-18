using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PilotAssistant.FlightModules
{
    public class VesselData
    {
        public VesselData(Vessel ves)
        {
            v = ves;
        }

        Vessel v;

        public double radarAlt = 0;
        public double pitch = 0;
        public double bank = 0;
        public double yaw = 0;
        public double AoA = 0;
        public double heading = 0;
        public double progradeHeading = 0;
        public double vertSpeed = 0;
        public double acceleration = 0;
        double oldSpd = 0;

        public Vector3d lastPlanetUp = Vector3d.zero;
        public Vector3d planetUp = Vector3d.zero;
        public Vector3d planetNorth = Vector3d.zero;
        public Vector3d planetEast = Vector3d.zero;

        public Vector3d surfVelForward = Vector3d.zero;
        public Vector3d surfVelRight = Vector3d.zero;

        public Vector3d surfVesForward = Vector3d.zero;
        public Vector3d surfVesRight = Vector3d.zero;

        public Vector3d velocity = Vector3d.zero;

        public Vector3 obtRadial = Vector3.zero;
        public Vector3 obtNormal = Vector3.zero;
        public Vector3 srfRadial = Vector3.zero;
        public Vector3 srfNormal = Vector3.zero;

        public void updateAttitude()
        {
            //if (PilotAssistantFlightCore.calculateDirection)
            //    findVesselFwdAxis(v);
            //else
            vesselFacingAxis = v.transform.up;

            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface
            // Called in OnPreAutoPilotUpdate. Do not call multiple times per physics frame or the "lastPlanetUp" vector will not be correct and VSpeed will not be calculated correctly
            // Can't just leave it to a Coroutine becuase it has to be called before anything else
            radarAlt = v.altitude - (v.mainBody.ocean ? Math.Max(v.pqsAltitude, 0) : v.pqsAltitude);
            velocity = v.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            acceleration = acceleration * 0.8 + 0.2 * (v.srfSpeed - oldSpd) / TimeWarp.fixedDeltaTime; // vessel.acceleration.magnitude includes acceleration by gravity
            vertSpeed = Vector3d.Dot((planetUp + lastPlanetUp) / 2, velocity);

            // surface vectors
            lastPlanetUp = planetUp;
            planetUp = (v.rootPart.transform.position - v.mainBody.position).normalized;
            planetEast = v.mainBody.getRFrmVel(v.findWorldCenterOfMass()).normalized;
            planetNorth = Vector3d.Cross(planetEast, planetUp).normalized;
            // Velocity forward and right parallel to the surface
            surfVelForward = Vector3.ProjectOnPlane(v.srf_velocity, planetUp).normalized;
            surfVelRight = Vector3d.Cross(planetUp, surfVelForward).normalized;
            // Vessel forward and right vetors, parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, vesselFacingAxis).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(v.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(v.obt_velocity, obtNormal).normalized;
            srfNormal = Vector3.Cross(v.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(v.srf_velocity, srfNormal).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, vesselFacingAxis);
            heading = -1 * Vector3d.Angle(-surfVesForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVesForward, planetEast));
            if (heading < 0)
                heading += 360; // offset -ve heading by 360 degrees

            progradeHeading = -1 * Vector3d.Angle(-surfVelForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVelForward, planetEast));
            if (progradeHeading < 0)
                progradeHeading += 360; // offset -ve heading by 360 degrees

            bank = Vector3d.Angle(surfVesRight, v.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -v.ReferenceTransform.forward));

            if (v.srfSpeed > 1)
            {
                Vector3d AoAVec = (Vector3d)vesselFacingAxis * Vector3d.Dot(vesselFacingAxis, v.srf_velocity.normalized) +
                                    (Vector3d)v.ReferenceTransform.forward * Vector3d.Dot(v.ReferenceTransform.forward, v.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
                AoA = Vector3d.Angle(AoAVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(AoAVec, v.ReferenceTransform.forward));

                Vector3d yawVec = (Vector3d)vesselFacingAxis * Vector3d.Dot(vesselFacingAxis, v.srf_velocity.normalized) +
                                    (Vector3d)v.ReferenceTransform.right * Vector3d.Dot(v.ReferenceTransform.right, v.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
                yaw = Vector3d.Angle(yawVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(yawVec, v.ReferenceTransform.right));
            }
            else
                AoA = yaw = 0;

            oldSpd = v.srfSpeed;
        }

        Vector3 vesselFacingAxis = new Vector3();
        /// <summary>
        /// Find the vessel orientation at the CoM by interpolating from surrounding part transforms
        /// This orientation should be significantly more resistant to vessel flex/wobble than the vessel transform (root part) as a free body rotates about it's CoM
        /// </summary>
        void findVesselFwdAxis(Vessel v)
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
            // closest part now lists the part that is probably the closest to the CoM on the vessel (if the part tree moves away from the CoM first and then towards the CoM that branch could be closer
            // but is someone really goig to try fly a monstrosity like that)
            //
            // now require two things, accounting for any rotation in part placement, and interpolating with surrounding parts (parent/children/symmetry counterparts) to "shift" the location to the CoM
            // accounting for rotation is the most important, the nearby position will work for now.
            //Vector3 location = closestPart.partTransform.position - v.CurrentCoM;
            Quaternion fixedRotation = closestPart.transform.localRotation * closestPart.orgRot.Inverse();
            vesselFacingAxis = fixedRotation * Vector3.up;
        }

        ArrowPointer pointer;
        public void drawArrow(Vector3 dir, Part p)
        {
            if (pointer == null)
                pointer = ArrowPointer.Create(p.partTransform, Vector3.zero, dir, 100, Color.red, true);
            else
            {
                pointer.Direction = dir;
            }
        }
    }
}
