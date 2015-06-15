
namespace PilotAssistant.FlightModules
{

    public class AsstVesselModule
    {
        public Vessel vesselRef;
        public PilotAssistant vesselAsst;
        public SurfSAS vesselSSAS;
        Stock_SAS vesselStockSAS;
        public VesselData vesselData;

        public AsstVesselModule(Vessel ves)
        {
            vesselRef = ves;
            vesselAsst = new PilotAssistant();
            vesselSSAS = new SurfSAS();
            vesselStockSAS = new Stock_SAS();
            vesselData = new VesselData(ves);
        }

        public void Start()
        {
            vesselAsst.Start(this);
            vesselSSAS.Start(this);
            vesselStockSAS.Start(this);

            vesselRef.OnPreAutopilotUpdate += new FlightInputCallback(preAutoPilotUpdate);
            vesselRef.OnPostAutopilotUpdate += new FlightInputCallback(postAutoPilotUpdate);
        }

        public void Update()
        {
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

        public void OnGUI()
        {
            vesselAsst.drawGUI();
            vesselSSAS.drawGUI();
            vesselStockSAS.drawGUI();
        }

        public void OnDestroy()
        {
            vesselAsst.OnDestroy();
            vesselSSAS.OnDestroy();
            PilotAssistantFlightCore.Instance.removeVessel(this);
        }

        public bool isActiveVessel()
        {
            return vesselRef.isActiveVessel;
        }
    }
}
