using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    public static class FlightData
    {
        internal static Vessel thisVessel;

        internal static double pitch = 0;
        internal static double roll = 0;
        internal static double yaw = 0;
        internal static double AoA = 0;
        internal static double heading = 0;
        internal static double progradeHeading = 0;
        internal static double vertSpeed = 0;

        internal static Vector3d lastPlanetUp = Vector3d.zero;
        internal static Vector3d planetUp = Vector3d.zero;
        internal static Vector3d planetNorth = Vector3d.zero;
        internal static Vector3d planetEast = Vector3d.zero;

        internal static Vector3d surfVelForward = Vector3d.zero;
        internal static Vector3d surfVelRight = Vector3d.zero;

        internal static Vector3d surfVesForward = Vector3d.zero;
        internal static Vector3d surfVesRight = Vector3d.zero;

        internal static Vector3d velocity = Vector3d.zero;

        internal static void updateAttitude()
        {
            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface

            // surface vectors
            lastPlanetUp = planetUp;
            planetUp = (thisVessel.rootPart.transform.position - thisVessel.mainBody.position).normalized;
            planetEast = thisVessel.mainBody.getRFrmVel(thisVessel.findWorldCenterOfMass()).normalized;
            planetNorth = Vector3d.Cross(planetUp, planetEast).normalized;
            // Velocity forward and right parallel to the surface
            surfVelForward = (thisVessel.srf_velocity - thisVessel.verticalSpeed * planetUp).normalized;
            surfVelRight = Vector3d.Cross(planetUp, surfVelForward).normalized;
            // Vessel forward and right vetors, parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, thisVessel.ReferenceTransform.up).normalized;
            surfVesForward = Vector3d.Cross(planetUp, surfVesRight).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, thisVessel.ReferenceTransform.up);
            heading = -1 * Vector3d.Angle(surfVesForward, planetNorth) * Math.Sign(Vector3d.Dot(surfVesForward, planetEast));
            if (heading < 0)
                heading = 360 + heading; // heading is -(0-180), so it's actually 360 - Abs(heading)

            progradeHeading = -1 * Vector3d.Angle(-surfVelForward, planetNorth) * Math.Sign(Vector3d.Dot(-surfVelForward, planetEast));
            if (progradeHeading < 0)
                progradeHeading = 360 + progradeHeading; // heading is -(0-180), so it's actually 360 - Abs(heading)

            roll = Vector3d.Angle(surfVesRight, thisVessel.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, thisVessel.ReferenceTransform.forward));

            Vector3d AoAVec = (Vector3d)thisVessel.ReferenceTransform.up * Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                (Vector3d)thisVessel.ReferenceTransform.forward * Vector3d.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            AoA = Vector3d.Angle(AoAVec, thisVessel.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(AoAVec, thisVessel.ReferenceTransform.forward));

            Vector3d yawVec = (Vector3d)thisVessel.ReferenceTransform.up * Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                (Vector3d)thisVessel.ReferenceTransform.right * Vector3d.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
            yaw = Vector3d.Angle(yawVec, thisVessel.ReferenceTransform.up) * Math.Sign(Vector3d.Dot(yawVec, thisVessel.ReferenceTransform.right));

            velocity = thisVessel.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            vertSpeed = Vector3d.Dot((planetUp + lastPlanetUp) / 2, velocity);
        }
    }
}