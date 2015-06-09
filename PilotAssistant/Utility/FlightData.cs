using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    public static class FlightData
    {
        public static Vessel thisVessel;

        public static double radarAlt = 0;
        public static double pitch = 0;
        public static double bank = 0;
        public static double yaw = 0;
        public static double AoA = 0;
        public static double heading = 0;
        public static double progradeHeading = 0;
        public static double vertSpeed = 0;
        public static double acceleration = 0;
        static double oldSpd = 0;

        public static Vector3d lastPlanetUp = Vector3d.zero;
        public static Vector3d planetUp = Vector3d.zero;
        public static Vector3d planetNorth = Vector3d.zero;
        public static Vector3d planetEast = Vector3d.zero;

        public static Vector3d surfVelForward = Vector3d.zero;
        public static Vector3d surfVelRight = Vector3d.zero;

        public static Vector3d surfVesForward = Vector3d.zero;
        public static Vector3d surfVesRight = Vector3d.zero;

        public static Vector3d velocity = Vector3d.zero;

        public static Vector3 obtRadial = Vector3.zero;
        public static Vector3 obtNormal = Vector3.zero;
        public static Vector3 srfRadial = Vector3.zero;
        public static Vector3 srfNormal = Vector3.zero;

        static ArrowPointer pointer;
        public static void updateAttitude()
        {
            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface
            // Called in OnPreAutoPilotUpdate. Do not call multiple times per physics frame or the "lastPlanetUp" vector will not be correct and VSpeed will not be calculated correctly
            // Can't just leave it to a Coroutine becuase it has to be called before anything else
            radarAlt = thisVessel.altitude - (thisVessel.mainBody.ocean ? Math.Max(thisVessel.pqsAltitude, 0) : thisVessel.pqsAltitude);
            velocity = thisVessel.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            acceleration = acceleration * 0.8 + 0.2 * (thisVessel.srfSpeed - oldSpd) / TimeWarp.fixedDeltaTime; // vessel.acceleration.magnitude includes acceleration by gravity
            vertSpeed = Vector3d.Dot((planetUp + lastPlanetUp) / 2, velocity);

            // surface vectors
            lastPlanetUp = planetUp;
            planetUp = (thisVessel.rootPart.transform.position - thisVessel.mainBody.position).normalized;
            planetEast = thisVessel.mainBody.getRFrmVel(thisVessel.findWorldCenterOfMass()).normalized;
            planetNorth = Vector3d.Cross(planetEast, planetUp).normalized;
            // Velocity forward and right parallel to the surface
            surfVelForward = Vector3.ProjectOnPlane(thisVessel.srf_velocity, planetUp).normalized;
            surfVelRight = Vector3d.Cross(planetUp, surfVelForward).normalized;
            // Vessel forward and right vetors, parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, thisVessel.ReferenceTransform.up).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(FlightData.thisVessel.obt_velocity, FlightData.planetUp);
            obtRadial = Vector3.Cross(obtNormal, FlightData.thisVessel.obt_velocity);
            srfNormal = Vector3.Cross(FlightData.thisVessel.srf_velocity, FlightData.planetUp);
            srfRadial = Vector3.Cross(srfNormal, FlightData.thisVessel.srf_velocity);

            pitch = 90 - Vector3d.Angle(planetUp, thisVessel.ReferenceTransform.up);
            heading = -1 * Vector3d.Angle(-surfVesForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVesForward, planetEast));
            if (heading < 0)
                heading += 360; // offset -ve heading by 360 degrees

            progradeHeading = -1 * Vector3d.Angle(-surfVelForward, -planetNorth) * Math.Sign(Vector3d.Dot(-surfVelForward, planetEast));
            if (progradeHeading < 0)
                progradeHeading += 360; // offset -ve heading by 360 degrees

            bank = Vector3d.Angle(surfVesRight, thisVessel.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -thisVessel.ReferenceTransform.forward));

            if (thisVessel.srfSpeed > 1)
            {
                Vector3d AoAVec = (Vector3d)thisVessel.ReferenceTransform.up * Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                                    (Vector3d)thisVessel.ReferenceTransform.forward * Vector3d.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
                AoA = Vector3d.Angle(AoAVec, thisVessel.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(AoAVec, thisVessel.ReferenceTransform.forward));

                Vector3d yawVec = (Vector3d)thisVessel.ReferenceTransform.up * Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                                    (Vector3d)thisVessel.ReferenceTransform.right * Vector3d.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
                yaw = Vector3d.Angle(yawVec, thisVessel.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(yawVec, thisVessel.ReferenceTransform.right));
            }
            else
                AoA = yaw = 0;

            oldSpd = thisVessel.srfSpeed;
            //drawArrow(radial);
        }

        public static void drawArrow(Vector3 dir)
        {
            if (pointer == null)
                pointer = ArrowPointer.Create(thisVessel.rootPart.transform, Vector3.zero, dir, 100, Color.red, true);
            else
                pointer.Direction = dir;
        }
    }
}