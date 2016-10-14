using System;
using UnityEngine;
using PilotAssistant.Utility;

namespace PilotAssistant.FlightModules
{

    public class AsstVesselModule : VesselModule
    {
        public PilotAssistant vesselAsst;
        public VesselData vesselData;

        public override Activation GetActivation()
        {
            return Activation.LoadedVessels;
        }

        public override bool ShouldBeActive()
        {
            return Vessel.loaded;
        }

        protected override void OnStart()
        {
            base.OnStart();
            try
            {
                vesselAsst = new PilotAssistant(this);
                vesselData = new VesselData(this);
                PilotAssistantFlightCore.Instance.addVessel(this);

                vesselAsst.Start();

                Vessel.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotUpdate);
                Vessel.OnPostAutopilotUpdate += new FlightInputCallback(postAutoPilotUpdate);

                GameEvents.onVesselChange.Add(vesselSwitch);
                GameEvents.onTimeWarpRateChanged.Add(warpHandler);
            }
            catch (Exception ex)
            {
                Utils.LogError("Startup error");
                Utils.LogError(ex.Message);
                Utils.LogError(ex.InnerException);
                Utils.LogError(ex.StackTrace);
            }
        }

        public void Update()
        {
            if (ReferenceEquals(Vessel, null))
                return;
            vesselAsst.Update();
        }

        public void warpHandler()
        {
            vesselAsst.warpHandler();
        }

        public void vesselSwitch(Vessel v)
        {
            if (v == Vessel)
                vesselAsst.vesselSwitch(v);
        }

        public void preAutoPilotUpdate(FlightCtrlState state)
        {
            if (Vessel.HoldPhysics)
                return;
            vesselData.updateAttitude();
        }

        public void postAutoPilotUpdate(FlightCtrlState state)
        {
            if (Vessel.HoldPhysics)
                return;
            vesselAsst.vesselController(state);
        }

        public void OnGUI()
        {
            if (PilotAssistantFlightCore.bHideUI || PilotAssistantFlightCore.Instance  == null 
                || PilotAssistantFlightCore.Instance.selectedVesselIndex >= PilotAssistantFlightCore.Instance.controlledVessels.Count
                || PilotAssistantFlightCore.Instance.controlledVessels[PilotAssistantFlightCore.Instance.selectedVesselIndex] != this)
                return;
            vesselAsst.drawGUI();
        }

        public void OnDestroy()
        {
            if (Vessel != null)
            {
                GameEvents.onVesselChange.Remove(vesselSwitch);
                GameEvents.onTimeWarpRateChanged.Remove(warpHandler);

                Vessel.OnPreAutopilotUpdate -= preAutoPilotUpdate;
                Vessel.OnPostAutopilotUpdate -= postAutoPilotUpdate;
                if (!ReferenceEquals(vesselAsst, null))
                {
                    vesselAsst.OnDestroy();
                    if (!ReferenceEquals(PilotAssistantFlightCore.Instance, null))
                        PilotAssistantFlightCore.Instance.removeVessel(this);
                }
                vesselAsst = null;
                vesselData = null;
            }
        }
    }
}
