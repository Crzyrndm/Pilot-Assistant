using System;
using System.Collections.Generic;
using UnityEngine;

namespace PilotAssistant.Utility
{
    public class FlightData
    {
        private Vessel thisVessel;
        
        private double pitch = 0;
        private double roll = 0;
        private double yaw = 0;
        private double aoa = 0;
        private double heading = 0;

        private Vector3d planetUp = Vector3d.zero;
        private Vector3d planetNorth = Vector3d.zero;
        private Vector3d planetEast = Vector3d.zero;

        private Vector3d surfVelForward = Vector3d.zero;
        private Vector3d surfVelRight = Vector3d.zero;

        private Vector3d surfVesForward = Vector3d.zero;
        private Vector3d surfVesRight = Vector3d.zero;

        public Vessel Vessel { get { return thisVessel; } set { thisVessel = value; } }

        public double Pitch { get { return pitch; } }
        public double Roll { get { return roll; } }
        public double Yaw { get { return yaw; } }
        public double AoA { get { return aoa; } }
        public double Heading { get { return heading; } }

        public Vector3d PlanetUp { get { return planetUp; } }
        public Vector3d PlanetNorth { get { return planetNorth; } }
        public Vector3d PlanetEast { get { return planetEast; } }

        public Vector3d SurfVelForward { get { return surfVelForward; } }
        public Vector3d SurfVelRight { get { return surfVelRight; } }

        public Vector3d SurfVesForward { get { return surfVesForward; } }
        public Vector3d SurfVesRight { get { return surfVesRight; } }

        public FlightData(Vessel v) { thisVessel = v; }

        public void UpdateAttitude()
        {
            // this gives me 4 frames of reference to use. Orientation,
            // Velocity, and both of the previous parallel to the surface

            // surface vectors
            planetUp = (thisVessel.findWorldCenterOfMass() - thisVessel.mainBody.position).normalized;
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
            roll = Vector3d.Angle(surfVesRight, thisVessel.ReferenceTransform.right) *
                Math.Sign(Vector3d.Dot(surfVesRight, thisVessel.ReferenceTransform.forward));

            // Velocity vector projected onto a plane that divides the airplane into left and right halves
            Vector3d aoaVec = (Vector3d)thisVessel.ReferenceTransform.up *
                Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                (Vector3d)thisVessel.ReferenceTransform.forward *
                Vector3d.Dot(thisVessel.ReferenceTransform.forward, thisVessel.srf_velocity.normalized);
            aoa = Vector3d.Angle(aoaVec, thisVessel.ReferenceTransform.up) *
                Math.Sign(Vector3d.Dot(aoaVec, thisVessel.ReferenceTransform.forward));

            // Velocity vector projected onto the vehicle-horizontal plane
            Vector3d yawVec = (Vector3d)thisVessel.ReferenceTransform.up *
                Vector3d.Dot(thisVessel.ReferenceTransform.up, thisVessel.srf_velocity.normalized) +
                (Vector3d)thisVessel.ReferenceTransform.right *
                Vector3d.Dot(thisVessel.ReferenceTransform.right, thisVessel.srf_velocity.normalized);
            yaw = Vector3d.Angle(yawVec, thisVessel.ReferenceTransform.up) *
                Math.Sign(Vector3d.Dot(yawVec, thisVessel.ReferenceTransform.right));
        }
    }
}
