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
            surfVesRight = Vector3d.Cross(planetUp, v.ReferenceTransform.up).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(v.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(v.obt_velocity, obtNormal).normalized;
            srfNormal = Vector3.Cross(v.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(v.srf_velocity, srfNormal).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, v.ReferenceTransform.up);
            heading = -1 * Vector3d.Angle(-surfVesForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVesForward, planetEast));
            if (heading < 0)
                heading += 360; // offset -ve heading by 360 degrees

            progradeHeading = -1 * Vector3d.Angle(-surfVelForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVelForward, planetEast));
            if (progradeHeading < 0)
                progradeHeading += 360; // offset -ve heading by 360 degrees

            bank = Vector3d.Angle(surfVesRight, v.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -v.ReferenceTransform.forward));

            if (v.srfSpeed > 1)
            {
                Vector3d AoAVec = (Vector3d)v.ReferenceTransform.up * Vector3d.Dot(v.ReferenceTransform.up, v.srf_velocity.normalized) +
                                    (Vector3d)v.ReferenceTransform.forward * Vector3d.Dot(v.ReferenceTransform.forward, v.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
                AoA = Vector3d.Angle(AoAVec, v.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(AoAVec, v.ReferenceTransform.forward));

                Vector3d yawVec = (Vector3d)v.ReferenceTransform.up * Vector3d.Dot(v.ReferenceTransform.up, v.srf_velocity.normalized) +
                                    (Vector3d)v.ReferenceTransform.right * Vector3d.Dot(v.ReferenceTransform.right, v.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
                yaw = Vector3d.Angle(yawVec, v.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(yawVec, v.ReferenceTransform.right));
            }
            else
                AoA = yaw = 0;

            oldSpd = v.srfSpeed;
        }
    }
}
