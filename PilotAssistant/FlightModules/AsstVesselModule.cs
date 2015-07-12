using UnityEngine;

namespace PilotAssistant.FlightModules
{

    public class AsstVesselModule : VesselModule
    {
        public Vessel vesselRef;
        public PilotAssistant vesselAsst;
        public SurfSAS vesselSSAS;
        public Stock_SAS vesselStockSAS;
        public VesselData vesselData;

        public void Awake()
        {
            vesselRef = GetComponent<Vessel>();
            vesselAsst = new PilotAssistant(this);
            vesselSSAS = new SurfSAS(this);
            vesselStockSAS = new Stock_SAS(this);
            vesselData = new VesselData(this);
        }

        public void Start()
        {
            PilotAssistantFlightCore.Instance.addVessel(this);

            vesselAsst.Start();
            vesselSSAS.Start();
            vesselStockSAS.Start();

            vesselRef.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotUpdate);
            vesselRef.OnPostAutopilotUpdate += new FlightInputCallback(postAutoPilotUpdate);

            GameEvents.onVesselChange.Add(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Add(warpHandler);
        }

        public void Update()
        {
            if (vesselRef == null)
                return;
            vesselAsst.Update();
            vesselSSAS.Update();
        }

        public void warpHandler()
        {
            vesselAsst.warpHandler();
            vesselSSAS.warpHandler();
        }

        public void preAutoPilotUpdate(FlightCtrlState state)
        {
            vesselData.updateAttitude();
        }

        public void postAutoPilotUpdate(FlightCtrlState state)
        {
            vesselSSAS.SurfaceSAS(state);
            vesselAsst.vesselController(state);
        }

        public void vesselSwitch(Vessel v)
        {
            if (v == vesselRef)
                vesselAsst.vesselSwitch(v);
        }

        public void OnGUI()
        {
            if (PilotAssistantFlightCore.bHideUI || PilotAssistantFlightCore.Instance.controlledVessels[PilotAssistantFlightCore.Instance.selectedVesselIndex] != this)
                return;
            vesselAsst.drawGUI();
            vesselSSAS.drawGUI();
            vesselStockSAS.drawGUI();
        }

        public void OnDestroy()
        {
            Debug.Log("Asst Vessel Module Destroyed");
            GameEvents.onVesselChange.Remove(vesselSwitch);
            GameEvents.onTimeWarpRateChanged.Remove(warpHandler);

            if (vesselAsst != null)
                vesselAsst.OnDestroy();
            if (PilotAssistantFlightCore.Instance != null)
                PilotAssistantFlightCore.Instance.removeVessel(this);
        }

        public bool isActiveVessel()
        {
            return vesselRef.isActiveVessel;
        }
    }
}
