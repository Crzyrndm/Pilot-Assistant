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

        //internal static Quaternion attitude = Quaternion.identity;

        internal static NavBall ball;

        internal static void updateAttitude()
        {
            // blatant copying of FAR get attitude logic because its just so straightfoward...
            if (ball == null)
                ball = FlightUIController.fetch.GetComponentInChildren<NavBall>();

            // pitch/roll/heading
            Quaternion vesselRot = Quaternion.Inverse(ball.relativeGymbal);
            pitch = (vesselRot.eulerAngles.x > 180) ? (360 - vesselRot.eulerAngles.x) : -vesselRot.eulerAngles.x; // pitch up is +ve
            roll = (vesselRot.eulerAngles.z > 180) ? (vesselRot.eulerAngles.z - 360) : vesselRot.eulerAngles.z;
            heading = vesselRot.eulerAngles.y;

            // pitch AoA
            Vector3 tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + 
                thisVessel.ReferenceTransform.forward * Vector3.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);   //velocity vector projected onto a plane that divides the airplane into left and right halves
            AoA = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.forward);
            AoA = 180 / Math.PI * Math.Asin(AoA);
            if (double.IsNaN(AoA))
                AoA = 0;

            // yaw AoA
            tmpVec = thisVessel.ReferenceTransform.up * Vector3.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) + 
                thisVessel.ReferenceTransform.right * Vector3.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);     //velocity vector projected onto the vehicle-horizontal plane
            yaw = Vector3.Dot(tmpVec.normalized, thisVessel.ReferenceTransform.right);
            yaw = 180 / Math.PI * Math.Asin(yaw);
            if (double.IsNaN(yaw))
                yaw = 0;

            // attitude = surfAtt();
        }

        internal static Quaternion surfAtt()
        {
            // Construct surface relative forward Vector3
            // heading is on x-y plane, z is pitch angle
            // heading 0/360 => x = 1, y = 0
            // heading 90 => x = 0, y = 1
            // heading 180 => x = -1, y = 0
            // heading 270 => x = 0, y = -1
            // pitch 0 => z = 0
            Vector3d surf = new Vector3d(0, 0, 0);
            surf.x = Math.Cos(heading * Math.PI / 180);
            surf.y = Math.Sin(heading * Math.PI / 180);
            surf.z = Math.Sin(pitch * Math.PI / 180);
            return Quaternion.LookRotation(surf);
        }
    }
}
