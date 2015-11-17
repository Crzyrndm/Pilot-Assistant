using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PilotAssistant
{
    public interface IVesselData
    {
        double radarAlt { get; }
        double pitch { get; }
        double bank { get; }
        double yaw { get; }
        double AoA { get; }
        double heading { get; }
        double progradeHeading { get; } // heading for the velocity vector
        double vertSpeed { get; } // precision vertical speed, corrected for planetary curvature
        double acceleration { get; } // acceleration projected onto the velocity vector
        Vector3d planetUp { get; }
        Vector3d planetNorth { get; }
        Vector3d planetEast { get; }
        Vector3d surfVelForward { get; } // velocity vector parallel to the surface
        Vector3d surfVelRight { get; } // vector parallel to the surface and perpendicular to vessel velocity
        Vector3d surfVesForward { get; } // facing vector parallel to the surface
        Vector3d surfVesRight { get; } // vector parallel to the surface and perpendicular to vessel facing
        Vector3d velocity { get; }
        Vector3 obtRadial { get; }
        Vector3 obtNormal { get; }
        Vector3 srfRadial { get; }
        Vector3 srfNormal { get; }
    }
}
